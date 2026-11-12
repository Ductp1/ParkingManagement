using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT thao tác dữ liệu tiện ích (Amenities), bộ sưu tập ảnh (Photos) và thuộc tính của bãi xe (US-064).
/// </summary>
public interface ILotDetailsRepository
{
    // ==================== AMENITIES ====================
    Task<IReadOnlyList<LotAmenity>> GetAmenitiesByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default);
    Task<LotAmenity?> GetAmenityByCodeAsync(int parkingLotId, string code, CancellationToken cancellationToken = default);
    Task AddAmenityAsync(LotAmenity amenity, CancellationToken cancellationToken = default);
    Task DeleteAmenityAsync(LotAmenity amenity, CancellationToken cancellationToken = default);
    Task ReplaceAmenitiesAsync(int parkingLotId, IEnumerable<LotAmenity> amenities, CancellationToken cancellationToken = default);

    // ==================== PHOTOS ====================
    Task<IReadOnlyList<LotPhoto>> GetPhotosByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default);
    Task<LotPhoto?> GetPhotoByIdAsync(int photoId, CancellationToken cancellationToken = default);
    Task AddPhotoAsync(LotPhoto photo, CancellationToken cancellationToken = default);
    Task UpdatePhotoAsync(LotPhoto photo, CancellationToken cancellationToken = default);
    Task DeletePhotoAsync(LotPhoto photo, CancellationToken cancellationToken = default);
    Task SetCoverPhotoAsync(int parkingLotId, int photoId, CancellationToken cancellationToken = default);
    Task ReorderPhotosAsync(int parkingLotId, IReadOnlyDictionary<int, int> photoSortOrders, CancellationToken cancellationToken = default);

    // ==================== LOT & ATTRIBUTES ====================
    Task<ParkingLot?> GetLotByIdAsync(int parkingLotId, CancellationToken cancellationToken = default);
    Task<ParkingLot?> GetLotWithDetailsAsync(int parkingLotId, CancellationToken cancellationToken = default);
    Task UpdateLotAsync(ParkingLot lot, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
