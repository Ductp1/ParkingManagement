using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace UserService.Application.Features.Users;

// ===== DTO =====
public sealed record UserDto(int Id, string FullName, string? Email, string? PhoneNumber, string Status, string KycStatus,
    IReadOnlyList<string> Roles, OwnerProfileDto? OwnerProfile);

public sealed record OwnerProfileDto(int Id, string BusinessName, string? TaxCode, string BankName, string BankAccountNumber);

public sealed record UserSummaryDto(int Id, string FullName, string? Email, string Status, IReadOnlyList<string> Roles);

// ===== PORT (Infrastructure cài đặt bằng EF Core + LINQ) =====
public interface IUserQueries
{
    Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<PagedResult<UserSummaryDto>> ListAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken);
}

// ===== USE CASE =====
public interface IGetUserByIdUseCase
{
    Task<UserDto> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class GetUserByIdUseCase(IUserQueries queries) : IGetUserByIdUseCase
{
    public async Task<UserDto> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ValidationException("Id người dùng phải là số nguyên dương.");
        return await queries.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Người dùng", id);
    }
}

public interface IListUsersUseCase
{
    Task<PagedResult<UserSummaryDto>> ExecuteAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>UC-40: Admin tìm kiếm người dùng, lọc theo vai trò, có phân trang.</summary>
public sealed class ListUsersUseCase(IUserQueries queries) : IListUsersUseCase
{
    public Task<PagedResult<UserSummaryDto>> ExecuteAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ValidationException("page phải ≥ 1.");
        if (pageSize is < 1 or > 100) throw new ValidationException("pageSize phải trong khoảng 1–100.");
        return queries.ListAsync(role, page, pageSize, cancellationToken);
    }
}
