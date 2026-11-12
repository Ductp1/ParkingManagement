using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai kho lưu trữ nhật ký kiểm toán thay đổi bảng giá bằng EF Core (US-072).
/// </summary>
public sealed class RateCardAuditLogRepository(PaymentDbContext dbContext) : IRateCardAuditLogRepository
{
    public async Task AddAsync(RateCardAuditLog auditLog, CancellationToken cancellationToken)
    {
        await dbContext.RateCardAuditLogs.AddAsync(auditLog, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RateCardAuditLog>> GetByParkingLotIdAsync(int parkingLotId, CancellationToken cancellationToken)
    {
        return await dbContext.RateCardAuditLogs
            .AsNoTracking()
            .Where(x => x.ParkingLotId == parkingLotId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}

