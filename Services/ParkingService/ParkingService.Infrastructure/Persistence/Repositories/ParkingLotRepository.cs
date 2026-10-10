using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>ADAPTER cài đặt IParkingLotRepository bằng EF Core + LINQ.</summary>
public sealed class ParkingLotRepository(ParkingDbContext db) : IParkingLotRepository
{
    public Task<ParkingLot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        // SELECT TOP(1) ... FROM ParkingLots WHERE Id = @id AND IsDeleted = 0 (kèm Amenities và Photos theo US-018)
        => db.ParkingLots.AsNoTracking()
            .Include(p => p.Amenities)
            .Include(p => p.Photos)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ParkingLot>> FindActiveInBoxAsync(double minLat, double maxLat, double minLng, double maxLng,
        int minHeightCm, CancellationToken cancellationToken = default)
        // Dùng index (Latitude, Longitude) để lọc thô; Haversine chính xác tính ở Application.
        => await db.ParkingLots.AsNoTracking()
            .Where(p => p.Status == ParkingLotStatus.Active
                     && p.Latitude >= minLat && p.Latitude <= maxLat
                     && p.Longitude >= minLng && p.Longitude <= maxLng
                     && p.MaxHeightCm >= minHeightCm)
            .ToListAsync(cancellationToken);
}
