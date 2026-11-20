using AdminService.Application.Features.Users;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-096: Admin đình chỉ / mở lại / xóa user – UserService thực hiện, AdminService kiểm tra nghĩa vụ liên service và ghi audit.
public class AdminUserLifecycleTests
{
    private const int AdminId = 1;
    private const int UserId = 7;

    private sealed class FakeUsers(AdminUserDto? user) : IUserServiceClient
    {
        public List<string> Calls { get; } = [];
        public bool Changed { get; set; } = true;
        public Exception? FailWith { get; set; }

        public Task<PagedResult<AdminUserSummaryDto>> ListUsersAsync(UserRoleType? role, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<AdminUserDto?> FindUserByIdAsync(int userId, CancellationToken ct) => Task.FromResult(user?.Id == userId ? user : null);
        public Task<OwnerAccountDto?> LockOwnerAsync(int ownerProfileId, string reason, DateTime? lockedUntilUtc, int performedByUserId, CancellationToken ct) => throw new NotSupportedException();
        public Task<OwnerAccountDto?> UnlockOwnerAsync(int ownerProfileId, string reason, int performedByUserId, CancellationToken ct) => throw new NotSupportedException();

        public Task<UserLifecycleResultDto> SuspendUserAsync(int userId, string reason, DateTime? lockedUntilUtc, string? rowVersion, CancellationToken ct)
            => Run($"suspend:{userId}:{reason}:{rowVersion}", "Locked");
        public Task<UserLifecycleResultDto> ReactivateUserAsync(int userId, string reason, string? rowVersion, CancellationToken ct)
            => Run($"reactivate:{userId}:{reason}", "Active");
        public Task<UserLifecycleResultDto> EraseUserAsync(int userId, string reason, string? rowVersion, CancellationToken ct)
            => Run($"erase:{userId}:{reason}", "Deleted");

        private Task<UserLifecycleResultDto> Run(string call, string status)
        {
            if (FailWith is not null) throw FailWith;
            Calls.Add(call);
            return Task.FromResult(new UserLifecycleResultDto(UserId, status, null, "v2", Changed));
        }
    }

    private sealed class FakeObligations : IUserObligationSource
    {
        public bool OpenBookings { get; set; }
        public int Unpaid { get; set; }
        public int Signed { get; set; }
        public int Sanctions { get; set; }
        public Exception? FailWith { get; set; }

        public Task<bool> HasOpenBookingsAsync(int userId, CancellationToken ct) => FailWith is null ? Task.FromResult(OpenBookings) : throw FailWith;
        public Task<int> CountUnpaidSettlementsAsync(int ownerProfileId, CancellationToken ct) => Task.FromResult(Unpaid);
        public Task<int> CountSignedContractsAsync(int ownerProfileId, CancellationToken ct) => Task.FromResult(Signed);
        public Task<int> CountActiveSanctionsAsync(int ownerProfileId, CancellationToken ct) => Task.FromResult(Sanctions);
    }

    private sealed class FakeAudit : IAdminAuditWriter
    {
        public List<AuditLog> Logs { get; } = [];
        public Task WriteAsync(AuditLog auditLog, CancellationToken ct) { Logs.Add(auditLog); return Task.CompletedTask; }
    }

    private static AdminUserDto Driver() => new(UserId, "Trần Văn Minh", "minh@example.vn", "0901234567", "Active", "Verified", ["Driver"], null, "v1");
    private static AdminUserDto Owner() => new(UserId, "Chủ bãi", "owner@example.vn", "0901234567", "Active", "Verified", ["LotOwner"],
        new AdminUserOwnerProfileDto(5, "CTY"), "v1");

    private static AdminUserLifecycleUseCase UseCase(FakeUsers users, FakeObligations? obligations = null, FakeAudit? audit = null)
        => new(users, obligations ?? new FakeObligations(), audit ?? new FakeAudit());

    [Fact]
    public async Task Suspend_calls_user_service_with_row_version_and_audits_with_reason_and_admin()
    {
        var users = new FakeUsers(Driver());
        var audit = new FakeAudit();

        var result = await UseCase(users, audit: audit).SuspendAsync(UserId, new SuspendAdminUserRequest("  Nghi gian lận ", null, "v1"), AdminId);

        Assert.Equal("Locked", result.Status);
        Assert.Equal(["suspend:7:Nghi gian lận:v1"], users.Calls);
        var log = Assert.Single(audit.Logs);
        Assert.Equal(("User.Suspended", "User", "7", AdminId, "Nghi gian lận"), (log.Action, log.EntityName, log.EntityId, log.UserId, log.Reason));
    }

