using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Application.Features.Privacy;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Test;

// US-096 / US-116: Admin đình chỉ, mở lại, xóa (ẩn danh hóa) user và gỡ tài khoản chủ bãi chưa kích hoạt khi hủy hồ sơ đối tác.
public class UserLifecycleUseCaseTests
{
    private const int AdminId = 1;
    private static readonly DateTime Now = new(2026, 11, 20, 3, 0, 0, DateTimeKind.Utc);
    private readonly Store store = new();

    private UserLifecycleUseCases UseCases(int? adminId = AdminId)
        => new(store, store, store, store, new Current(adminId), new Passwords(), new Clock());

    private User AddUser(string email, UserStatus status = UserStatus.Active, UserRoleType role = UserRoleType.Driver, bool owner = false)
    {
        var user = new User { FullName = "Trần Văn Minh", Email = email, PhoneNumber = "0901234567", Status = status, PasswordHash = "hash:x" };
        user.Roles.Add(new UserRole { Role = role });
        if (owner) user.OwnerProfile = new OwnerProfile { BusinessName = "CTY", TaxCode = "0312345678", BankBin = "1", BankName = "B", BankAccountNumber = "1", BankAccountName = "A" };
        store.Add(user);
        return user;
    }

    private static string Version(User user) => Convert.ToBase64String(user.RowVersion);

    // ==================== Suspend / Reactivate ====================

    [Fact]
    public async Task Suspend_indefinitely_locks_user_revokes_sessions_and_records_security_event()
    {
        var user = AddUser("a@example.vn");

        var result = await UseCases().SuspendAsync(user.Id, new SuspendUserRequest("  Nghi gian lận  ", null, Version(user)), default);

        Assert.Equal((user.Id, "Locked", null, true), (result.UserId, result.Status, result.LockedUntilUtc, result.Changed));
        Assert.Equal(("Nghi gian lận", UserStatus.Locked), (user.LockReason, user.Status));
        Assert.True(user.IsSuspendedAt(Now));
        Assert.Equal([user.Id], store.RevokedFor);
        Assert.Contains("Nghi gian lận", Assert.Single(store.Security).Detail);
    }

    [Fact]
    public async Task Suspend_until_date_keeps_active_status_and_expires_by_itself()
    {
        var user = AddUser("a@example.vn");
        var until = Now.AddDays(3);

        await UseCases().SuspendAsync(user.Id, new SuspendUserRequest("Tạm khóa", until, null), default);

        Assert.Equal((UserStatus.Active, until), (user.Status, user.LockedUntilUtc));
        Assert.True(user.IsSuspendedAt(Now));
        Assert.False(user.IsSuspendedAt(until.AddSeconds(1)));   // hết hạn: đăng nhập / refresh / validator coi là hoạt động trở lại
    }

    [Fact]
    public async Task Suspend_is_idempotent_for_same_request()
    {
        var user = AddUser("a@example.vn");
        var request = new SuspendUserRequest("Lý do", null, null);
        await UseCases().SuspendAsync(user.Id, request, default);

        var again = await UseCases().SuspendAsync(user.Id, request, default);

        Assert.False(again.Changed);
        Assert.Single(store.Security);
    }

