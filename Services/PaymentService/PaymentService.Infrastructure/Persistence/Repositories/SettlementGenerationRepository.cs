using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Settlements;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence.Queries;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>
/// US-080: đọc / ghi sinh kỳ quyết toán. Chỉ ĐỌC Payments / Refunds / CommissionEntries / CompensationVouchers (luồng thanh toán,
/// hoàn tiền, hoa hồng không đổi). Khóa lượt chạy = pg_try_advisory_lock trên chính kết nối của DbContext (giữ mở tới khi nhả).
/// </summary>
public sealed class SettlementGenerationRepository(PaymentDbContext db) : ISettlementGenerationRepository
{
    /// <summary>Khác khóa job hoa hồng (US-079) để hai job không chặn nhau.</summary>
    public const long RunLockKey = 8_000_800_080;

    public async Task<IAsyncDisposable?> TryAcquireRunLockAsync(CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        var acquired = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock({RunLockKey}) AS \"Value\"")
            .SingleAsync(cancellationToken);
        if (acquired) return new RunLock(db);

        await db.Database.CloseConnectionAsync();
        return null;
    }

    // ===== Lịch kỳ =====

    public Task<SettlementPeriod?> GetLatestPeriodAsync(CancellationToken cancellationToken)
        => db.SettlementPeriods.AsNoTracking().OrderByDescending(p => p.PeriodStart).FirstOrDefaultAsync(cancellationToken);

    public Task<SettlementPeriod?> FindPeriodAsync(DateOnly periodStart, CancellationToken cancellationToken)
        => db.SettlementPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.PeriodStart == periodStart, cancellationToken);

    public async Task<SettlementPeriod> AddPeriodAsync(SettlementPeriod period, CancellationToken cancellationToken)
    {
        try
        {
            db.SettlementPeriods.Add(period);
            await db.SaveChangesAsync(cancellationToken);
            db.Entry(period).State = EntityState.Detached;
            return period;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            return await FindPeriodAsync(period.PeriodStart, cancellationToken)
                   ?? throw new InvalidOperationException($"Không đọc lại được kỳ {period.PeriodStart:yyyy-MM-dd} sau khi trùng.");
        }
    }

    // ===== Đọc =====

    public async Task<IReadOnlyList<int>> ListCandidateOwnerIdsAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        var owners = new HashSet<int>();
        owners.UnionWith(await SettlementQueryShapes.OwnersWithUnsettledPayments(db.Payments.AsNoTracking(), db.SettlementLines.AsNoTracking(),
            cutoffUtc).ToListAsync(cancellationToken));
        owners.UnionWith(await SettlementQueryShapes.OwnersWithUnsettledRefunds(db.Refunds.AsNoTracking(), db.Payments.AsNoTracking(),
            db.SettlementLines.AsNoTracking(), cutoffUtc).ToListAsync(cancellationToken));
        owners.UnionWith(await SettlementQueryShapes.OwnersWithApprovedAdjustments(db.FinancialAdjustments.AsNoTracking())
            .ToListAsync(cancellationToken));
        owners.UnionWith(await SettlementQueryShapes.OwnersWithVouchersToCharge(db.CompensationVouchers.AsNoTracking(),
            db.FinancialAdjustments.AsNoTracking(), cutoffUtc).ToListAsync(cancellationToken));
        owners.UnionWith(await SettlementQueryShapes.OwnersWithCarryOver(db.Settlements.AsNoTracking()).ToListAsync(cancellationToken));
        owners.UnionWith(await SettlementQueryShapes.OwnersWithOpenHolds(db.SettlementLines.AsNoTracking(), db.Settlements.AsNoTracking())
            .ToListAsync(cancellationToken));
        return [.. owners.Where(o => o > 0).Order()];
    }

    public async Task<OwnerSettlementState> GetOwnerStateAsync(int ownerProfileId, DateOnly periodStart, CancellationToken cancellationToken)
    {
        var rows = await SettlementQueryShapes.OwnerSettlements(db.Settlements.AsNoTracking(), ownerProfileId)
            .Select(s => new { s.Id, s.PeriodStart, s.Status })
            .ToListAsync(cancellationToken);
        return SettlementQueryShapes.ToOwnerState(rows.Select(r => (r.Id, r.PeriodStart, r.Status)), periodStart);
    }

    public async Task<SettlementSnapshot> LoadSnapshotAsync(int ownerProfileId, DateTime cutoffUtc, DateTime nowUtc, int? excludeSettlementId,
        CancellationToken cancellationToken)
    {
        var lines = db.SettlementLines.AsNoTracking();
        var payments = await SettlementQueryShapes.UnsettledPayments(db.Payments.AsNoTracking(), lines, ownerProfileId, cutoffUtc,
            excludeSettlementId).ToListAsync(cancellationToken);
        var refunds = await SettlementQueryShapes.UnsettledRefunds(db.Refunds.AsNoTracking(), db.Payments.AsNoTracking(), lines, ownerProfileId,
            cutoffUtc, excludeSettlementId).ToListAsync(cancellationToken);

        var settledIds = refunds.Where(r => r.PaymentSettled).Select(r => r.PaymentId).Distinct().ToList();
        var settled = settledIds.Count == 0
            ? []
            : await SettlementQueryShapes.PaymentInfo(db.Payments.AsNoTracking(), settledIds).ToListAsync(cancellationToken);

        var involved = payments.Select(p => p.PaymentId).Concat(refunds.Select(r => r.PaymentId)).Distinct().ToList();
        var accruals = involved.Count == 0
            ? []
            : await SettlementQueryShapes.Accruals(db.CommissionEntries.AsNoTracking(), involved).ToListAsync(cancellationToken);
        var refundPaymentIds = refunds.Select(r => r.PaymentId).Distinct().ToList();
        var prior = refundPaymentIds.Count == 0
            ? []
            : await SettlementQueryShapes.PriorRefunds(lines, refundPaymentIds, excludeSettlementId).ToListAsync(cancellationToken);

        var held = await SettlementQueryShapes.HeldElsewhere(lines, db.Settlements.AsNoTracking(), ownerProfileId, excludeSettlementId)
            .ToListAsync(cancellationToken);
        var vouchers = await SettlementQueryShapes.Vouchers(db.CompensationVouchers.AsNoTracking(), ownerProfileId, cutoffUtc)
            .ToListAsync(cancellationToken);
        var voucherAdjustments = await SettlementQueryShapes.VoucherAdjustments(db.FinancialAdjustments.AsNoTracking(), ownerProfileId)
            .Select(a => new { a.Id, a.SourceReference, a.Amount })
            .ToListAsync(cancellationToken);
        var attachable = await SettlementQueryShapes.AttachableAdjustments(db.FinancialAdjustments.AsNoTracking(), ownerProfileId, nowUtc,
            excludeSettlementId).ToListAsync(cancellationToken);
        var carryOver = await SettlementQueryShapes.CarryOver(db.Settlements.AsNoTracking(), ownerProfileId, excludeSettlementId)
            .FirstOrDefaultAsync(cancellationToken);

        return new SettlementSnapshot(payments, refunds, settled, accruals, prior, held, vouchers,
            SettlementQueryShapes.ToVoucherCharges(voucherAdjustments.Select(a => (a.Id, a.SourceReference, a.Amount))), attachable, carryOver);
    }

    public Task<Settlement?> FindSettlementAsync(int settlementId, CancellationToken cancellationToken)
        => db.Settlements.AsNoTracking().Include(s => s.Period).FirstOrDefaultAsync(s => s.Id == settlementId, cancellationToken);

    // ===== Ghi =====

    public async Task AddAsync(Settlement settlement, IReadOnlyList<FinancialAdjustment> newAdjustments, IReadOnlyList<int> attachAdjustmentIds,
        OutboxMessage audit, CancellationToken cancellationToken)
    {
        try
        {
            if (settlement.Period is { Id: > 0 } period) db.Attach(period);
            db.Settlements.Add(settlement);
            foreach (var adjustment in newAdjustments) settlement.Adjustments.Add(adjustment);
            await AttachAdjustmentsAsync(settlement, attachAdjustmentIds, cancellationToken);
            db.OutboxMessages.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            throw new SettlementWriteConflictException($"Kỳ {settlement.Code} trùng dữ liệu vừa được ghi bởi thao tác khác.", ex);
        }
    }

    public async Task<Settlement> ReplaceDraftAsync(int settlementId, byte[] expectedRowVersion, Action<Settlement> apply,
        IReadOnlyList<FinancialAdjustment> newAdjustments, IReadOnlyList<int> attachAdjustmentIds, Func<Settlement, OutboxMessage> audit,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var settlement = await db.Settlements.Include(s => s.Lines).Include(s => s.Adjustments)
                             .FirstOrDefaultAsync(s => s.Id == settlementId, cancellationToken)
                         ?? throw new NotFoundException("Kỳ quyết toán", settlementId);
        if (settlement.Status != SettlementStatus.Draft)
            throw new ConflictException($"Kỳ quyết toán {settlement.Code} đang ở trạng thái {settlement.Status}, chỉ kỳ Draft được tính lại.");

        try
        {
            // Bước 1: xóa dòng cũ + gỡ bút toán khỏi kỳ, kiểm tra RowVersion (UPDATE … WHERE RowVersion = bản Admin đã đọc).
            db.Entry(settlement).Property(s => s.RowVersion).OriginalValue = expectedRowVersion;
            db.Entry(settlement).Property(s => s.GeneratedAtUtc).IsModified = true;
            db.SettlementLines.RemoveRange(settlement.Lines.ToList());
            foreach (var adjustment in settlement.Adjustments.ToList()) adjustment.SettlementId = null;
            await db.SaveChangesAsync(cancellationToken);
            settlement.Lines.Clear();                                   // dòng đã xóa (Detached) không còn thuộc kỳ

            // Bước 2: kết quả mới (cùng transaction – lỗi thì kỳ giữ nguyên như trước).
            apply(settlement);
            foreach (var adjustment in newAdjustments) settlement.Adjustments.Add(adjustment);
            await AttachAdjustmentsAsync(settlement, attachAdjustmentIds, cancellationToken);
            db.OutboxMessages.Add(audit(settlement));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return settlement;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("Dữ liệu kỳ quyết toán đã thay đổi, hãy tải lại.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            throw new SettlementWriteConflictException($"Kỳ {settlement.Code} trùng dữ liệu vừa được ghi bởi thao tác khác.", ex);
        }
    }

    private async Task AttachAdjustmentsAsync(Settlement settlement, IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return;
        var adjustments = await db.FinancialAdjustments.Where(a => ids.Contains(a.Id)).ToListAsync(cancellationToken);
        foreach (var adjustment in adjustments)
        {
            // Bút toán bị Admin khác gắn vào kỳ khác trong lúc tính → dữ liệu đã đổi, để lượt sau tính lại.
            if (adjustment.SettlementId is { } current && current != settlement.Id)
                throw new ConflictException($"Bút toán điều chỉnh #{adjustment.Id} vừa được gắn vào kỳ khác, hãy tải lại.");
            adjustment.Settlement = settlement;
        }
    }

    /// <summary>23505 = unique_violation của PostgreSQL.</summary>
    public static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private sealed class RunLock(PaymentDbContext db) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({RunLockKey})");
            }
            finally
            {
                // Trả kết nối về pool: Npgsql reset phiên (DISCARD ALL) nên khóa cũng được nhả kể cả khi lệnh unlock lỗi.
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
