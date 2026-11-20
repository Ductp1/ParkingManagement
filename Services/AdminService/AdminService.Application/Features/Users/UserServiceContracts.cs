using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Application.Features.Users;

// ===== DTO (dữ liệu đọc từ UserService – AdminService không truy cập pm_user) =====
/// <summary>BusinessName = null cho tới khi UserService bổ sung trường này vào danh sách người dùng.</summary>
public sealed record AdminUserSummaryDto(int Id, string FullName, string? Email, string Status, IReadOnlyList<string> Roles,
    string? BusinessName = null);

public sealed record AdminUserOwnerProfileDto(int Id, string BusinessName);

/// <summary>RowVersion (base64) Admin gửi lại khi đình chỉ / xóa; LockedUntilUtc + LockReason cho biết user đang bị đình chỉ.</summary>
public sealed record AdminUserDto(int Id, string FullName, string? Email, string? PhoneNumber, string Status, string KycStatus,
    IReadOnlyList<string> Roles, AdminUserOwnerProfileDto? OwnerProfile, string? RowVersion = null, DateTime? LockedUntilUtc = null,
    string? LockReason = null, bool OwnerLocked = false);

/// <summary>Kết quả đình chỉ / mở lại / xóa từ UserService. Changed = false khi gọi lại một thao tác đã xong (idempotent).</summary>
public sealed record UserLifecycleResultDto(int UserId, string Status, DateTime? LockedUntilUtc, string? RowVersion, bool Changed);

/// <summary>Tài khoản chủ bãi theo UserService: hồ sơ chủ bãi + user sở hữu + trạng thái khóa hiện tại.</summary>
public sealed record OwnerAccountDto(int OwnerProfileId, int UserId, string BusinessName, string FullName, string? Email,
    string? PhoneNumber, bool IsLocked, string OwnerStatus, string UserStatus);

// ===== PORT (gọi UserService) =====
/// <summary>
/// US-096: Cổng duy nhất để AdminService lấy dữ liệu người dùng. Infrastructure cài bằng HttpClient gọi qua Gateway;
/// unit test dùng fake. Lỗi được báo bằng exception chung của SharedKernel: UserService từ chối token →
/// AuthenticationException (401) / ForbiddenException (403); không gọi được hoặc phản hồi không hợp lệ →
/// DependencyUnavailableException (503).
/// </summary>
public interface IUserServiceClient
{
    Task<PagedResult<AdminUserSummaryDto>> ListUsersAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Trả null khi UserService báo 404 (người dùng không tồn tại).</summary>
    Task<AdminUserDto?> FindUserByIdAsync(int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Khóa chủ bãi bên UserService (idempotent: đã khóa rồi gọi lại vẫn thành công). lockedUntilUtc = null là vô thời hạn.
    /// Trả trạng thái sau khi khóa; null khi UserService báo chủ bãi không tồn tại.
    /// </summary>
    Task<OwnerAccountDto?> LockOwnerAsync(int ownerProfileId, string reason, DateTime? lockedUntilUtc, int performedByUserId, CancellationToken cancellationToken);
    /// <summary>Mở khóa chủ bãi bên UserService (idempotent). Trả null khi UserService báo chủ bãi không tồn tại.</summary>
    Task<OwnerAccountDto?> UnlockOwnerAsync(int ownerProfileId, string reason, int performedByUserId, CancellationToken cancellationToken);

    /// <summary>
    /// US-096: đình chỉ user (lockedUntilUtc = null là vô thời hạn). Lỗi nghiệp vụ của UserService (tự khóa mình, Admin cuối, RowVersion lệch)
    /// được chuyển nguyên văn thành ConflictException / ValidationException.
    /// </summary>
    Task<UserLifecycleResultDto> SuspendUserAsync(int userId, string reason, DateTime? lockedUntilUtc, string? rowVersion, CancellationToken cancellationToken)
        => throw new NotSupportedException();
    Task<UserLifecycleResultDto> ReactivateUserAsync(int userId, string reason, string? rowVersion, CancellationToken cancellationToken)
        => throw new NotSupportedException();
    /// <summary>Xóa = ẩn danh hóa tài khoản (không hoàn tác). AdminService đã kiểm tra nghĩa vụ ở các service khác trước khi gọi.</summary>
    Task<UserLifecycleResultDto> EraseUserAsync(int userId, string reason, string? rowVersion, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
