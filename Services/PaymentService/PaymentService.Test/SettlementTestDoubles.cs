using System.Reflection;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Settlements;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence.Queries;

namespace PaymentService.Test;

// US-080: test double dùng chung cho sinh kỳ quyết toán.

/// <summary>
/// pm_payment trong bộ nhớ chạy ĐÚNG hình dạng truy vấn của Infrastructure (SettlementQueryShapes) và mô phỏng các index duy nhất
/// (kỳ / chủ bãi, giao dịch, hoàn tiền, khoản giữ, voucher) + RowVersion như PostgreSQL.
/// </summary>
internal sealed class InMemorySettlementRepository : ISettlementGenerationRepository
{
    public List<Payment> Payments { get; } = [];
    public List<Refund> Refunds { get; } = [];
    public List<CommissionEntry> Entries { get; } = [];
    public List<CompensationVoucher> Vouchers { get; } = [];
    public List<FinancialAdjustment> Adjustments { get; } = [];
    public List<Settlement> Settlements { get; } = [];
    public List<SettlementPeriod> Periods { get; } = [];
    public List<OutboxMessage> Outbox { get; } = [];
    public bool LockAvailable { get; set; } = true;
    private int _nextId = 1000;

    private IQueryable<SettlementLine> Lines => Settlements.SelectMany(s => s.Lines).ToList().AsQueryable();

    public Task<IAsyncDisposable?> TryAcquireRunLockAsync(CancellationToken cancellationToken)
        => Task.FromResult<IAsyncDisposable?>(LockAvailable ? new NoopLock() : null);

    public Task<SettlementPeriod?> GetLatestPeriodAsync(CancellationToken cancellationToken)
        => Task.FromResult(Periods.OrderByDescending(p => p.PeriodStart).FirstOrDefault());

    public Task<SettlementPeriod?> FindPeriodAsync(DateOnly periodStart, CancellationToken cancellationToken)
        => Task.FromResult(Periods.FirstOrDefault(p => p.PeriodStart == periodStart));

    public Task<SettlementPeriod> AddPeriodAsync(SettlementPeriod period, CancellationToken cancellationToken)
    {
        var existing = Periods.FirstOrDefault(p => p.PeriodStart == period.PeriodStart);
        if (existing is not null) return Task.FromResult(existing);
        Periods.Add(period.WithId(++_nextId));
        return Task.FromResult(period);
    }

