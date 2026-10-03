using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT truy cập dữ liệu bãi đỗ. Application chỉ biết interface này;
/// ParkingService.Infrastructure cài đặt bằng EF Core + LINQ (Dependency Inversion).
/// </summary>
public interface IParkingLotRepository
{
    Task<ParkingLot?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Bãi Active nằm trong khung tọa độ và có trần cao ≥ <paramref name="minHeightCm"/>.</summary>
    Task<IReadOnlyList<ParkingLot>> FindActiveInBoxAsync(double minLat, double maxLat, double minLng, double maxLng,
        int minHeightCm, CancellationToken cancellationToken = default);
}
