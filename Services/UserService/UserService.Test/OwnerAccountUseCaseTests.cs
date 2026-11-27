using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Test;

// US-116 U2–U5: tài khoản chủ bãi do Admin cấp từ hồ sơ đối tác + link kích hoạt (chủ bãi tự đặt mật khẩu).
public class OwnerAccountUseCaseTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 3, 0, 0, DateTimeKind.Utc);
    private readonly Store store = new();
    private DateTime clockNow = Now;

    private OwnerAccountUseCases Owners => new(store, store, new Passwords(), new Delivery(), new Cipher(),
        new OwnerActivationOptions(TimeSpan.FromHours(72)), new Clock(() => clockNow));

    private static IssueOwnerAccountRequest Request(string email = "minh@example.vn", string phone = "0901234567", string taxCode = "0312345678")
        => new(12, " Trần Văn Minh ", email, phone, "Công ty TNHH Bãi xe Thủ Đức", taxCode, "12 Võ Văn Ngân", "970436", "Vietcombank",
            "0071000123456", "CONG TY TNHH BAI XE THU DUC");

    /// <summary>Token thật lấy từ outbox (cipher giả lưu bản rõ sau tiền tố purpose).</summary>
    private string TokenFromOutbox(int index = -1)
    {
        var messages = store.Outbox.Where(m => m.EventType == OwnerAccountUseCases.ActivationEvent).ToList();
        var payload = JsonDocument.Parse((index < 0 ? messages[^1] : messages[index]).PayloadJson).RootElement;
        return new Cipher().Decrypt(payload.GetProperty("EncryptedToken").GetString()!, OwnerAccountUseCases.ActivationCipherPurpose);
    }

    // ==================== U2 ====================

    [Fact]
    public async Task Issue_creates_pending_lot_owner_with_profile_bank_and_activation_link()
    {
        var issued = await Owners.IssueAsync(Request(), default);

        var user = Assert.Single(store.Users);
        Assert.Equal((issued.UserId, user.OwnerProfile!.Id), (user.Id, issued.OwnerProfileId));
        Assert.Equal(("Trần Văn Minh", "minh@example.vn", "0901234567", UserStatus.PendingVerification),
            (user.FullName, user.Email, user.PhoneNumber, user.Status));
        Assert.Equal(UserRoleType.LotOwner, Assert.Single(user.Roles).Role);
        Assert.Equal(("0312345678", "970436", "0071000123456"), (user.OwnerProfile.TaxCode, user.OwnerProfile.BankBin, user.OwnerProfile.BankAccountNumber));
        Assert.StartsWith("hash:", user.PasswordHash);

        var activation = Assert.Single(store.Otps);
        Assert.Equal((OtpPurpose.OwnerActivation, user.Id, "minh@example.vn", Now.AddHours(72)),
            (activation.Purpose, activation.UserId, activation.Destination, activation.ExpiresAtUtc));
        var token = TokenFromOutbox();
        Assert.Equal(OwnerAccountUseCases.HashToken(token), activation.CodeHash);
        Assert.DoesNotContain(token, store.Outbox.Single(m => m.EventType == OwnerAccountUseCases.ActivationEvent).PayloadJson.Replace(
            OwnerAccountUseCases.ActivationCipherPurpose + "|" + token, ""));
        Assert.Contains(store.Events, e => e is OwnerProfileCreated o && o.OwnerProfileId == issued.OwnerProfileId);
        Assert.Equal(1, store.Commits);
    }

    [Fact]
    public async Task Issue_again_for_the_same_application_returns_the_same_account()
    {
        var first = await Owners.IssueAsync(Request(), default);

        var second = await Owners.IssueAsync(Request(), default);

        Assert.Equal(first, second);
        Assert.Single(store.Users);
        Assert.Single(store.Otps);
    }

    [Theory]
    [InlineData("minh@example.vn", "0999999999", "0399999999")]
    [InlineData("other@example.vn", "0901234567", "0399999999")]
    [InlineData("other@example.vn", "0999999999", "0312345678")]
    public async Task Issue_conflicts_with_another_account(string email, string phone, string taxCode)
    {
        await Owners.IssueAsync(Request(), default);

        await Assert.ThrowsAsync<ConflictException>(() => Owners.IssueAsync(Request(email, phone, taxCode), default));
        Assert.Single(store.Users);
    }

    [Fact]
    public async Task Issue_conflicts_when_email_belongs_to_an_active_account()
    {
        var issued = await Owners.IssueAsync(Request(), default);
        store.Users.Single(u => u.Id == issued.UserId).Status = UserStatus.Active;

        await Assert.ThrowsAsync<ConflictException>(() => Owners.IssueAsync(Request(), default));
    }

    [Fact]
    public async Task Issue_validates_input()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Owners.IssueAsync(Request(email: "0901234567"), default));
        await Assert.ThrowsAsync<ValidationException>(() => Owners.IssueAsync(Request() with { BankAccountNumber = " " }, default));
        await Assert.ThrowsAsync<ValidationException>(() => Owners.IssueAsync(Request() with { PartnerApplicationId = 0 }, default));
        Assert.Empty(store.Users);
    }

    // ==================== U3 ====================

    [Fact]
    public async Task Resend_revokes_the_old_link_and_sends_a_new_one()
    {
        var issued = await Owners.IssueAsync(Request(), default);
        var oldToken = TokenFromOutbox();

        await Owners.ResendActivationAsync(issued.OwnerProfileId, default);

        Assert.Equal(2, store.Otps.Count);
        Assert.NotNull(store.Otps[0].ConsumedAtUtc);
        Assert.Null(store.Otps[1].ConsumedAtUtc);
        await Assert.ThrowsAsync<ValidationException>(() => Owners.ValidateActivationAsync(oldToken, default));
        Assert.Equal("Trần Văn Minh", (await Owners.ValidateActivationAsync(TokenFromOutbox(), default)).FullName);
    }

    [Fact]
    public async Task Resend_fails_for_missing_or_activated_accounts()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Owners.ResendActivationAsync(999, default));
        var issued = await Owners.IssueAsync(Request(), default);
        await Owners.ActivateAsync(new ActivateOwnerRequest(TokenFromOutbox(), "matkhau123"), default);

        await Assert.ThrowsAsync<ConflictException>(() => Owners.ResendActivationAsync(issued.OwnerProfileId, default));
    }

    // ==================== U4 / U5 ====================

    [Fact]
    public async Task Validate_returns_who_the_link_belongs_to()
    {
        await Owners.IssueAsync(Request(), default);

        var info = await Owners.ValidateActivationAsync(TokenFromOutbox(), default);

        Assert.Equal(("Trần Văn Minh", "minh@example.vn", "Công ty TNHH Bãi xe Thủ Đức", Now.AddHours(72)),
            (info.FullName, info.Email, info.BusinessName, info.ExpiresAtUtc));
    }

    [Fact]
    public async Task Activate_sets_password_activates_and_burns_the_link()
    {
        var issued = await Owners.IssueAsync(Request(), default);
        var token = TokenFromOutbox();

        await Owners.ActivateAsync(new ActivateOwnerRequest(token, "matkhau123"), default);

        var user = store.Users.Single(u => u.Id == issued.UserId);
        Assert.Equal((UserStatus.Active, "hash:matkhau123", true), (user.Status, user.PasswordHash, user.EmailConfirmed));
        Assert.NotNull(store.Otps.Single().ConsumedAtUtc);
        Assert.Contains(store.Security, e => e.EventType == SecurityEventType.AccountActivated && e.UserId == user.Id);
        await Assert.ThrowsAsync<ValidationException>(() => Owners.ActivateAsync(new ActivateOwnerRequest(token, "khac12345"), default));
    }

    [Fact]
    public async Task Expired_wrong_or_weak_requests_are_rejected()
    {
        await Owners.IssueAsync(Request(), default);
        var token = TokenFromOutbox();

        await Assert.ThrowsAsync<ValidationException>(() => Owners.ActivateAsync(new ActivateOwnerRequest(token, "short"), default));
        await Assert.ThrowsAsync<ValidationException>(() => Owners.ValidateActivationAsync("not-a-token", default));
        await Assert.ThrowsAsync<ValidationException>(() => Owners.ValidateActivationAsync(null, default));
        clockNow = Now.AddHours(73);
        await Assert.ThrowsAsync<ValidationException>(() => Owners.ActivateAsync(new ActivateOwnerRequest(token, "matkhau123"), default));
        Assert.Equal(UserStatus.PendingVerification, store.Users.Single().Status);
    }

    // ==================== Fakes ====================

    private static void Id(BaseEntity entity, int id) => typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(entity, id);

    private sealed class Clock(Func<DateTime> now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now()); }
    private sealed class Passwords : IPasswordHasher
    {
        public string Hash(string value) => "hash:" + value;
        public bool Verify(string value, string hash) => hash == Hash(value);
    }
    private sealed class Cipher : ISecretCipher
    {
        public string Encrypt(string value, string purpose) => purpose + "|" + value;
        public string Decrypt(string value, string purpose) => value[(purpose.Length + 1)..];
    }
    private sealed class Delivery : IOtpDeliveryConfiguration { public void EnsureAvailable() { } }

    private sealed class Store : IIdentityStore, IOwnerAccountStore
    {
        public List<User> Users = [];
        public List<OtpCode> Otps = [];
        public List<OutboxMessage> Outbox = [];
        public List<SecurityEvent> Security = [];
        public List<IntegrationEvent> Events = [];
        public int Commits;
        private int next = 10;

        public Task<IIdentityTransaction> BeginAsync(CancellationToken ct) => Task.FromResult<IIdentityTransaction>(new Tx(this));
        public Task<User?> FindContactAsync(string contact, CancellationToken ct)
            => Task.FromResult(Users.FirstOrDefault(u => !u.IsDeleted && (u.Email == contact || u.PhoneNumber == contact)));
        public Task<User?> FindUserAsync(int id, bool forUpdate, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        public Task<OtpCode?> LatestOtpAsync(int userId, OtpPurpose purpose, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<OtpCode>> PendingOtpsAsync(int userId, OtpPurpose purpose, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> OtpFailuresAsync(int userId, DateTime since, CancellationToken ct) => Task.FromResult(0);
        public Task RevokeSessionsAsync(int userId, DateTime now, CancellationToken ct) => Task.CompletedTask;
        public void Add(object entity)
        {
            if (entity is BaseEntity e && e.Id == 0) Id(e, next++);
            switch (entity)
            {
                case User user:
                    if (user.OwnerProfile is { } profile) { Id(profile, next++); profile.UserId = user.Id; profile.User = user; }
                    Users.Add(user);
                    break;
                case OtpCode otp: otp.User = Users.Single(u => u.Id == otp.UserId); Otps.Add(otp); break;
                case OutboxMessage m: Outbox.Add(m); break;
                case SecurityEvent s: Security.Add(s); break;
            }
        }
        public void Event(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<OwnerProfile?> FindByTaxCodeAsync(string taxCode, CancellationToken ct)
            => Task.FromResult(Users.Select(u => u.OwnerProfile).FirstOrDefault(p => p?.TaxCode == taxCode));
        public Task<OwnerProfile?> FindProfileAsync(int ownerProfileId, CancellationToken ct)
            => Task.FromResult(Users.Select(u => u.OwnerProfile).FirstOrDefault(p => p?.Id == ownerProfileId));
        public Task<OtpCode?> FindActivationAsync(string tokenHash, CancellationToken ct)
            => Task.FromResult(Otps.FirstOrDefault(o => o.CodeHash == tokenHash && o.Purpose == OtpPurpose.OwnerActivation));
        public Task<IReadOnlyList<OtpCode>> OpenActivationsAsync(int userId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<OtpCode>>(Otps.Where(o => o.UserId == userId && o.Purpose == OtpPurpose.OwnerActivation && o.ConsumedAtUtc == null).ToList());

        private sealed class Tx(Store store) : IIdentityTransaction
        {
            public Task CommitAsync(CancellationToken ct) { store.Commits++; return Task.CompletedTask; }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
