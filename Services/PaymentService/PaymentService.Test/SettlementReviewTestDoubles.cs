using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Settlements;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;
using PaymentService.Infrastructure.Persistence.Queries;
using static PaymentService.Test.OwnerRevenueTestData;

namespace PaymentService.Test;

/// <summary>
/// US-080: kỳ quyết toán / bút toán trong bộ nhớ chạy ĐÚNG hình dạng truy vấn đọc của Infrastructure (SettlementReadShapes)
/// và mô phỏng RowVersion (đóng dấu mới mỗi lần lưu, khác bản đã đọc → ConflictException) như PostgreSQL.
/// </summary>
internal sealed class InMemorySettlementReview : ISettlementQueries, ISettlementReviewRepository
{
    public List<Settlement> Settlements { get; } = [];
    public List<FinancialAdjustment> Adjustments { get; } = [];
    public List<SettlementPeriod> Periods { get; } = [];
    public List<OutboxMessage> Outbox { get; } = [];
    private int _nextId = 5000;

    private IQueryable<SettlementLine> Lines => Settlements.SelectMany(s => s.Lines).ToList().AsQueryable();

    // ===== Dựng dữ liệu =====

    public Settlement Add(int owner, DateOnly periodStart, decimal net, SettlementStatus status = SettlementStatus.Draft, string? blocked = null,
        params SettlementLine[] lines)
    {
        var s = new Settlement
        {
            Code = SettlementCalendar.SettlementCode(periodStart, owner), OwnerProfileId = owner, PeriodStart = periodStart,
            PeriodEnd = periodStart.AddDays(6), GrossAmount = Math.Max(net, 0) + 10_000, CommissionAmount = 10_000, NetPayout = net,
            Status = status, IsBlocked = blocked is not null, BlockedReasons = blocked, GeneratedAtUtc = Utc(2026, 11, 10, 3),
            RowVersion = Guid.NewGuid().ToByteArray(),
        }.WithId(++_nextId);
        foreach (var line in lines)
        {
            line.WithId(++_nextId);
            line.SettlementId = s.Id;
            s.Lines.Add(line);
        }
        Settlements.Add(s);
        return s;
    }

    public FinancialAdjustment AddAdjustment(int owner, decimal amount, FinancialAdjustmentStatus status = FinancialAdjustmentStatus.Approved,
        int createdBy = 8, int? settlementId = null)
    {
        var a = new FinancialAdjustment
        {
            OwnerProfileId = owner, Amount = amount, Reason = "Điều chỉnh", CreatedByUserId = createdBy, Status = status, SettlementId = settlementId,
            ApprovedAtUtc = status == FinancialAdjustmentStatus.Approved ? Utc(2026, 11, 9) : null, RowVersion = Guid.NewGuid().ToByteArray(),
        }.WithId(++_nextId);
        Adjustments.Add(a);
        return a;
    }

    // ===== ISettlementQueries =====

    public Task<PagedResult<SettlementSummaryDto>> ListAsync(SettlementListFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var all = SettlementReadShapes.Filter(Settlements.AsQueryable(), filter).ToList();
        return Task.FromResult(new PagedResult<SettlementSummaryDto>(
            [.. all.Skip((page - 1) * pageSize).Take(pageSize).Select(SettlementReviewRules.Summary)], page, pageSize, all.Count));
    }

