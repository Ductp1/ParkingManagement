using System.Text.Json;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Users;

// ===== REQUEST (admin lấy từ JWT, không nằm trong body) =====

/// <summary>LockedUntilUtc = null là đình chỉ vô thời hạn. RowVersion lấy từ chi tiết user.</summary>
public sealed record SuspendAdminUserRequest(string? Reason, DateTime? LockedUntilUtc, string? RowVersion);
public sealed record ReactivateAdminUserRequest(string? Reason, string? RowVersion);
/// <summary>ConfirmEmail = email của user gõ lại để xác nhận (so với email hiện tại trong UserService).</summary>
public sealed record DeleteAdminUserRequest(string? Reason, string? ConfirmEmail, string? RowVersion);

// ===== PORT =====

/// <summary>
/// Các nghĩa vụ còn lại của một user – chặn xóa. Booking (BookingService) và settlement (PaymentService) đọc qua Gateway bằng JWT Admin
/// đang thao tác; hợp đồng Signed và chế tài hiệu lực nằm trong pm_admin. Không đọc được → DependencyUnavailableException (503, không xóa).
/// </summary>
public interface IUserObligationSource
{
    /// <summary>Có booking đang mở (PendingPayment, PendingOwnerApproval, Confirmed, CheckedIn, Parking) của tài xế này không.</summary>
    Task<bool> HasOpenBookingsAsync(int userId, CancellationToken cancellationToken);
    /// <summary>Số kỳ quyết toán chưa Paid (Draft, PendingReview, Approved) của chủ bãi.</summary>
    Task<int> CountUnpaidSettlementsAsync(int ownerProfileId, CancellationToken cancellationToken);
    Task<int> CountSignedContractsAsync(int ownerProfileId, CancellationToken cancellationToken);
    Task<int> CountActiveSanctionsAsync(int ownerProfileId, CancellationToken cancellationToken);
}

/// <summary>Ghi AuditLog độc lập (thao tác chính chạy ở UserService nên không chung transaction với pm_admin).</summary>
public interface IAdminAuditWriter
{
    Task WriteAsync(AuditLog auditLog, CancellationToken cancellationToken);
}

// ===== USE CASE =====

