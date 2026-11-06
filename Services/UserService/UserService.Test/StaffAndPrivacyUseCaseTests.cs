using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Application.Features.Kyc;
using UserService.Application.Features.Privacy;
using UserService.Application.Features.Staff;
using UserService.Application.Features.Users;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Test;

/// <summary>US-075 (Owner quản lý Staff) và US-007 (xử lý yêu cầu xóa dữ liệu).</summary>
public class StaffAndPrivacyUseCaseTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);
    private const int OwnerProfileId = 5, OwnerUserId = 1, LotA = 100, LotB = 200;
    private readonly Store store = new();
    private readonly Lots lots = new([LotA, LotB]);
    private readonly Current current = new() { UserId = OwnerUserId, OwnerProfileId = OwnerProfileId };
    private readonly Files files = new();

    public StaffAndPrivacyUseCaseTests()
    {
        var owner = new User { FullName = "Owner", Email = "owner@example.com", Status = UserStatus.Active, PasswordHash = "hash:Password123" };
        Id(owner, OwnerUserId);
        owner.Roles.Add(new UserRole { Role = UserRoleType.LotOwner });
        owner.OwnerProfile = new OwnerProfile { BusinessName = "Lot", UserId = OwnerUserId };
        Id(owner.OwnerProfile, OwnerProfileId);
        store.Users.Add(owner);
    }

    private StaffUseCases Staff => new(store, store, lots, current, new Passwords(), new Delivery(), new Cipher(), new Clock());
    private DataRequestUseCases Privacy => new(store, store, current, new Passwords(), files, new Cipher(), new Clock());

    // ===== US-075 =====
    [Fact]
    public async Task Create_staff_makes_active_account_and_emails_temporary_password()
    {
        var item = await Staff.AssignAsync(new("Nhân viên", "staff@example.com", LotA), default);
        var user = store.Users.Single(u => u.Email == "staff@example.com");
        Assert.Equal((UserStatus.Active, true), (user.Status, user.EmailConfirmed));
        Assert.Equal(UserRoleType.Staff, Assert.Single(user.Roles).Role);
        Assert.Equal(user.Id, item.UserId);
        var assignment = Assert.Single(store.Assignments);
        Assert.Equal((OwnerProfileId, LotA, true), (assignment.OwnerProfileId, assignment.ParkingLotId, assignment.IsActive));
        Assert.Empty(store.Otps);
        Assert.Contains(store.Events, e => e is UserRegistered r && r.UserId == user.Id);
        Assert.Equal(1, store.Commits);

        // Outbox chứa mật khẩu tạm đã mã hóa; hash của tài khoản khớp đúng mật khẩu đó.
        var message = Assert.Single(store.Outbox, m => m.EventType == StaffUseCases.CredentialsEvent);
        using var payload = System.Text.Json.JsonDocument.Parse(message.PayloadJson);
        var encrypted = payload.RootElement.GetProperty("EncryptedPassword").GetString()!;
        var password = new Cipher().Decrypt(encrypted, StaffUseCases.CredentialsCipherPurpose);
        Assert.DoesNotContain(password, message.PayloadJson.Replace(encrypted, ""));
        Assert.True(new Passwords().Verify(password, user.PasswordHash));
        Assert.Equal("staff@example.com", payload.RootElement.GetProperty("Destination").GetString());
    }

    [Fact]
    public async Task New_staff_needs_email_to_receive_credentials()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Staff.AssignAsync(new("Nhân viên", "0901234567", LotA), default));
        Assert.Empty(store.Assignments);
    }

    [Fact]
    public void Temporary_password_meets_password_rules_and_avoids_ambiguous_characters()
    {
        for (var i = 0; i < 200; i++)
        {
            var password = StaffUseCases.TemporaryPassword();
            Assert.Equal(10, password.Length);
            Assert.Contains(password, char.IsLetter);
            Assert.Contains(password, char.IsDigit);
            Assert.DoesNotContain(password, c => "0O1lI".Contains(c));
        }
    }

    [Fact]
    public async Task Same_contact_adds_second_lot_without_new_account_or_otp()
    {
        var first = await Staff.AssignAsync(new("Nhân viên", "staff@example.com", LotA), default);
        var otps = store.Otps.Count;
        var second = await Staff.AssignAsync(new("Tên khác", "STAFF@example.com", LotB), default);
        Assert.Equal(first.UserId, second.UserId);
        Assert.Equal(1, store.Users.Count(u => u.Email == "staff@example.com"));
        Assert.Equal(2, store.Assignments.Count(a => a.IsActive && a.StaffUserId == first.UserId));
        Assert.Equal(otps, store.Otps.Count);
        await Assert.ThrowsAsync<ConflictException>(() => Staff.AssignAsync(new("Nhân viên", "staff@example.com", LotB), default));
    }

    [Fact]
    public async Task Lot_outside_owner_directory_is_forbidden_and_writes_nothing()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => Staff.AssignAsync(new("Nhân viên", "staff@example.com", 999), default));
        Assert.Empty(store.Assignments);
        Assert.Equal(0, store.Commits);
    }

    [Fact]
    public async Task Cannot_take_over_driver_account_or_other_tenants_staff()
    {
        var driver = new User { FullName = "Driver", Email = "driver@example.com", Status = UserStatus.Active };
        driver.Roles.Add(new UserRole { Role = UserRoleType.Driver });
        store.Add(driver);
        await Assert.ThrowsAsync<ConflictException>(() => Staff.AssignAsync(new("X", "driver@example.com", LotA), default));

        var foreign = new User { FullName = "Foreign", Email = "foreign@example.com", Status = UserStatus.Active };
        foreign.Roles.Add(new UserRole { Role = UserRoleType.Staff });
        store.Add(foreign);
        store.Add(new StaffAssignment { StaffUserId = foreign.Id, OwnerProfileId = 77, ParkingLotId = 7, IsActive = false });
        await Assert.ThrowsAsync<ConflictException>(() => Staff.AssignAsync(new("X", "foreign@example.com", LotA), default));
        Assert.DoesNotContain(store.Assignments, a => a.OwnerProfileId == OwnerProfileId);
    }

    [Fact]
    public async Task Locked_owner_profile_cannot_assign_staff()
    {
        store.Users[0].OwnerProfile!.IsLocked = true;
        await Assert.ThrowsAsync<ForbiddenException>(() => Staff.AssignAsync(new("Nhân viên", "staff@example.com", LotA), default));
        Assert.Empty(store.Assignments);
    }

    [Fact]
    public async Task Non_owner_is_forbidden()
    {
        current.OwnerProfileId = null;
        await Assert.ThrowsAsync<ForbiddenException>(() => Staff.ListAsync(1, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Staff.RevokeAsync(1, default));
    }

    [Fact]
    public async Task Revoke_is_tenant_scoped_idempotent_and_revokes_sessions()
    {
        var item = await Staff.AssignAsync(new("Nhân viên", "staff@example.com", LotA), default);
        current.OwnerProfileId = 77;
        await Assert.ThrowsAsync<NotFoundException>(() => Staff.RevokeAsync(item.Id, default));
        current.OwnerProfileId = OwnerProfileId;

        await Staff.RevokeAsync(item.Id, default);
        var assignment = store.Assignments.Single(a => a.Id == item.Id);
        Assert.False(assignment.IsActive);
        Assert.Equal(Now, assignment.RevokedAtUtc);
        Assert.Contains(item.UserId, store.RevokedSessions);

        store.RevokedSessions.Clear();
        await Staff.RevokeAsync(item.Id, default);
        Assert.Empty(store.RevokedSessions);
        // Sau khi gỡ, có thể phân công lại cùng bãi.
        await Staff.AssignAsync(new("Nhân viên", "staff@example.com", LotA), default);
        Assert.Single(store.Assignments, a => a.IsActive);
    }

    [Fact]
    public async Task Lot_directory_outage_is_reported_as_dependency_error()
    {
        lots.Down = true;
        await Assert.ThrowsAsync<DependencyUnavailableException>(() => Staff.AssignAsync(new("Nhân viên", "staff@example.com", LotA), default));
    }

    // ===== US-007 =====
    [Fact]
    public async Task Repeated_submission_returns_the_open_request()
    {
        var driver = Driver();
        var first = await Privacy.SubmitAsync(new(DataRequestType.Deletion, "Password123"), default);
        var second = await Privacy.SubmitAsync(new(DataRequestType.Deletion, "Password123"), default);
        Assert.Equal(first.Id, second.Id);
        var request = Assert.Single(store.Requests);
        Assert.Equal((driver.Id, DataRequestStatus.Submitted, Now.AddHours(72)), (request.UserId, request.Status, request.DueAtUtc));
        await Assert.ThrowsAsync<ValidationException>(() => Privacy.SubmitAsync(new(DataRequestType.Correction, "Password123"), default));
    }

    [Fact]
    public async Task Five_wrong_reauth_passwords_lock_the_account_and_persist()
    {
        var driver = Driver();
        for (var i = 0; i < 4; i++)
            await Assert.ThrowsAsync<ForbiddenException>(() => Privacy.SubmitAsync(new(DataRequestType.Export, "Wrong123"), default));
        Assert.Equal(4, driver.FailedLoginCount);
        Assert.Null(driver.LockedUntilUtc);
        await Assert.ThrowsAsync<ForbiddenException>(() => Privacy.SubmitAsync(new(DataRequestType.Export, "Wrong123"), default));
        Assert.Equal(Now.AddMinutes(15), driver.LockedUntilUtc);
        Assert.Contains(driver.Id, store.RevokedSessions);
        Assert.Contains(store.Events, e => e is UserLocked l && l.UserId == driver.Id);
        Assert.Equal(5, store.Commits); // bộ đếm được commit dù trả lỗi
        Assert.Empty(store.Requests);
        // Đang khóa thì mật khẩu đúng cũng bị từ chối.
        await Assert.ThrowsAsync<ForbiddenException>(() => Privacy.SubmitAsync(new(DataRequestType.Export, "Password123"), default));
    }

    [Fact]
    public async Task Successful_reauth_resets_failure_counter()
    {
        var driver = Driver();
        await Assert.ThrowsAsync<ForbiddenException>(() => Privacy.SubmitAsync(new(DataRequestType.Export, "Wrong123"), default));
        await Privacy.SubmitAsync(new(DataRequestType.Export, "Password123"), default);
        Assert.Equal(0, driver.FailedLoginCount);
    }

    [Fact]
    public async Task Export_only_returns_own_profile_and_completes_request()
    {
        var driver = Driver();
        var item = await Privacy.SubmitAsync(new(DataRequestType.Export, "Password123"), default);
        var json = System.Text.Encoding.UTF8.GetString(await Privacy.ExportAsync(item.Id, new("Password123"), default));
        Assert.Contains("driver@example.com", json);
        Assert.DoesNotContain("hash:", json);
        Assert.Equal(DataRequestStatus.Completed, store.Requests.Single().Status);

        current.UserId = OwnerUserId;
        await Assert.ThrowsAsync<NotFoundException>(() => Privacy.ExportAsync(item.Id, new("Password123"), default));
    }

    [Fact]
    public async Task Admin_approval_anonymizes_user_and_cleans_up()
    {
        var driver = Driver();
        driver.PhoneNumber = "0901234567";
        driver.AvatarUrl = "/api/v1/users/avatars/x.png";
        driver.KycNumberEncrypted = "secret";
        driver.KycStatus = KycStatus.Verified;
        store.Add(new StaffAssignment { StaffUserId = driver.Id, OwnerProfileId = OwnerProfileId, ParkingLotId = LotA });
        store.Add(new OtpCode { UserId = driver.Id, Purpose = OtpPurpose.ResetPassword, ExpiresAtUtc = Now.AddMinutes(5) });
        var item = await Privacy.SubmitAsync(new(DataRequestType.Deletion, "Password123"), default);

        current.UserId = 99;
        var result = await Privacy.ApproveDeletionAsync(item.Id, new(" Đã xác minh "), default);

        Assert.Equal("Completed", result.Status);
        Assert.Equal(99, result.HandledByUserId);
        Assert.Equal("Đã xác minh", result.ResultNote);
        Assert.True(driver.IsDeleted);
        Assert.Equal(Now, driver.DeletedAtUtc);
        Assert.Equal(DataRequestUseCases.ErasedName, driver.FullName);
        Assert.Equal($"deleted-{driver.Id}@deleted.invalid", driver.Email);
        Assert.Null(driver.PhoneNumber);
        Assert.Null(driver.AvatarUrl);
        Assert.Null(driver.KycNumberEncrypted);
        Assert.Equal(KycStatus.NotSubmitted, driver.KycStatus);
        Assert.NotEqual("hash:Password123", driver.PasswordHash);
        Assert.Contains(driver.Id, store.RevokedSessions);
        Assert.Contains(driver.Id, store.DeactivatedStaff);
        Assert.All(store.Otps.Where(o => o.UserId == driver.Id), o => Assert.Equal(Now, o.ConsumedAtUtc));
        Assert.Contains("/api/v1/users/avatars/x.png", files.Deleted);

        await Assert.ThrowsAsync<ConflictException>(() => Privacy.ApproveDeletionAsync(item.Id, new(null), default));
        await Assert.ThrowsAsync<ConflictException>(() => Privacy.RejectAsync(item.Id, new("Muộn"), default));
    }

    [Fact]
    public async Task Active_owner_and_admin_cannot_be_anonymized()
    {
        current.UserId = OwnerUserId; current.OwnerProfileId = OwnerProfileId;
        var ownerRequest = await Privacy.SubmitAsync(new(DataRequestType.Deletion, "Password123"), default);
        current.UserId = 99;
        await Assert.ThrowsAsync<ConflictException>(() => Privacy.ApproveDeletionAsync(ownerRequest.Id, new(null), default));
        Assert.False(store.Users[0].IsDeleted);

        var admin = Driver();
        admin.Roles.Add(new UserRole { Role = UserRoleType.Admin });
        var adminRequest = await Privacy.SubmitAsync(new(DataRequestType.Deletion, "Password123"), default);
        current.UserId = 99;
        await Assert.ThrowsAsync<ConflictException>(() => Privacy.ApproveDeletionAsync(adminRequest.Id, new(null), default));
        Assert.False(admin.IsDeleted);
    }

    [Fact]
    public async Task Admin_cannot_approve_export_or_own_request_and_reject_needs_reason()
    {
        var driver = Driver();
        var export = await Privacy.SubmitAsync(new(DataRequestType.Export, "Password123"), default);
        var deletion = await Privacy.SubmitAsync(new(DataRequestType.Deletion, "Password123"), default);
        await Assert.ThrowsAsync<ConflictException>(() => Privacy.ApproveDeletionAsync(deletion.Id, new(null), default));
        current.UserId = 99;
        await Assert.ThrowsAsync<ValidationException>(() => Privacy.ApproveDeletionAsync(export.Id, new(null), default));
        await Assert.ThrowsAsync<ValidationException>(() => Privacy.RejectAsync(deletion.Id, new("  "), default));

        var rejected = await Privacy.RejectAsync(deletion.Id, new("Còn nợ phí gửi xe"), default);
        Assert.Equal("Rejected", rejected.Status);
        Assert.False(driver.IsDeleted);
    }

    [Fact]
    public async Task Correction_request_requires_reason_and_admin_completes_it()
    {
        Driver();
        var item = await Privacy.SubmitAsync(new(DataRequestType.Correction, "Password123", "  Sai ngày sinh, đúng là 15/08/2001 "), default);
        Assert.Equal("Sai ngày sinh, đúng là 15/08/2001", item.Reason);
        current.UserId = 99;
        await Assert.ThrowsAsync<ValidationException>(() => Privacy.CompleteCorrectionAsync(item.Id, new(" "), default));
        var done = await Privacy.CompleteCorrectionAsync(item.Id, new("Đã cập nhật"), default);
        Assert.Equal(("Completed", "Đã cập nhật"), (done.Status, done.ResultNote));
        await Assert.ThrowsAsync<ConflictException>(() => Privacy.CompleteCorrectionAsync(item.Id, new("Lần hai"), default));
    }

    [Fact]
    public async Task Personal_data_view_and_export_mask_document_number()
    {
        var driver = Driver();
        driver.KycStatus = KycStatus.Pending;
        driver.KycDocumentType = KycDocumentType.CitizenId;
        driver.KycNumberEncrypted = new Cipher().Encrypt("079201008921", KycUseCases.CipherPurpose);
        var view = await Privacy.ViewMineAsync(default);
        Assert.Equal("••••••••8921", view.KycMaskedNumber);
        Assert.Equal(2, view.ActiveSessions);
        Assert.Single(view.RecentSecurityEvents);

        var item = await Privacy.SubmitAsync(new(DataRequestType.Export, "Password123"), default);
        var json = System.Text.Encoding.UTF8.GetString(await Privacy.ExportAsync(item.Id, new("Password123"), default));
        Assert.DoesNotContain("079201008921", json);
        Assert.Contains("8921", json);
    }

    private User Driver()
    {
        var user = new User { FullName = "Driver", Email = "driver@example.com", Status = UserStatus.Active, PasswordHash = "hash:Password123" };
        user.Roles.Add(new UserRole { Role = UserRoleType.Driver });
        store.Add(user);
        current.UserId = user.Id; current.OwnerProfileId = null;
        return user;
    }

    private static void Id(BaseEntity entity, int id) => typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(entity, id);
    private static void Created(BaseEntity entity) => typeof(BaseEntity).GetProperty(nameof(BaseEntity.CreatedAtUtc))!.SetValue(entity, Now);

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
    private sealed class Lots(int[] ids) : IOwnerLotDirectory
    {
        public bool Down;
        public Task<IReadOnlyList<OwnerLot>> GetLotsAsync(int ownerProfileId, CancellationToken ct)
            => Down ? throw new DependencyUnavailableException("down")
                : Task.FromResult<IReadOnlyList<OwnerLot>>(ownerProfileId == OwnerProfileId ? ids.Select(id => new OwnerLot(id, null)).ToList() : []);
    }
    private sealed class Files : IAvatarStorage
    {
        public List<string?> Deleted = [];
        public Task<string> SaveAsync(Stream image, CancellationToken ct) => Task.FromResult("new");
        public Task DeleteAsync(string? url, CancellationToken ct) { Deleted.Add(url); return Task.CompletedTask; }
        public Task<byte[]?> ReadAsync(Guid id, CancellationToken ct) => Task.FromResult<byte[]?>(null);
    }
    private sealed class Current : ICurrentUser
    {
        public int? UserId { get; set; }
        public string? Email => null;
        public IReadOnlyList<string> Roles => [];
        public int? OwnerProfileId { get; set; }
        public bool IsAuthenticated => UserId is not null;
    }

    /// <summary>Bộ nhớ giả lập cho cả ba port; thay đổi entity được giữ nguyên tham chiếu như EF tracking.</summary>
    private sealed class Store : IIdentityStore, IStaffStore, IDataRequestStore
    {
        public List<User> Users = [];
        public List<StaffAssignment> Assignments = [];
        public List<OtpCode> Otps = [];
        public List<OutboxMessage> Outbox = [];
        public List<DataSubjectRequest> Requests = [];
        public List<IntegrationEvent> Events = [];
        public List<int> RevokedSessions = [];
        public List<int> DeactivatedStaff = [];
        public int Commits;
        private int next = 10;

        public Task<IIdentityTransaction> BeginAsync(CancellationToken ct) => Task.FromResult<IIdentityTransaction>(new Tx(this));
        public Task<User?> FindContactAsync(string contact, CancellationToken ct)
            => Task.FromResult(Users.FirstOrDefault(u => !u.IsDeleted && (u.Email == contact || u.PhoneNumber == contact)));
        public Task<User?> FindUserAsync(int id, bool forUpdate, CancellationToken ct)
            => Task.FromResult(Users.FirstOrDefault(u => u.Id == id && !u.IsDeleted));
        public Task<OtpCode?> LatestOtpAsync(int userId, OtpPurpose purpose, CancellationToken ct)
            => Task.FromResult(Otps.LastOrDefault(o => o.UserId == userId && o.Purpose == purpose));
        public Task<IReadOnlyList<OtpCode>> PendingOtpsAsync(int userId, OtpPurpose purpose, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<OtpCode>>(Otps.Where(o => o.UserId == userId && o.Purpose == purpose && o.ConsumedAtUtc == null).ToList());
        public Task<int> OtpFailuresAsync(int userId, DateTime since, CancellationToken ct) => Task.FromResult(0);
        public Task RevokeSessionsAsync(int userId, DateTime now, CancellationToken ct) { RevokedSessions.Add(userId); return Task.CompletedTask; }
        public void Add(object entity)
        {
            if (entity is BaseEntity e && e.Id == 0) { Id(e, next++); Created(e); }
            switch (entity)
            {
                case User user: Users.Add(user); break;
                case StaffAssignment a:
                    if (a.StaffUser is { } staffUser)
                    {
                        if (staffUser.Id == 0) { Id(staffUser, next++); Users.Add(staffUser); }
                        a.StaffUserId = staffUser.Id;
                    }
                    Assignments.Add(a);
                    break;
                case OtpCode otp: Otps.Add(otp); break;
                case OutboxMessage m: Outbox.Add(m); break;
                case DataSubjectRequest r: Requests.Add(r); break;
            }
        }
        public void Event(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        // IStaffStore
        public Task<IReadOnlyList<StaffItem>> ListActiveAsync(int ownerProfileId, int page, int pageSize, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<StaffItem>>(Assignments.Where(a => a.OwnerProfileId == ownerProfileId && a.IsActive)
                .Select(a => new StaffItem(a.Id, a.StaffUserId, Users.Single(u => u.Id == a.StaffUserId).FullName, a.ParkingLotId, "Active")).ToList());
        public Task<IReadOnlyList<StaffAssignment>> AssignmentsOfUserAsync(int staffUserId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<StaffAssignment>>(Assignments.Where(a => a.StaffUserId == staffUserId).ToList());
        public Task<StaffAssignment?> FindAssignmentAsync(int id, int ownerProfileId, CancellationToken ct)
            => Task.FromResult(Assignments.FirstOrDefault(a => a.Id == id && a.OwnerProfileId == ownerProfileId));

        // IDataRequestStore
        public Task<IReadOnlyList<DataRequestItem>> ListForUserAsync(int userId, int page, int pageSize, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DataRequestItem>>(Requests.Where(r => r.UserId == userId)
                .Select(r => new DataRequestItem(r.Id, r.RequestType.ToString(), r.Status.ToString(), r.CreatedAtUtc, r.DueAtUtc)).ToList());
        public Task<IReadOnlyList<AdminDataRequestItem>> ListAsync(DataRequestStatus? status, DataRequestType? type, int page, int pageSize, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AdminDataRequestItem>>([]);
        public Task<DataSubjectRequest?> FindOpenAsync(int userId, DataRequestType type, CancellationToken ct)
            => Task.FromResult(Requests.FirstOrDefault(r => r.UserId == userId && r.RequestType == type
                && r.Status is DataRequestStatus.Submitted or DataRequestStatus.InProgress));
        public Task<DataSubjectRequest?> FindAsync(int id, CancellationToken ct) => Task.FromResult(Requests.FirstOrDefault(r => r.Id == id));
        public Task DeactivateStaffAssignmentsAsync(int userId, DateTime now, CancellationToken ct)
        {
            DeactivatedStaff.Add(userId);
            foreach (var a in Assignments.Where(a => a.StaffUserId == userId && a.IsActive)) { a.IsActive = false; a.RevokedAtUtc = now; }
            return Task.CompletedTask;
        }

        public Task<int> ActiveSessionCountAsync(int userId, DateTime now, CancellationToken ct) => Task.FromResult(2);
        public Task<IReadOnlyList<SecurityActivity>> RecentSecurityEventsAsync(int userId, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SecurityActivity>>([new("LoginSucceeded", Now, "127.0.0.1")]);

        private sealed class Tx(Store store) : IIdentityTransaction
        {
            public Task CommitAsync(CancellationToken ct) { store.Commits++; return Task.CompletedTask; }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