    public Task<IReadOnlyList<SettlementSummaryDto>> ListAllAsync(SettlementListFilter filter, int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SettlementSummaryDto>>(
            [.. SettlementReadShapes.Filter(Settlements.AsQueryable(), filter).Take(take).AsEnumerable().Select(SettlementReviewRules.Summary)]);

    public Task<SettlementStatsDto> GetStatsAsync(DateOnly? periodStart, CancellationToken cancellationToken)
        => Task.FromResult(SettlementStats.From(SettlementReadShapes.TotalsByStatus(Settlements.AsQueryable(), periodStart).ToList()));

    public Task<Settlement?> FindAsync(int settlementId, CancellationToken cancellationToken)
        => Task.FromResult(Settlements.FirstOrDefault(s => s.Id == settlementId));

    public Task<IReadOnlyList<SettlementLineTotalDto>> SumLinesByTypeAsync(int settlementId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SettlementLineTotalDto>>([.. SettlementReadShapes.LineTotals(Lines, settlementId).AsEnumerable()
            .OrderBy(t => t.LineType).Select(t => new SettlementLineTotalDto(t.LineType.ToString(), t.Count, t.Amount, t.CommissionAmount))]);

    public Task<PagedResult<SettlementLineDto>> ListLinesAsync(int settlementId, SettlementLineType? lineType, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var all = SettlementReadShapes.Lines(Lines, settlementId, lineType).ToList();
        return Task.FromResult(new PagedResult<SettlementLineDto>([.. all.Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.ToDto())],
            page, pageSize, all.Count));
    }

    public Task<IReadOnlyList<SettlementLineDto>> ListAllLinesAsync(int settlementId, int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SettlementLineDto>>([.. SettlementReadShapes.Lines(Lines, settlementId, null).Take(take).AsEnumerable()
            .Select(r => r.ToDto())]);

    public Task<IReadOnlyList<SettlementPeriodDto>> ListPeriodsAsync(int take, DateTime nowUtc, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SettlementPeriodDto>>([.. SettlementReadShapes.Periods(Periods.AsQueryable(), Settlements.AsQueryable(), take)
            .AsEnumerable().Select(p => new SettlementPeriodDto(p.Id, p.PeriodStart, p.PeriodEnd, p.Cycle,
                SettlementCalendar.HasEnded(p.PeriodEnd, nowUtc), p.SettlementCount))]);

    public Task<PagedResult<FinancialAdjustmentDto>> ListAdjustmentsAsync(AdjustmentListFilter filter, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var all = SettlementReadShapes.Adjustments(Adjustments.AsQueryable(), Settlements.AsQueryable(), filter).ToList();
        return Task.FromResult(new PagedResult<FinancialAdjustmentDto>(
            [.. all.Skip((page - 1) * pageSize).Take(pageSize).Select(r => SettlementReviewRules.Adjustment(r.Adjustment, r.SettlementCode))],
            page, pageSize, all.Count));
    }

    // ===== ISettlementReviewRepository =====

    public Task<Settlement?> FindTrackedAsync(int settlementId, CancellationToken cancellationToken) => FindAsync(settlementId, cancellationToken);

    public Task SaveAsync(Settlement settlement, byte[] expectedRowVersion, OutboxMessage audit, CancellationToken cancellationToken)
    {
        Stamp(settlement.RowVersion, expectedRowVersion, v => settlement.RowVersion = v);
        Outbox.Add(audit);
        return Task.CompletedTask;
    }

    public Task<FinancialAdjustment?> FindAdjustmentTrackedAsync(int adjustmentId, CancellationToken cancellationToken)
        => Task.FromResult(Adjustments.FirstOrDefault(a => a.Id == adjustmentId));

    public Task<bool> IsReversedAsync(int adjustmentId, CancellationToken cancellationToken)
        => Task.FromResult(Adjustments.Any(a => a.ReversesAdjustmentId == adjustmentId && a.Status != FinancialAdjustmentStatus.Rejected));

    public Task AddAdjustmentAsync(FinancialAdjustment adjustment, Func<FinancialAdjustment, OutboxMessage> audit, CancellationToken cancellationToken)
    {
        adjustment.WithId(++_nextId);
        adjustment.RowVersion = Guid.NewGuid().ToByteArray();
        Adjustments.Add(adjustment);
        Outbox.Add(audit(adjustment));
        return Task.CompletedTask;
    }

    public Task SaveAdjustmentAsync(FinancialAdjustment adjustment, byte[] expectedRowVersion, OutboxMessage audit, CancellationToken cancellationToken)
    {
        Stamp(adjustment.RowVersion, expectedRowVersion, v => adjustment.RowVersion = v);
        Outbox.Add(audit);
        return Task.CompletedTask;
    }

    /// <summary>Mô phỏng UPDATE … WHERE RowVersion = bản đã đọc.</summary>
    private static void Stamp(byte[] current, byte[] expected, Action<byte[]> set)
    {
        if (!current.AsSpan().SequenceEqual(expected)) throw new ConflictException("Dữ liệu đã thay đổi, hãy tải lại.");
        set(Guid.NewGuid().ToByteArray());
    }
}
