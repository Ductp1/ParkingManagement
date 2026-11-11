using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Settlements;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>US-080: ghi chuyển trạng thái kỳ quyết toán và bút toán điều chỉnh – dữ liệu + audit cùng transaction, RowVersion chống ghi đè.</summary>
public sealed class SettlementReviewRepository(PaymentDbContext db) : ISettlementReviewRepository
{
    public Task<Settlement?> FindTrackedAsync(int settlementId, CancellationToken cancellationToken)
        => db.Settlements.Include(s => s.Period).FirstOrDefaultAsync(s => s.Id == settlementId, cancellationToken);

    public async Task SaveAsync(Settlement settlement, byte[] expectedRowVersion, OutboxMessage audit, CancellationToken cancellationToken)
    {
        db.Entry(settlement).Property(s => s.RowVersion).OriginalValue = expectedRowVersion;
        db.OutboxMessages.Add(audit);
        await SaveOrConflictAsync(cancellationToken);
    }

    public Task<FinancialAdjustment?> FindAdjustmentTrackedAsync(int adjustmentId, CancellationToken cancellationToken)
        => db.FinancialAdjustments.Include(a => a.Settlement).FirstOrDefaultAsync(a => a.Id == adjustmentId, cancellationToken);

    public Task<bool> IsReversedAsync(int adjustmentId, CancellationToken cancellationToken)
        => db.FinancialAdjustments.AnyAsync(a => a.ReversesAdjustmentId == adjustmentId && a.Status != FinancialAdjustmentStatus.Rejected,
            cancellationToken);

    public async Task AddAdjustmentAsync(FinancialAdjustment adjustment, Func<FinancialAdjustment, OutboxMessage> audit,
        CancellationToken cancellationToken)
    {
        // Audit cần Id do database cấp → 2 SaveChanges trong một transaction (lỗi thì không còn gì).
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.FinancialAdjustments.Add(adjustment);
        await db.SaveChangesAsync(cancellationToken);
        db.OutboxMessages.Add(audit(adjustment));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveAdjustmentAsync(FinancialAdjustment adjustment, byte[] expectedRowVersion, OutboxMessage audit,
        CancellationToken cancellationToken)
    {
        db.Entry(adjustment).Property(a => a.RowVersion).OriginalValue = expectedRowVersion;
        db.OutboxMessages.Add(audit);
        await SaveOrConflictAsync(cancellationToken);
    }

    private async Task SaveOrConflictAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("Dữ liệu đã thay đổi, hãy tải lại.");
        }
    }
}
