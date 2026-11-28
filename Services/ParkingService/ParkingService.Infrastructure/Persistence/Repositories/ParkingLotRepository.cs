using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>ADAPTER cài đặt IParkingLotRepository bằng EF Core + LINQ.</summary>
public sealed class ParkingLotRepository(ParkingDbContext db) : IParkingLotRepository
{
    public async Task<ParkingLot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // SELECT TOP(1) ... FROM ParkingLots WHERE Id = @id AND IsDeleted = 0 (kèm Amenities và Photos theo US-018)
        var lot = await db.ParkingLots
            .Include(p => p.Amenities)
            .Include(p => p.Photos)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (lot != null && lot.Status == ParkingLotStatus.Active && lot.AvailableSlots == 0)
        {
            var realAvailableCount = await db.Slots
                .CountAsync(s => s.Floor.Zone.ParkingLotId == lot.Id
                              && s.IsActive
                              && s.State == SlotState.Available, cancellationToken);
            if (realAvailableCount > 0)
            {
                var total = Math.Max(lot.TotalSlots, realAvailableCount);
                lot.SetSlotCounters(total, realAvailableCount);
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (lot.TotalSlots > 0)
            {
                lot.SetSlotCounters(lot.TotalSlots, lot.TotalSlots);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return lot;
    }

    public async Task<IReadOnlyList<ParkingLot>> FindActiveInBoxAsync(double minLat, double maxLat, double minLng, double maxLng,
        int minHeightCm, CancellationToken cancellationToken = default)
    {
        // Dùng index (Latitude, Longitude) để lọc thô; Haversine chính xác tính ở Application.
        var lots = await db.ParkingLots
            .Include(p => p.Amenities)
            .Where(p => p.Status == ParkingLotStatus.Active
                     && p.Latitude >= minLat && p.Latitude <= maxLat
                     && p.Longitude >= minLng && p.Longitude <= maxLng
                     && p.MaxHeightCm >= minHeightCm)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var lot in lots)
        {
            if (lot.AvailableSlots == 0)
            {
                var realAvailableCount = await db.Slots
                    .CountAsync(s => s.Floor.Zone.ParkingLotId == lot.Id
                                  && s.IsActive
                                  && s.State == SlotState.Available, cancellationToken);
                if (realAvailableCount > 0)
                {
                    var total = Math.Max(lot.TotalSlots, realAvailableCount);
                    lot.SetSlotCounters(total, realAvailableCount);
                    changed = true;
                }
                else if (lot.TotalSlots > 0)
                {
                    lot.SetSlotCounters(lot.TotalSlots, lot.TotalSlots);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return lots;
    }
}
