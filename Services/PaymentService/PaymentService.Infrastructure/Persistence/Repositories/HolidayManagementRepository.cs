using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Lớp cài đặt kho lưu trữ và quản lý ngày nghỉ lễ (Holiday) sử dụng EF Core.
/// </summary>
public sealed class HolidayManagementRepository(PaymentDbContext db) : IHolidayManagementRepository
{
    public async Task<IReadOnlyList<Holiday>> GetAllAsync(int? year, CancellationToken cancellationToken)
    {
        var query = db.Holidays.AsNoTracking();

        if (year.HasValue)
        {
            query = query.Where(h => h.Date.Year == year.Value);
        }

        return await query.OrderBy(h => h.Date).ToListAsync(cancellationToken);
    }

    public async Task<Holiday?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await db.Holidays.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(DateOnly date, string? city, CancellationToken cancellationToken)
    {
        return await db.Holidays.AnyAsync(h => h.Date == date && h.City == city, cancellationToken);
    }

    public async Task AddAsync(Holiday holiday, CancellationToken cancellationToken)
    {
        await db.Holidays.AddAsync(holiday, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Holiday holiday, CancellationToken cancellationToken)
    {
        db.Holidays.Remove(holiday);
        await db.SaveChangesAsync(cancellationToken);
    }
}