    [Fact]
    public async Task Suspend_rejects_missing_reason_past_date_self_pending_banned_and_last_admin()
    {
        var user = AddUser("a@example.vn");
        await Assert.ThrowsAsync<ValidationException>(() => UseCases().SuspendAsync(user.Id, new SuspendUserRequest(" ", null, null), default));
        await Assert.ThrowsAsync<ValidationException>(() => UseCases().SuspendAsync(user.Id, new SuspendUserRequest("x", Now.AddMinutes(-1), null), default));
        await Assert.ThrowsAsync<AuthenticationException>(() => UseCases(null).SuspendAsync(user.Id, new SuspendUserRequest("x", null, null), default));
        await Assert.ThrowsAsync<ConflictException>(() => UseCases().SuspendAsync(AdminId, new SuspendUserRequest("x", null, null), default));

        var pending = AddUser("p@example.vn", UserStatus.PendingVerification);
        var banned = AddUser("b@example.vn", UserStatus.Banned);
        await Assert.ThrowsAsync<ConflictException>(() => UseCases().SuspendAsync(pending.Id, new SuspendUserRequest("x", null, null), default));
        await Assert.ThrowsAsync<ConflictException>(() => UseCases().SuspendAsync(banned.Id, new SuspendUserRequest("x", null, null), default));

        var lastAdmin = AddUser("admin@example.vn", role: UserRoleType.Admin);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => UseCases().SuspendAsync(lastAdmin.Id, new SuspendUserRequest("x", null, null), default));
        Assert.Contains("quản trị viên cuối cùng", ex.Message);
        Assert.Equal(UserStatus.Active, lastAdmin.Status);
        Assert.Empty(store.Security);
    }

    [Fact]
    public async Task Suspend_with_stale_row_version_is_409_and_changes_nothing()
    {
        var user = AddUser("a@example.vn");

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            UseCases().SuspendAsync(user.Id, new SuspendUserRequest("x", null, Convert.ToBase64String([9, 9, 9])), default));

        Assert.Contains("tải lại", ex.Message);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public async Task Reactivate_restores_active_and_is_idempotent()
    {
        var user = AddUser("a@example.vn");
        await UseCases().SuspendAsync(user.Id, new SuspendUserRequest("Lý do", null, null), default);

        var result = await UseCases().ReactivateAsync(user.Id, new ReactivateUserRequest("Đã xác minh", Version(user)), default);
        var again = await UseCases().ReactivateAsync(user.Id, new ReactivateUserRequest("Đã xác minh", null), default);

        Assert.Equal(("Active", true, false), (result.Status, result.Changed, again.Changed));
        Assert.Equal((UserStatus.Active, null, null), (user.Status, user.LockedUntilUtc, user.LockReason));
        Assert.Equal(SecurityEventType.AccountUnlocked, store.Security[^1].EventType);
        await Assert.ThrowsAsync<ValidationException>(() => UseCases().ReactivateAsync(user.Id, new ReactivateUserRequest("", null), default));
    }

    // ==================== Erase ====================

    [Fact]
    public async Task Erase_anonymizes_user_frees_email_and_phone_and_terminates_owner_profile_with_tax_code_released()
    {
        var user = AddUser("owner@example.vn", role: UserRoleType.LotOwner, owner: true);
        user.EmailConfirmed = true;
        store.Add(new OtpCode { UserId = user.Id, Destination = "owner@example.vn", Purpose = OtpPurpose.Register, CodeHash = "h", ExpiresAtUtc = Now.AddMinutes(5) });

        var result = await UseCases().EraseAsync(user.Id, new EraseUserRequest("Yêu cầu từ chủ bãi", Version(user)), default);

        Assert.Equal(("Deleted", true), (result.Status, result.Changed));
        Assert.True(user.IsDeleted);
        Assert.Equal((UserAnonymizer.ErasedName, $"deleted-{user.Id}@deleted.invalid", null), (user.FullName, user.Email, user.PhoneNumber));
        Assert.Equal((OwnerStatus.Terminated, true, null), (user.OwnerProfile!.Status, user.OwnerProfile.IsLocked, user.OwnerProfile.TaxCode));
        Assert.All(store.Otps, o => Assert.NotNull(o.ConsumedAtUtc));
        Assert.Contains(user.Id, store.RevokedFor);
        // Email / SĐT cũ dùng lại được: không còn user nào giữ chúng.
        Assert.Null(await store.FindContactAsync("owner@example.vn", default));
        Assert.Null(await store.FindContactAsync("0901234567", default));
    }

    [Fact]
    public async Task Erase_blocks_self_and_last_admin_but_allows_admin_when_another_exists()
    {
        var admin = AddUser("admin@example.vn", role: UserRoleType.Admin);
        var self = AddUser("self@example.vn", role: UserRoleType.Admin);
        await Assert.ThrowsAsync<ConflictException>(() => UseCases(self.Id).EraseAsync(self.Id, new EraseUserRequest("x", null), default));
        await UseCases(self.Id).EraseAsync(admin.Id, new EraseUserRequest("Nghỉ việc", null), default);   // còn "self" là Admin khác
        Assert.True(admin.IsDeleted);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => UseCases(99).EraseAsync(self.Id, new EraseUserRequest("x", null), default));
        Assert.Contains("quản trị viên cuối cùng", ex.Message);
    }

    // ==================== Discard owner account (hủy hồ sơ đối tác) ====================

    [Fact]
    public async Task Discard_removes_never_activated_owner_account_revokes_links_and_allows_reissue_with_same_contacts()
    {
        var user = AddUser("minh@example.vn", UserStatus.PendingVerification, UserRoleType.LotOwner, owner: true);
        var link = new OtpCode { UserId = user.Id, Destination = "minh@example.vn", Purpose = OtpPurpose.OwnerActivation, CodeHash = "t", ExpiresAtUtc = Now.AddHours(10) };
        store.Add(link);
        var profileId = user.OwnerProfile!.Id;

        var result = await UseCases().DiscardOwnerAccountAsync(profileId, new DiscardOwnerAccountRequest("Hủy hồ sơ PA-1"), default);
        var again = await UseCases().DiscardOwnerAccountAsync(profileId, new DiscardOwnerAccountRequest("Hủy hồ sơ PA-1"), default);

        Assert.Equal(("Deleted", true, false), (result.Status, result.Changed, again.Changed));
        Assert.NotNull(link.ConsumedAtUtc);
        Assert.Equal((OwnerStatus.Terminated, null), (user.OwnerProfile.Status, user.OwnerProfile.TaxCode));
        Assert.Null(await store.FindByTaxCodeAsync("0312345678", default));
        Assert.Null(await store.FindContactAsync("minh@example.vn", default));
    }

    [Fact]
    public async Task Discard_is_409_when_owner_already_activated_or_ever_signed_in_and_404_when_unknown()
    {
        var activated = AddUser("a@example.vn", UserStatus.Active, UserRoleType.LotOwner, owner: true);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            UseCases().DiscardOwnerAccountAsync(activated.OwnerProfile!.Id, new DiscardOwnerAccountRequest("x"), default));
        Assert.Contains("Đình chỉ hoặc Xóa user", ex.Message);
        Assert.False(activated.IsDeleted);

        var signedIn = AddUser("b@example.vn", UserStatus.PendingVerification, UserRoleType.LotOwner, owner: true);
        store.SignedIn.Add(signedIn.Id);
        await Assert.ThrowsAsync<ConflictException>(() =>
            UseCases().DiscardOwnerAccountAsync(signedIn.OwnerProfile!.Id, new DiscardOwnerAccountRequest("x"), default));

        await Assert.ThrowsAsync<NotFoundException>(() => UseCases().DiscardOwnerAccountAsync(999, new DiscardOwnerAccountRequest("x"), default));
        await Assert.ThrowsAsync<ValidationException>(() => UseCases().DiscardOwnerAccountAsync(activated.OwnerProfile!.Id, new DiscardOwnerAccountRequest(""), default));
    }

    // ==================== Mã lỗi kích hoạt + email đối tác ====================

    [Fact]
    public void Error_codes_travel_on_validation_and_conflict_exceptions()
    {
        Assert.Equal(OwnerAccountUseCases.ActivationExpired, new ValidationException("m", OwnerAccountUseCases.ActivationExpired).Code);
        Assert.Equal("c", new ConflictException("m", "c").Code);
        Assert.Null(new ValidationException("m").Code);
    }

    [Fact]
    public async Task Partner_email_goes_to_outbox_with_encrypted_link_and_rejects_bad_input()
    {
        var emails = new PartnerEmailUseCases(store, new Cipher(), new Delivery());
        var link = "https://web.test/partner/apply/edit#code=PA-1&token=SECRETTOKEN";

        await emails.EnqueueAsync(new PartnerEmailRequest(PartnerEmailUseCases.InfoRequested, "an@example.com", "Nguyễn Văn An", "PA-2026-0012",
            "Chụp lại CCCD", ["Mã số thuế"], Now.AddDays(7), link), default);

        var message = Assert.Single(store.Outbox);
        Assert.Equal(PartnerEmailUseCases.EmailEvent, message.EventType);
        // Link (có token) chỉ nằm trong trường đã mã hóa; không có trường "Link" rõ.
        Assert.Contains("\"EncryptedLink\":\"" + PartnerEmailUseCases.CipherPurpose + "|", message.PayloadJson);
        Assert.DoesNotContain("\"Link\":", message.PayloadJson);
        Assert.Contains("an@example.com", message.PayloadJson);

        await Assert.ThrowsAsync<ValidationException>(() => emails.EnqueueAsync(new PartnerEmailRequest("Other", "an@example.com", "A", "PA-1", null, null, null, link), default));
        await Assert.ThrowsAsync<ValidationException>(() => emails.EnqueueAsync(new PartnerEmailRequest(PartnerEmailUseCases.InfoRequested, "không phải email", "A", "PA-1", null, null, null, link), default));
        await Assert.ThrowsAsync<ValidationException>(() => emails.EnqueueAsync(new PartnerEmailRequest(PartnerEmailUseCases.InfoRequested, "an@example.com", "A", "PA-1", null, null, null, "javascript:alert(1)"), default));
        Assert.Single(store.Outbox);
    }

    // ==================== Fakes ====================

    private static void SetId(BaseEntity entity, int id) => typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(entity, id);

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
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
    private sealed class Current(int? userId) : ICurrentUser
    {
        public int? UserId => userId;
        public string? Email => null;
        public IReadOnlyList<string> Roles => ["Admin"];
        public int? OwnerProfileId => null;
        public bool IsAuthenticated => userId is not null;
    }

    private sealed class Store : IIdentityStore, IOwnerAccountStore, IDataRequestStore, IUserLifecycleStore
    {
        public List<User> Users = [];
        public List<OtpCode> Otps = [];
        public List<OutboxMessage> Outbox = [];
        public List<SecurityEvent> Security = [];
        public List<int> RevokedFor = [];
        public HashSet<int> SignedIn = [];
        private int next = 100;   // Id user khác AdminId (1) để không dính chặn "tự thao tác"

        public Task<IIdentityTransaction> BeginAsync(CancellationToken ct) => Task.FromResult<IIdentityTransaction>(new Tx());
        public Task<User?> FindContactAsync(string contact, CancellationToken ct)
            => Task.FromResult(Users.FirstOrDefault(u => !u.IsDeleted && (u.Email == contact || u.PhoneNumber == contact)));
        public Task<User?> FindUserAsync(int id, bool forUpdate, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id && !u.IsDeleted));
        public Task<OtpCode?> LatestOtpAsync(int userId, OtpPurpose purpose, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<OtpCode>> PendingOtpsAsync(int userId, OtpPurpose purpose, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<OtpCode>>(Otps.Where(o => o.UserId == userId && o.Purpose == purpose && o.ConsumedAtUtc == null).ToList());
        public Task<int> OtpFailuresAsync(int userId, DateTime since, CancellationToken ct) => Task.FromResult(0);
        public Task RevokeSessionsAsync(int userId, DateTime now, CancellationToken ct) { RevokedFor.Add(userId); return Task.CompletedTask; }
        public void Add(object entity)
        {
            if (entity is BaseEntity e && e.Id == 0) SetId(e, next++);
            switch (entity)
            {
                case User user:
                    user.RowVersion = Guid.NewGuid().ToByteArray();
                    if (user.OwnerProfile is { } profile) { SetId(profile, next++); profile.UserId = user.Id; profile.User = user; }
                    Users.Add(user);
                    break;
                case OtpCode otp: otp.User = Users.Single(u => u.Id == otp.UserId); Otps.Add(otp); break;
                case OutboxMessage m: Outbox.Add(m); break;
                case SecurityEvent s: Security.Add(s); break;
            }
        }
        public void Event(IntegrationEvent integrationEvent) { }
        /// <summary>Giống ServiceDbContext: mỗi lần lưu, user đã sửa được đóng dấu RowVersion mới.</summary>
        public Task SaveAsync(CancellationToken ct) { foreach (var u in Users) u.RowVersion = Guid.NewGuid().ToByteArray(); return Task.CompletedTask; }

        public Task<OwnerProfile?> FindByTaxCodeAsync(string taxCode, CancellationToken ct)
            => Task.FromResult(Users.Where(u => !u.IsDeleted).Select(u => u.OwnerProfile).FirstOrDefault(p => p?.TaxCode == taxCode));
        public Task<OwnerProfile?> FindProfileAsync(int ownerProfileId, CancellationToken ct)
            => Task.FromResult(Users.Select(u => u.OwnerProfile).FirstOrDefault(p => p?.Id == ownerProfileId));
        public Task<OtpCode?> FindActivationAsync(string tokenHash, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<OtpCode>> OpenActivationsAsync(int userId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<OtpCode>>(Otps.Where(o => o.UserId == userId && o.Purpose == OtpPurpose.OwnerActivation && o.ConsumedAtUtc == null).ToList());

        public Task<IReadOnlyList<UserService.Application.Features.Privacy.DataRequestItem>> ListForUserAsync(int userId, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserService.Application.Features.Privacy.AdminDataRequestItem>> ListAsync(DataRequestStatus? status, DataRequestType? type, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<DataSubjectRequest?> FindOpenAsync(int userId, DataRequestType type, CancellationToken ct) => throw new NotSupportedException();
        public Task<DataSubjectRequest?> FindAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task DeactivateStaffAssignmentsAsync(int userId, DateTime now, CancellationToken ct) => Task.CompletedTask;
        public Task<int> ActiveSessionCountAsync(int userId, DateTime now, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserService.Application.Features.Privacy.SecurityActivity>> RecentSecurityEventsAsync(int userId, int take, CancellationToken ct) => throw new NotSupportedException();

        public Task<int> CountOtherActiveAdminsAsync(int excludeUserId, DateTime nowUtc, CancellationToken ct)
            => Task.FromResult(Users.Count(u => u.Id != excludeUserId && !u.IsDeleted && u.Status == UserStatus.Active && !(u.LockedUntilUtc > nowUtc)
                                                && u.Roles.Any(r => r.Role == UserRoleType.Admin)));
        public Task<bool> HasEverSignedInAsync(int userId, CancellationToken ct) => Task.FromResult(SignedIn.Contains(userId));

        private sealed class Tx : IIdentityTransaction
        {
            public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
