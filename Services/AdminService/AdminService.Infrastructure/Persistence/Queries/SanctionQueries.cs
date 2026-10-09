using AdminService.Application.Features.Owners;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.Infrastructure.Persistence.Queries;

/// <summary>Đọc bảng Sanctions (AsNoTracking, Select thẳng sang DTO, phân trang Skip/Take).</summary>
public sealed class SanctionQueries(AdminDbContext db) : ISanctionQueries
{
    public async Task<PagedResult<SanctionDto>> ListByOwnerAsync(int ownerProfileId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Sanctions.AsNoTracking().Where(s => s.OwnerProfileId == ownerProfileId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(s => s.StartsAtUtc).ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new SanctionDto(s.Id, s.OwnerProfileId, s.ParkingLotId, s.Level.ToString(), s.Reason, s.StartsAtUtc,
                s.EndsAtUtc, s.Status.ToString(), s.PenaltyAmount, s.IssuedByUserId, s.CreatedAtUtc, s.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<SanctionDto>(items, page, pageSize, total);
    }
}