public interface IAdminUserLifecycleUseCase
{
    Task<UserLifecycleResultDto> SuspendAsync(int userId, SuspendAdminUserRequest request, int performedByUserId, CancellationToken cancellationToken = default);
    Task<UserLifecycleResultDto> ReactivateAsync(int userId, ReactivateAdminUserRequest request, int performedByUserId, CancellationToken cancellationToken = default);
    Task<UserLifecycleResultDto> DeleteAsync(int userId, DeleteAdminUserRequest request, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-096 – Admin đình chỉ / mở lại / xóa (ẩn danh hóa) user. UserService thực hiện và kiểm tra (tự khóa mình, Admin cuối, RowVersion);
/// AdminService kiểm tra nghĩa vụ liên service trước khi xóa, rồi ghi AuditLog. Admin thực hiện lấy từ JWT.
/// Chủ bãi: đình chỉ dùng luồng khóa owner US-096 (Sanction) ở trang chi tiết, không làm song song ở đây.
/// </summary>
public sealed class AdminUserLifecycleUseCase(IUserServiceClient users, IUserObligationSource obligations, IAdminAuditWriter audit)
    : IAdminUserLifecycleUseCase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<UserLifecycleResultDto> SuspendAsync(int userId, SuspendAdminUserRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        Ensure(userId, performedByUserId);
        var reason = Reason(request?.Reason);
        var user = await Load(userId, cancellationToken);
        if (user.Roles.Contains("LotOwner") && user.OwnerProfile is not null)
            throw new ConflictException("Chủ bãi được khóa bằng \"Lock owner\" (chế tài US-096) để bãi và hợp đồng được xử lý đúng.");
        var result = await users.SuspendUserAsync(userId, reason, request!.LockedUntilUtc, request.RowVersion, cancellationToken);
        if (result.Changed)
            await audit.WriteAsync(Build(userId, performedByUserId, "User.Suspended", new { status = user.Status },
                new { status = result.Status, lockedUntilUtc = result.LockedUntilUtc }, reason), cancellationToken);
        return result;
    }

    public async Task<UserLifecycleResultDto> ReactivateAsync(int userId, ReactivateAdminUserRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        Ensure(userId, performedByUserId);
        var reason = Reason(request?.Reason);
        var user = await Load(userId, cancellationToken);
        var result = await users.ReactivateUserAsync(userId, reason, request!.RowVersion, cancellationToken);
        if (result.Changed)
            await audit.WriteAsync(Build(userId, performedByUserId, "User.Reactivated",
                new { status = user.Status, lockedUntilUtc = user.LockedUntilUtc }, new { status = result.Status }, reason), cancellationToken);
        return result;
    }

    public async Task<UserLifecycleResultDto> DeleteAsync(int userId, DeleteAdminUserRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        Ensure(userId, performedByUserId);
        var reason = Reason(request?.Reason);
        var user = await Load(userId, cancellationToken);
        if (!string.Equals(request!.ConfirmEmail?.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Gõ lại email của người dùng để xác nhận xóa.");

        var blockers = await FindBlockersAsync(user, cancellationToken);
        if (blockers.Count > 0)
            throw new ConflictException("Không thể xóa người dùng này: " + string.Join("; ", blockers)
                + ". Hãy xử lý các mục trên hoặc dùng Đình chỉ thay thế.");

        var result = await users.EraseUserAsync(userId, reason, request.RowVersion, cancellationToken);
        if (result.Changed)
            await audit.WriteAsync(Build(userId, performedByUserId, "User.Deleted", new { status = user.Status, roles = user.Roles },
                new { status = result.Status }, reason), cancellationToken);
        return result;
    }

    /// <summary>Danh sách lý do chặn xóa (tiếng Việt, hiện nguyên văn cho Admin). Dịch vụ nào không trả lời → 503, không xóa.</summary>
    private async Task<List<string>> FindBlockersAsync(AdminUserDto user, CancellationToken ct)
    {
        var blockers = new List<string>();
        if (await obligations.HasOpenBookingsAsync(user.Id, ct)) blockers.Add("còn booking đang mở");
        if (user.OwnerProfile is { } owner)
        {
            var contracts = await obligations.CountSignedContractsAsync(owner.Id, ct);
            if (contracts > 0) blockers.Add($"còn {contracts} hợp đồng bãi đã ký");
            var settlements = await obligations.CountUnpaidSettlementsAsync(owner.Id, ct);
            if (settlements > 0) blockers.Add($"còn {settlements} kỳ quyết toán chưa chi trả");
            var sanctions = await obligations.CountActiveSanctionsAsync(owner.Id, ct);
            if (sanctions > 0) blockers.Add($"còn {sanctions} chế tài đang hiệu lực");
        }
        return blockers;
    }

    private async Task<AdminUserDto> Load(int userId, CancellationToken ct)
        => await users.FindUserByIdAsync(userId, ct) ?? throw new NotFoundException("Người dùng", userId);

    private static void Ensure(int userId, int adminId)
    {
        if (userId <= 0) throw new ValidationException("Id người dùng phải là số nguyên dương.");
        if (adminId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
    }

    private static string Reason(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0) throw new ValidationException("Cần nhập lý do.");
        if (text.Length > 1000) throw new ValidationException("Lý do tối đa 1000 ký tự.");
        return text;
    }

    private static AuditLog Build(int userId, int adminId, string action, object before, object after, string reason) => new()
    {
        SourceService = "AdminService",
        UserId = adminId,
        Action = action,
        EntityName = "User",
        EntityId = userId.ToString(),
        OldValuesJson = JsonSerializer.Serialize(before, Json),
        NewValuesJson = JsonSerializer.Serialize(after, Json),
        Reason = reason,
    };
}
