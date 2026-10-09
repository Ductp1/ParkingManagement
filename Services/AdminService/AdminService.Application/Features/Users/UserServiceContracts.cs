using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Application.Features.Users;

// ===== DTO (dữ liệu đọc từ UserService – AdminService không truy cập pm_user) =====
/// <summary>BusinessName = null cho tới khi UserService bổ sung trường này vào danh sách người dùng.</summary>
public sealed record AdminUserSummaryDto(int Id, string FullName, string? Email, string Status, IReadOnlyList<string> Roles,
    string? BusinessName = null);

public sealed record AdminUserOwnerProfileDto(int Id, string BusinessName);

public sealed record AdminUserDto(int Id, string FullName, string? Email, string? PhoneNumber, string Status, string KycStatus,
    IReadOnlyList<string> Roles, AdminUserOwnerProfileDto? OwnerProfile);

/// <summary>Tài khoản chủ bãi theo UserService: hồ sơ chủ bãi + user sở hữu + trạng thái khóa hiện tại.</summary>
public sealed record OwnerAccountDto(int OwnerProfileId, int UserId, string BusinessName, string FullName, string? Email,
    string? PhoneNumber, bool IsLocked, string OwnerStatus, string UserStatus);

// ===== PORT (gọi UserService) =====
/// <summary>
/// US-096: Cổng duy nhất để AdminService lấy dữ liệu người dùng. Infrastructure cài bằng HttpClient gọi qua Gateway;
/// unit test dùng fake. Mọi lỗi kết nối / phản hồi không hợp lệ được báo bằng <see cref="UserServiceUnavailableException"/>.
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
}

/// <summary>
/// Không gọi được UserService (mất kết nối, timeout, mã lỗi ngoài dự kiến, nội dung trả về sai định dạng).
/// Middleware chung hiện chưa có mã 503 nên lỗi này ra ngoài thành 500.
/// </summary>
public sealed class UserServiceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
