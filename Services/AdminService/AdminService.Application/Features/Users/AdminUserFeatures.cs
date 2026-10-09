using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Users;

// ===== USE CASE (đọc) =====
public interface IListAdminUsersUseCase
{
    Task<PagedResult<AdminUserSummaryDto>> ExecuteAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-096 (UC-40): Admin xem danh sách người dùng, lọc theo vai trò, có phân trang.
/// Dữ liệu thuộc UserService nên chỉ đọc qua IUserServiceClient.
/// </summary>
public sealed class ListAdminUsersUseCase(IUserServiceClient userService) : IListAdminUsersUseCase
{
    public Task<PagedResult<AdminUserSummaryDto>> ExecuteAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (role is { } r && !Enum.IsDefined(r)) throw new ValidationException("Vai trò không hợp lệ.");
        if (page < 1) throw new ValidationException("page phải ≥ 1.");
        if (pageSize is < 1 or > 100) throw new ValidationException("pageSize phải trong khoảng 1–100.");
        return userService.ListUsersAsync(role, page, pageSize, cancellationToken);
    }
}

public interface IGetAdminUserByIdUseCase
{
    Task<AdminUserDto> ExecuteAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>US-096 (UC-40): Admin xem thông tin, vai trò, trạng thái của một người dùng kèm hồ sơ chủ bãi (nếu có).</summary>
public sealed class GetAdminUserByIdUseCase(IUserServiceClient userService) : IGetAdminUserByIdUseCase
{
    public async Task<AdminUserDto> ExecuteAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0) throw new ValidationException("Id người dùng phải là số nguyên dương.");
        return await userService.FindUserByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", userId);
    }
}
