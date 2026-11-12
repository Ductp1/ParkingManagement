using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cài đặt truy xuất dữ liệu mã khuyến mãi sử dụng Entity Framework Core và PaymentDbContext (US-073).
/// </summary>
public sealed class PromotionRepository(PaymentDbContext dbContext) : IPromotionRepository
{
    public async Task<Promotion?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        return await dbContext.Promotions
            .FirstOrDefaultAsync(p => p.Code == normalizedCode, cancellationToken);
    }

    public async Task<Promotion?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Promotions
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Promotion>> ListAsync(
        int? parkingLotId = null, 
        bool? isActiveOnly = null, 
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Promotions.AsNoTracking().AsQueryable();

        if (parkingLotId.HasValue)
        {
            // Lấy mã riêng của bãi xe HOẶC mã chung toàn sàn (ParkingLotId == null)
            query = query.Where(p => p.ParkingLotId == parkingLotId.Value || p.ParkingLotId == null);
        }

        if (isActiveOnly.HasValue && isActiveOnly.Value)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        await dbContext.Promotions.AddAsync(promotion, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        dbContext.Promotions.Update(promotion);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var query = dbContext.Promotions.AsNoTracking().Where(p => p.Code == normalizedCode);

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddRedemptionAsync(PromotionRedemption redemption, CancellationToken cancellationToken = default)
    {
        await dbContext.PromotionRedemptions.AddAsync(redemption, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

