using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.History;
using UserService.Application.Features.Identity;
using UserService.Application.Features.Kyc;
using UserService.Domain.Entities;

namespace UserService.Test;

/// <summary>US-005 (KYC CCCD/GPLX) và US-082 (lịch sử gửi xe & giao dịch).</summary>
public class KycAndHistoryUseCaseTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);
    private const int DriverId = 7, AdminId = 99;
    private readonly Store store = new();
    private readonly Files files = new();
    private readonly Current current = new() { UserId = DriverId };
    private readonly User driver;

    public KycAndHistoryUseCaseTests()
    {
        driver = new User { FullName = "Tài xế", Email = "driver@example.com", Status = UserStatus.Active };
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(driver, DriverId);
        store.Users.Add(driver);
    }

    private KycUseCases Kyc => new(store, store, files, new Cipher(), current, new Clock());
    private static SubmitKycCommand Command(KycDocumentType type = KycDocumentType.CitizenId, string number = "0792 0100 8921")
        => new(type, number, new KycImageUpload(new MemoryStream([1]), 1), new KycImageUpload(new MemoryStream([2]), 1));

    // ===== US-005 =====
    [Theory]
    [InlineData("07920100892")]
    [InlineData("0792010089211")]
    [InlineData("07920100892a")]
    [InlineData("")]
    public async Task Document_number_must_be_twelve_digits(string number)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Kyc.SubmitAsync(Command(number: number), default));
        Assert.Empty(files.Saved);
    }

    [Fact]
    public async Task Submit_encrypts_number_stores_images_and_masks_on_read()
    {
        var result = await Kyc.SubmitAsync(Command(), default);
        Assert.Equal(KycStatus.Pending, driver.KycStatus);
        Assert.Equal(KycDocumentType.CitizenId, driver.KycDocumentType);
        Assert.DoesNotContain("079201008921", driver.KycNumberEncrypted);
        Assert.Equal("079201008921", new Cipher().Decrypt(driver.KycNumberEncrypted!, KycUseCases.CipherPurpose));
        Assert.Equal(2, files.Saved.Count);
        Assert.Equal(("Pending", "••••••••8921", true, true), (result.Status, result.MaskedNumber, result.HasFrontImage, result.HasBackImage));
        Assert.Contains(store.Events, e => e.EventType == SecurityEventType.KycSubmitted);
        Assert.Equal(1, store.Commits);
    }

    [Fact]
    public async Task Citizen_id_number_alone_submits_encrypted_without_images()
    {
        var result = await Kyc.SubmitCitizenIdAsync("0792 0100 8921", default);
        Assert.Equal(KycStatus.Pending, driver.KycStatus);
        Assert.Equal(KycDocumentType.CitizenId, driver.KycDocumentType);
        Assert.Equal("079201008921", new Cipher().Decrypt(driver.KycNumberEncrypted!, KycUseCases.CipherPurpose));
        Assert.Empty(files.Saved);
        Assert.Equal(("Pending", "••••••••8921", false, false), (result.Status, result.MaskedNumber, result.HasFrontImage, result.HasBackImage));
        Assert.Contains(store.Events, e => e.EventType == SecurityEventType.KycSubmitted);
        await Assert.ThrowsAsync<ConflictException>(() => Kyc.SubmitCitizenIdAsync("079201008921", default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("07920100892")]
    [InlineData("07920100892A")]
    public async Task Citizen_id_number_must_be_twelve_digits(string number)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Kyc.SubmitCitizenIdAsync(number, default));
        Assert.Equal(KycStatus.NotSubmitted, driver.KycStatus);
    }

    [Fact]
    public async Task Pending_or_verified_cannot_resubmit()
    {
        await Kyc.SubmitAsync(Command(), default);
        await Assert.ThrowsAsync<ConflictException>(() => Kyc.SubmitAsync(Command(KycDocumentType.DriverLicense), default));
        driver.KycStatus = KycStatus.Verified;
        await Assert.ThrowsAsync<ConflictException>(() => Kyc.SubmitAsync(Command(), default));
        Assert.Equal(2, files.Saved.Count);
    }

    [Fact]
    public async Task Failed_save_deletes_new_images()
    {
        store.FailSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Kyc.SubmitAsync(Command(), default));
        Assert.Equal(files.Saved, files.Deleted);
    }

    [Fact]
    public async Task Admin_rejects_with_reason_which_erases_document_then_user_resubmits()
    {
        await Kyc.SubmitAsync(Command(), default);
        var oldImages = files.Saved.ToList();
        current.UserId = AdminId;
        await Assert.ThrowsAsync<ValidationException>(() => Kyc.RejectAsync(DriverId, new(" "), default));
        await Kyc.RejectAsync(DriverId, new("Ảnh mặt sau bị lóa"), default);
        Assert.Equal(KycStatus.Rejected, driver.KycStatus);
        Assert.Null(driver.KycNumberEncrypted);
        Assert.Null(driver.KycFrontImageUrl);
        Assert.Equal(oldImages, files.Deleted);

        current.UserId = DriverId;
        var status = await Kyc.GetMineAsync(default);
        Assert.Equal(("Rejected", "Ảnh mặt sau bị lóa"), (status.Status, status.RejectionReason));
        await Kyc.SubmitAsync(Command(KycDocumentType.DriverLicense, "012345678901"), default);
        Assert.Equal(KycStatus.Pending, driver.KycStatus);
    }

    [Fact]
    public async Task Admin_approves_but_not_own_profile_and_only_pending()
    {
        current.UserId = AdminId;
        await Assert.ThrowsAsync<ConflictException>(() => Kyc.ApproveAsync(DriverId, default));
        current.UserId = DriverId;
        await Kyc.SubmitAsync(Command(), default);
        await Assert.ThrowsAsync<ForbiddenException>(() => Kyc.ApproveAsync(DriverId, default));
        current.UserId = AdminId;
        await Kyc.ApproveAsync(DriverId, default);
        Assert.Equal((KycStatus.Verified, (DateTime?)Now), (driver.KycStatus, driver.KycVerifiedAtUtc));
    }

    [Fact]
    public async Task Missing_image_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Kyc.ReadMyImageAsync(KycImageSide.Front, default));
    }

    // ===== US-082 =====
    private static BookingRecord Booking(int id, string status, int day, decimal amount)
        => new(id, $"BK-{id:0000}", status, "Bãi A", "A-01", "51K-889.32", Now.AddDays(-day), Now.AddDays(-day).AddHours(2), amount);

    [Fact]
    public async Task History_uses_token_user_filters_sorts_and_summarizes()
    {
        var source = new Activity
        {
            Bookings = [Booking(1, "Completed", 5, 50_000), Booking(2, "Cancelled", 1, 0), Booking(3, "CheckedOut", 2, 30_000), Booking(4, "Confirmed", -1, 20_000)]
        };
        var history = new ParkingHistoryUseCases(source, current);
        var page = await history.ListMineAsync("completed", 1, default);
        Assert.Equal(DriverId, source.RequestedUserId);
        Assert.Equal([3, 1], page.Items.Select(i => i.BookingId));
        Assert.Equal((4, 2, 80_000m), (page.Summary.TotalBookings, page.Summary.CompletedBookings, page.Summary.TotalSpent));
        Assert.True(page.PaymentsAvailable);
        Assert.Single(page.Items[0].Payments);

        var all = await history.ListMineAsync(null, 1, default);
        Assert.Equal([4, 2, 3, 1], all.Items.Select(i => i.BookingId));
        await Assert.ThrowsAsync<ValidationException>(() => history.ListMineAsync("everything", 1, default));
    }

    [Fact]
    public async Task Payment_outage_still_returns_bookings()
    {
        var source = new Activity { Bookings = [Booking(1, "Completed", 1, 10_000)], PaymentsDown = true };
        var page = await new ParkingHistoryUseCases(source, current).ListMineAsync(null, 1, default);
        Assert.False(page.PaymentsAvailable);
        Assert.Empty(Assert.Single(page.Items).Payments);
    }

    [Fact]
    public async Task Anonymous_user_cannot_read_history()
    {
        current.UserId = null;
        await Assert.ThrowsAsync<AuthenticationException>(() => new ParkingHistoryUseCases(new Activity(), current).ListMineAsync(null, 1, default));
    }

    // ===== Fakes =====
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed class Cipher : ISecretCipher
    {
        public string Encrypt(string value, string purpose) => purpose + "|" + new string(value.Reverse().ToArray());
        public string Decrypt(string value, string purpose) => new(value[(purpose.Length + 1)..].Reverse().ToArray());
    }
    private sealed class Current : ICurrentUser
    {
        public int? UserId { get; set; }
        public string? Email => null;
        public IReadOnlyList<string> Roles => [];
        public int? OwnerProfileId => null;
        public bool IsAuthenticated => UserId is not null;
    }
    private sealed class Files : IKycDocumentStorage
    {
        public List<string> Saved = [];
        public List<string> Deleted = [];
        public Task<string> SaveAsync(Stream image, CancellationToken ct) { var key = $"kyc:{Saved.Count}"; Saved.Add(key); return Task.FromResult(key); }
        public Task<byte[]?> ReadAsync(string? key, CancellationToken ct) => Task.FromResult<byte[]?>(null);
        public Task DeleteAsync(string? key, CancellationToken ct) { if (key is not null) Deleted.Add(key); return Task.CompletedTask; }
    }
    private sealed class Activity : IDriverActivitySource
    {
        public List<BookingRecord> Bookings = [];
        public bool PaymentsDown;
        public int? RequestedUserId;
        public Task<IReadOnlyList<BookingRecord>> BookingsAsync(int userId, CancellationToken ct)
        { RequestedUserId = userId; return Task.FromResult<IReadOnlyList<BookingRecord>>(Bookings); }
        public Task<IReadOnlyList<PaymentRecord>> PaymentsAsync(int bookingId, CancellationToken ct)
            => PaymentsDown ? throw new DependencyUnavailableException("down")
                : Task.FromResult<IReadOnlyList<PaymentRecord>>([new($"PM-{bookingId}", "Vnpay", "Succeeded", 10_000, 0, Now, null)]);
    }
    private sealed class Store : IIdentityStore, IKycStore
    {
        public List<User> Users = [];
        public List<SecurityEvent> Events = [];
        public int Commits;
        public bool FailSave;
        public Task<IIdentityTransaction> BeginAsync(CancellationToken ct) => Task.FromResult<IIdentityTransaction>(new Tx(this));
        public Task<User?> FindContactAsync(string contact, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<User?> FindUserAsync(int id, bool forUpdate, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        public Task<OtpCode?> LatestOtpAsync(int userId, OtpPurpose purpose, CancellationToken ct) => Task.FromResult<OtpCode?>(null);
        public Task<IReadOnlyList<OtpCode>> PendingOtpsAsync(int userId, OtpPurpose purpose, CancellationToken ct) => Task.FromResult<IReadOnlyList<OtpCode>>([]);
        public Task<int> OtpFailuresAsync(int userId, DateTime since, CancellationToken ct) => Task.FromResult(0);
        public Task RevokeSessionsAsync(int userId, DateTime now, CancellationToken ct) => Task.CompletedTask;
        public void Add(object entity)
        {
            if (entity is not SecurityEvent e) return;
            typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(e, Events.Count + 1);
            typeof(BaseEntity).GetProperty(nameof(BaseEntity.CreatedAtUtc))!.SetValue(e, Now.AddSeconds(Events.Count));
            Events.Add(e);
        }
        public void Event(IntegrationEvent integrationEvent) { }
        public Task SaveAsync(CancellationToken ct) => FailSave ? throw new InvalidOperationException("db") : Task.CompletedTask;
        public Task<IReadOnlyList<KycQueueRow>> ListPendingAsync(int page, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<KycQueueRow>>([]);
        public Task<SecurityEvent?> LatestKycEventAsync(int userId, CancellationToken ct)
            => Task.FromResult(Events.Where(e => e.UserId == userId).OrderByDescending(e => e.CreatedAtUtc).FirstOrDefault());
        private sealed class Tx(Store store) : IIdentityTransaction
        {
            public Task CommitAsync(CancellationToken ct) { store.Commits++; return Task.CompletedTask; }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
