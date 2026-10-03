using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Users;

namespace UserService.Infrastructure.Persistence.Queries;

/// <summary>Cài đặt IUserQueries bằng LINQ to Entities – Select thẳng sang DTO, không trả Entity ra ngoài.</summary>
public sealed class UserQueries(UserDbContext db) : IUserQueries
{
    public Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserDto(
                u.Id, u.FullName, u.Email, u.PhoneNumber, u.Status.ToString(), u.KycStatus.ToString(),
                u.Roles.Select(r => r.Role.ToString()).ToList(),
                u.OwnerProfile == null ? null : new OwnerProfileDto(
                    u.OwnerProfile.Id, u.OwnerProfile.BusinessName, u.OwnerProfile.TaxCode,
                    u.OwnerProfile.BankName, u.OwnerProfile.BankAccountNumber)))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<UserSummaryDto>> ListAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();
        if (role is { } r) query = query.Where(u => u.Roles.Any(x => x.Role == r));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new UserSummaryDto(u.Id, u.FullName, u.Email, u.Status.ToString(),
                u.Roles.Select(x => x.Role.ToString()).ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserSummaryDto>(items, page, pageSize, total);
    }
}