    [Fact]
    public async Task Suspend_validates_input_owner_redirect_and_does_not_audit_unchanged_calls()
    {
        await Assert.ThrowsAsync<ValidationException>(() => UseCase(new FakeUsers(Driver())).SuspendAsync(UserId, new SuspendAdminUserRequest(" ", null, null), AdminId));
        await Assert.ThrowsAsync<ValidationException>(() => UseCase(new FakeUsers(Driver())).SuspendAsync(0, new SuspendAdminUserRequest("x", null, null), AdminId));
        await Assert.ThrowsAsync<NotFoundException>(() => UseCase(new FakeUsers(null)).SuspendAsync(UserId, new SuspendAdminUserRequest("x", null, null), AdminId));
        var ex = await Assert.ThrowsAsync<ConflictException>(() => UseCase(new FakeUsers(Owner())).SuspendAsync(UserId, new SuspendAdminUserRequest("x", null, null), AdminId));
        Assert.Contains("Lock owner", ex.Message);

        var users = new FakeUsers(Driver()) { Changed = false };
        var audit = new FakeAudit();
        await UseCase(users, audit: audit).SuspendAsync(UserId, new SuspendAdminUserRequest("x", null, null), AdminId);
        Assert.Empty(audit.Logs);
    }

    [Fact]
    public async Task Reactivate_calls_user_service_and_audits()
    {
        var users = new FakeUsers(Driver());
        var audit = new FakeAudit();

        await UseCase(users, audit: audit).ReactivateAsync(UserId, new ReactivateAdminUserRequest("Đã xác minh", "v1"), AdminId);

        Assert.Equal(["reactivate:7:Đã xác minh"], users.Calls);
        Assert.Equal("User.Reactivated", Assert.Single(audit.Logs).Action);
        await Assert.ThrowsAsync<ValidationException>(() => UseCase(users).ReactivateAsync(UserId, new ReactivateAdminUserRequest("", null), AdminId));
    }

    [Fact]
    public async Task Delete_requires_typed_email_then_erases_and_audits()
    {
        var users = new FakeUsers(Driver());
        var audit = new FakeAudit();
        var useCase = UseCase(users, audit: audit);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.DeleteAsync(UserId, new DeleteAdminUserRequest("Yêu cầu", "sai@example.vn", "v1"), AdminId));
        Assert.Contains("Gõ lại email", ex.Message);
        Assert.Empty(users.Calls);

        var result = await useCase.DeleteAsync(UserId, new DeleteAdminUserRequest("Yêu cầu", " MINH@example.vn ", "v1"), AdminId);

        Assert.Equal("Deleted", result.Status);
        Assert.Equal(["erase:7:Yêu cầu"], users.Calls);
        Assert.Equal("User.Deleted", Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task Delete_is_409_listing_every_blocker_and_never_calls_erase()
    {
        var users = new FakeUsers(Owner());
        var obligations = new FakeObligations { OpenBookings = true, Signed = 2, Unpaid = 1, Sanctions = 3 };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            UseCase(users, obligations).DeleteAsync(UserId, new DeleteAdminUserRequest("x", "owner@example.vn", null), AdminId));

        Assert.Contains("còn booking đang mở", ex.Message);
        Assert.Contains("2 hợp đồng bãi đã ký", ex.Message);
        Assert.Contains("1 kỳ quyết toán chưa chi trả", ex.Message);
        Assert.Contains("3 chế tài đang hiệu lực", ex.Message);
        Assert.Contains("Đình chỉ", ex.Message);
        Assert.Empty(users.Calls);
    }

    [Fact]
    public async Task Delete_is_503_and_does_nothing_when_obligations_cannot_be_checked_and_surfaces_user_service_conflicts_verbatim()
    {
        var users = new FakeUsers(Driver());
        var obligations = new FakeObligations { FailWith = new DependencyUnavailableException("Chưa kiểm tra được nghĩa vụ") };
        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            UseCase(users, obligations).DeleteAsync(UserId, new DeleteAdminUserRequest("x", "minh@example.vn", null), AdminId));
        Assert.Empty(users.Calls);

        var lastAdmin = new FakeUsers(Driver()) { FailWith = new ConflictException("Không thể xóa quản trị viên cuối cùng.") };
        var audit = new FakeAudit();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            UseCase(lastAdmin, audit: audit).DeleteAsync(UserId, new DeleteAdminUserRequest("x", "minh@example.vn", null), AdminId));
        Assert.Equal("Không thể xóa quản trị viên cuối cùng.", ex.Message);
        Assert.Empty(audit.Logs);
    }
}