    public Task<IReadOnlyList<int>> ListCandidateOwnerIdsAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        var owners = new HashSet<int>();
        owners.UnionWith(SettlementQueryShapes.OwnersWithUnsettledPayments(Payments.AsQueryable(), Lines, cutoffUtc));
        owners.UnionWith(SettlementQueryShapes.OwnersWithUnsettledRefunds(Refunds.AsQueryable(), Payments.AsQueryable(), Lines, cutoffUtc));
        owners.UnionWith(SettlementQueryShapes.OwnersWithApprovedAdjustments(Adjustments.AsQueryable()));
        owners.UnionWith(SettlementQueryShapes.OwnersWithVouchersToCharge(Vouchers.AsQueryable(), Adjustments.AsQueryable(), cutoffUtc));
        owners.UnionWith(SettlementQueryShapes.OwnersWithCarryOver(Settlements.AsQueryable()));
        owners.UnionWith(SettlementQueryShapes.OwnersWithOpenHolds(Lines, Settlements.AsQueryable()));
        return Task.FromResult<IReadOnlyList<int>>([.. owners.Order()]);
    }

    public Task<OwnerSettlementState> GetOwnerStateAsync(int ownerProfileId, DateOnly periodStart, CancellationToken cancellationToken)
        => Task.FromResult(SettlementQueryShapes.ToOwnerState(
            SettlementQueryShapes.OwnerSettlements(Settlements.AsQueryable(), ownerProfileId).AsEnumerable().Select(s => (s.Id, s.PeriodStart, s.Status)),
            periodStart));

    public Task<SettlementSnapshot> LoadSnapshotAsync(int ownerProfileId, DateTime cutoffUtc, DateTime nowUtc, int? excludeSettlementId,
        CancellationToken cancellationToken)
    {
        var lines = Lines;
        var payments = SettlementQueryShapes.UnsettledPayments(Payments.AsQueryable(), lines, ownerProfileId, cutoffUtc, excludeSettlementId).ToList();
        var refunds = SettlementQueryShapes.UnsettledRefunds(Refunds.AsQueryable(), Payments.AsQueryable(), lines, ownerProfileId, cutoffUtc,
            excludeSettlementId).ToList();
        var settledIds = refunds.Where(r => r.PaymentSettled).Select(r => r.PaymentId).Distinct().ToList();
        var involved = payments.Select(p => p.PaymentId).Concat(refunds.Select(r => r.PaymentId)).Distinct().ToList();
        var voucherAdjustments = SettlementQueryShapes.VoucherAdjustments(Adjustments.AsQueryable(), ownerProfileId)
            .Select(a => new { a.Id, a.SourceReference, a.Amount }).ToList();
        return Task.FromResult(new SettlementSnapshot(payments, refunds,
            SettlementQueryShapes.PaymentInfo(Payments.AsQueryable(), settledIds).ToList(),
            SettlementQueryShapes.Accruals(Entries.AsQueryable(), involved).ToList(),
            SettlementQueryShapes.PriorRefunds(lines, refunds.Select(r => r.PaymentId).Distinct().ToList(), excludeSettlementId).ToList(),
            SettlementQueryShapes.HeldElsewhere(lines, Settlements.AsQueryable(), ownerProfileId, excludeSettlementId).ToList(),
            SettlementQueryShapes.Vouchers(Vouchers.AsQueryable(), ownerProfileId, cutoffUtc).ToList(),
            SettlementQueryShapes.ToVoucherCharges(voucherAdjustments.Select(a => (a.Id, a.SourceReference, a.Amount))),
            SettlementQueryShapes.AttachableAdjustments(Adjustments.AsQueryable(), ownerProfileId, nowUtc, excludeSettlementId).ToList(),
            SettlementQueryShapes.CarryOver(Settlements.AsQueryable(), ownerProfileId, excludeSettlementId).FirstOrDefault()));
    }

    public Task<Settlement?> FindSettlementAsync(int settlementId, CancellationToken cancellationToken)
        => Task.FromResult(Settlements.FirstOrDefault(s => s.Id == settlementId));

    public Task AddAsync(Settlement settlement, IReadOnlyList<FinancialAdjustment> newAdjustments, IReadOnlyList<int> attachAdjustmentIds,
        OutboxMessage audit, CancellationToken cancellationToken)
    {
        if (Settlements.Any(s => s.OwnerProfileId == settlement.OwnerProfileId && s.PeriodStart == settlement.PeriodStart))
            throw new SettlementWriteConflictException("23505 Settlements(OwnerProfileId, PeriodStart)");
        EnsureUnique(settlement, newAdjustments);
        settlement.WithId(++_nextId);
        Settlements.Add(settlement);
        Persist(settlement, newAdjustments, attachAdjustmentIds, audit);
        return Task.CompletedTask;
    }

    public Task<Settlement> ReplaceDraftAsync(int settlementId, byte[] expectedRowVersion, Action<Settlement> apply,
        IReadOnlyList<FinancialAdjustment> newAdjustments, IReadOnlyList<int> attachAdjustmentIds, Func<Settlement, OutboxMessage> audit,
        CancellationToken cancellationToken)
    {
        var settlement = Settlements.FirstOrDefault(s => s.Id == settlementId) ?? throw new NotFoundException("Kỳ quyết toán", settlementId);
        if (settlement.Status != SettlementStatus.Draft) throw new ConflictException("Không còn Draft.");
        if (!settlement.RowVersion.AsSpan().SequenceEqual(expectedRowVersion)) throw new ConflictException("Dữ liệu kỳ quyết toán đã thay đổi.");

        settlement.Lines.Clear();
        foreach (var adjustment in Adjustments.Where(a => a.SettlementId == settlementId)) adjustment.SettlementId = null;
        settlement.Adjustments.Clear();
        apply(settlement);
        EnsureUnique(settlement, newAdjustments);
        Persist(settlement, newAdjustments, attachAdjustmentIds, audit(settlement));
        return Task.FromResult(settlement);
    }

    private void EnsureUnique(Settlement settlement, IReadOnlyList<FinancialAdjustment> newAdjustments)
    {
        var others = Settlements.Where(s => s.Id != settlement.Id).SelectMany(s => s.Lines).ToList();
        foreach (var line in settlement.Lines)
        {
            if (line.LineType is SettlementLineType.Booking or SettlementLineType.WalkIn
                && others.Any(o => o.PaymentId == line.PaymentId && o.LineType is SettlementLineType.Booking or SettlementLineType.WalkIn))
                throw new SettlementWriteConflictException($"23505 {line.PaymentId}");
            if (line.RefundId is not null && others.Any(o => o.RefundId == line.RefundId))
                throw new SettlementWriteConflictException($"23505 refund {line.RefundId}");
            if (line.LineType == SettlementLineType.DisputeHold && others.Any(o => o.LineType == SettlementLineType.DisputeHold
                    && o.ReferenceCode == line.ReferenceCode && Math.Sign(o.Amount) == Math.Sign(line.Amount)))
                throw new SettlementWriteConflictException($"23505 hold {line.ReferenceCode}");
        }
        foreach (var adjustment in newAdjustments)
            if (Adjustments.Any(a => a.SourceType == adjustment.SourceType && a.SourceReference == adjustment.SourceReference))
                throw new SettlementWriteConflictException($"23505 voucher {adjustment.SourceReference}");
    }

    private void Persist(Settlement settlement, IReadOnlyList<FinancialAdjustment> newAdjustments, IReadOnlyList<int> attachIds, OutboxMessage audit)
    {
        foreach (var line in settlement.Lines.Where(l => l.Id == 0))
        {
            line.WithId(++_nextId);
            line.SettlementId = settlement.Id;
        }
        foreach (var adjustment in newAdjustments)
        {
            adjustment.WithId(++_nextId);
            Adjustments.Add(adjustment);
        }
        foreach (var adjustment in Adjustments.Where(a => attachIds.Contains(a.Id) || newAdjustments.Contains(a)))
        {
            adjustment.SettlementId = settlement.Id;
            if (!settlement.Adjustments.Contains(adjustment)) settlement.Adjustments.Add(adjustment);
        }
        settlement.RowVersion = Guid.NewGuid().ToByteArray();
        Outbox.Add(audit);
    }

    private sealed class NoopLock : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>SETTLEMENT_CYCLE theo thời điểm; Fail = AdminService lỗi.</summary>
