using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Queries;

/// <summary>ADAPTER cổng đọc Admin dashboard (US-081): AsNoTracking + Select, chỉ đọc cột cần – không đọc OwnerProfileId, hotline.</summary>
public sealed class AdminLotSummaryQueries(ParkingDbContext db) : IAdminLotSummaryQueries
{
    public async Task<IReadOnlyList<AdminLotSummaryRow>> ListAsync(CancellationToken cancellationToken = default)
        => await Rows(db.ParkingLots.AsNoTracking(), db.LotCapacityConfigs.AsNoTracking()).ToListAsync(cancellationToken);

    /// <summary>Cờ vận hành lấy bằng subquery theo ParkingLotId (bãi chưa có LotCapacityConfig → false).</summary>
    public static IQueryable<AdminLotSummaryRow> Rows(IQueryable<ParkingLot> lots, IQueryable<LotCapacityConfig> capacityConfigs)
        => lots.Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Select(p => new AdminLotSummaryRow(p.Id, p.Name, p.City, p.District, p.Status, p.IntegrationTier, p.TotalSlots,
                capacityConfigs.Any(c => c.ParkingLotId == p.Id && c.IsEmergencyStopped),
                capacityConfigs.Any(c => c.ParkingLotId == p.Id && c.InstantBookingPaused)));
}
