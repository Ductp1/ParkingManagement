using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Lớp cài đặt kho lưu trữ và quản lý bảng giá (RateCard) sử dụng EF Core.
/// </summary>
public sealed class RateCardManagementRepository(PaymentDbContext db) : IRateCardManagementRepository
{
    public async Task<IReadOnlyList<RateCard>> GetByParkingLotIdAsync(int parkingLotId, CancellationToken cancellationToken)
    {
        return await db.RateCards
            .AsNoTracking()
            .Include(c => c.Rules)
            .Where(c => c.ParkingLotId == parkingLotId)
            .OrderByDescending(c => c.EffectiveFromUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<RateCard?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await db.RateCards
            .Include(c => c.Rules)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<RateCard?> GetCurrentActiveAsync(int parkingLotId, DateTime atUtc, CancellationToken cancellationToken)
    {
        return await db.RateCards
            .Where(c => c.ParkingLotId == parkingLotId && c.IsActive
                     && c.EffectiveFromUtc <= atUtc && (c.EffectiveToUtc == null || c.EffectiveToUtc > atUtc))
            .OrderByDescending(c => c.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(RateCard rateCard, CancellationToken cancellationToken)
    {
        await db.RateCards.AddAsync(rateCard, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RateCard rateCard, CancellationToken cancellationToken)
    {
        db.RateCards.Update(rateCard);
        await db.SaveChangesAsync(cancellationToken);
    }
}