internal sealed class FakeCycleClient(Func<DateTime, SettlementCycleQuote>? resolve = null) : ISettlementCycleClient
{
    public List<DateTime> Calls { get; } = [];
    public bool Fail { get; set; }

    public Task<SettlementCycleQuote> ResolveAsync(DateTime atUtc, CancellationToken cancellationToken)
    {
        Calls.Add(atUtc);
        if (Fail) throw new DependencyUnavailableException("AdminService trả HTTP 503.");
        return Task.FromResult(resolve?.Invoke(atUtc) ?? new SettlementCycleQuote("WEEKLY", null));
    }
}

internal sealed class FakeBookingClient : IBookingStatusClient
{
    public Dictionary<string, BookingStatusInfo> Statuses { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<IReadOnlyCollection<string>> Calls { get; } = [];
    public bool Fail { get; set; }

    public Task<IReadOnlyDictionary<string, BookingStatusInfo>> GetStatusesAsync(IReadOnlyCollection<string> bookingCodes,
        CancellationToken cancellationToken)
    {
        Calls.Add(bookingCodes);
        if (Fail) throw new DependencyUnavailableException("BookingService không phản hồi.");
        return Task.FromResult<IReadOnlyDictionary<string, BookingStatusInfo>>(bookingCodes.ToDictionary(c => c,
            c => Statuses.GetValueOrDefault(c) ?? BookingStatusInfo.NotFound, StringComparer.OrdinalIgnoreCase));
    }
}

internal sealed class FakeHoldClient : IPayoutHoldClient
{
    public List<PayoutHoldItem> Holds { get; } = [];
    public List<int?> Calls { get; } = [];
    public bool Fail { get; set; }

    public Task<IReadOnlyList<PayoutHoldItem>> ListOpenHoldsAsync(int? ownerProfileId, CancellationToken cancellationToken)
    {
        Calls.Add(ownerProfileId);
        if (Fail) throw new DependencyUnavailableException("SupportService trả HTTP 500.");
        return Task.FromResult<IReadOnlyList<PayoutHoldItem>>([.. Holds.Where(h => ownerProfileId is null || h.OwnerProfileId == ownerProfileId)]);
    }
}

internal static class SettlementTestData
{
    private static readonly PropertyInfo CreatedAt = typeof(BaseEntity).GetProperty(nameof(BaseEntity.CreatedAtUtc))!;
    private static readonly PropertyInfo UpdatedAt = typeof(BaseEntity).GetProperty(nameof(BaseEntity.UpdatedAtUtc))!;

    public static T CreatedAtUtc<T>(this T entity, DateTime createdAtUtc, DateTime? updatedAtUtc = null) where T : BaseEntity
    {
        CreatedAt.SetValue(entity, createdAtUtc);
        if (updatedAtUtc is not null) UpdatedAt.SetValue(entity, updatedAtUtc);
        return entity;
    }

    public static Refund CompletedRefund(int id, Payment payment, decimal amount, DateTime processedAtUtc)
        => new Refund { PaymentId = payment.Id, Amount = amount, Status = RefundStatus.Completed, ProcessedAtUtc = processedAtUtc,
            Reason = RefundReason.LotFault }.WithId(id);

    public static BookingStatusInfo Ended(string status, DateTime atUtc) => new(true, status, atUtc);
}
