using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER cài đặt ILotDetailsRepository bằng EF Core + LINQ trên DbContext của ParkingService.
/// </summary>
public sealed class LotDetailsRepository(ParkingDbContext db) : ILotDetailsRepository
{
    // ==================== AMENITIES ====================

    public async Task<IReadOnlyList<LotAmenity>> GetAmenitiesByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => await db.LotAmenities.AsNoTracking()
            .Where(a => a.ParkingLotId == parkingLotId)
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);

    public Task<LotAmenity?> GetAmenityByCodeAsync(int parkingLotId, string code, CancellationToken cancellationToken = default)
        => db.LotAmenities.FirstOrDefaultAsync(
            a => a.ParkingLotId == parkingLotId && a.Code == code, cancellationToken);

    public Task AddAmenityAsync(LotAmenity amenity, CancellationToken cancellationToken = default)
    {
        db.LotAmenities.Add(amenity);
        return Task.CompletedTask;
    }

    public Task DeleteAmenityAsync(LotAmenity amenity, CancellationToken cancellationToken = default)
    {
        db.LotAmenities.Remove(amenity);
        return Task.CompletedTask;
    }

    public async Task ReplaceAmenitiesAsync(int parkingLotId, IEnumerable<LotAmenity> amenities, CancellationToken cancellationToken = default)
    {
        var existing = await db.LotAmenities
            .Where(a => a.ParkingLotId == parkingLotId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            db.LotAmenities.RemoveRange(existing);
        }

        db.LotAmenities.AddRange(amenities);
    }

    // ==================== PHOTOS ====================

    public async Task<IReadOnlyList<LotPhoto>> GetPhotosByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => await db.LotPhotos.AsNoTracking()
            .Where(p => p.ParkingLotId == parkingLotId)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

    public Task<LotPhoto?> GetPhotoByIdAsync(int photoId, CancellationToken cancellationToken = default)
        => db.LotPhotos.FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);

    public Task AddPhotoAsync(LotPhoto photo, CancellationToken cancellationToken = default)
    {
        db.LotPhotos.Add(photo);
        return Task.CompletedTask;
    }

    public Task UpdatePhotoAsync(LotPhoto photo, CancellationToken cancellationToken = default)
    {
        db.LotPhotos.Update(photo);
        return Task.CompletedTask;
    }

    public Task DeletePhotoAsync(LotPhoto photo, CancellationToken cancellationToken = default)
    {
        db.LotPhotos.Remove(photo);
        return Task.CompletedTask;
    }

    public async Task SetCoverPhotoAsync(int parkingLotId, int photoId, CancellationToken cancellationToken = default)
    {
        var photos = await db.LotPhotos
            .Where(p => p.ParkingLotId == parkingLotId)
            .ToListAsync(cancellationToken);

        string? coverUrl = null;
        foreach (var p in photos)
        {
            if (p.Id == photoId)
            {
                p.IsCover = true;
                coverUrl = p.Url;
            }
            else
            {
                p.IsCover = false;
            }
        }

        var lot = await db.ParkingLots.FirstOrDefaultAsync(l => l.Id == parkingLotId, cancellationToken);
        if (lot != null && coverUrl != null)
        {
            lot.SetCoverImageUrl(coverUrl);
        }
    }

    public async Task ReorderPhotosAsync(int parkingLotId, IReadOnlyDictionary<int, int> photoSortOrders, CancellationToken cancellationToken = default)
    {
        var photos = await db.LotPhotos
            .Where(p => p.ParkingLotId == parkingLotId)
            .ToListAsync(cancellationToken);

        foreach (var photo in photos)
        {
            if (photoSortOrders.TryGetValue(photo.Id, out var newSortOrder))
            {
                photo.SortOrder = newSortOrder;
            }
        }
    }

    // ==================== LOT & ATTRIBUTES ====================

    public Task<ParkingLot?> GetLotByIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => db.ParkingLots.FirstOrDefaultAsync(p => p.Id == parkingLotId && !p.IsDeleted, cancellationToken);

    public Task<ParkingLot?> GetLotWithDetailsAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => db.ParkingLots
            .Include(p => p.Amenities)
            .Include(p => p.Photos)
            .FirstOrDefaultAsync(p => p.Id == parkingLotId && !p.IsDeleted, cancellationToken);

    public Task UpdateLotAsync(ParkingLot lot, CancellationToken cancellationToken = default)
    {
        db.ParkingLots.Update(lot);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
