using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Settlements;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;

namespace PaymentService.Infrastructure.Persistence.Queries;

/// <summary>US-080: đọc kỳ quyết toán / dòng / bút toán (AsNoTracking, lọc – đếm – tổng ở database).</summary>
public sealed class SettlementQueries(PaymentDbContext db) : ISettlementQueries
{
    public async Task<PagedResult<SettlementSummaryDto>> ListAsync(SettlementListFilter filter, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = SettlementReadShapes.Filter(db.Settlements.AsNoTracking(), filter);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Include(s => s.Period).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<SettlementSummaryDto>([.. items.Select(SettlementReviewRules.Summary)], page, pageSize, total);
    }

    public async Task<IReadOnlyList<SettlementSummaryDto>> ListAllAsync(SettlementListFilter filter, int take, CancellationToken cancellationToken)
        => [.. (await SettlementReadShapes.Filter(db.Settlements.AsNoTracking(), filter).Include(s => s.Period).Take(take)
            .ToListAsync(cancellationToken)).Select(SettlementReviewRules.Summary)];

    public async Task<SettlementStatsDto> GetStatsAsync(DateOnly? periodStart, CancellationToken cancellationToken)
        => SettlementStats.From(await SettlementReadShapes.TotalsByStatus(db.Settlements.AsNoTracking(), periodStart).ToListAsync(cancellationToken));

    public Task<Settlement?> FindAsync(int settlementId, CancellationToken cancellationToken)
        => db.Settlements.AsNoTracking().Include(s => s.Period).FirstOrDefaultAsync(s => s.Id == settlementId, cancellationToken);

    public async Task<IReadOnlyList<SettlementLineTotalDto>> SumLinesByTypeAsync(int settlementId, CancellationToken cancellationToken)
        => [.. (await SettlementReadShapes.LineTotals(db.SettlementLines.AsNoTracking(), settlementId).ToListAsync(cancellationToken))
            .OrderBy(t => t.LineType).Select(t => new SettlementLineTotalDto(t.LineType.ToString(), t.Count, t.Amount, t.CommissionAmount))];

    public async Task<PagedResult<SettlementLineDto>> ListLinesAsync(int settlementId, SettlementLineType? lineType, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = SettlementReadShapes.Lines(db.SettlementLines.AsNoTracking(), settlementId, lineType);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<SettlementLineDto>([.. rows.Select(r => r.ToDto())], page, pageSize, total);
    }

    public async Task<IReadOnlyList<SettlementLineDto>> ListAllLinesAsync(int settlementId, int take, CancellationToken cancellationToken)
        => [.. (await SettlementReadShapes.Lines(db.SettlementLines.AsNoTracking(), settlementId, null).Take(take).ToListAsync(cancellationToken))
            .Select(r => r.ToDto())];

    public async Task<IReadOnlyList<SettlementPeriodDto>> ListPeriodsAsync(int take, DateTime nowUtc, CancellationToken cancellationToken)
        => [.. (await SettlementReadShapes.Periods(db.SettlementPeriods.AsNoTracking(), db.Settlements.AsNoTracking(), take)
                .ToListAsync(cancellationToken))
            .Select(p => new SettlementPeriodDto(p.Id, p.PeriodStart, p.PeriodEnd, p.Cycle, SettlementCalendar.HasEnded(p.PeriodEnd, nowUtc),
                p.SettlementCount))];

    public async Task<PagedResult<FinancialAdjustmentDto>> ListAdjustmentsAsync(AdjustmentListFilter filter, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = SettlementReadShapes.Adjustments(db.FinancialAdjustments.AsNoTracking(), db.Settlements.AsNoTracking(), filter);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<FinancialAdjustmentDto>([.. rows.Select(r => SettlementReviewRules.Adjustment(r.Adjustment, r.SettlementCode))],
            page, pageSize, total);
    }
}

public static class SettlementStats
{
    public static SettlementStatsDto From(IReadOnlyList<SettlementStatusTotalRow> rows)
    {
        SettlementStatusTotalRow? Of(SettlementStatus status) => rows.FirstOrDefault(r => r.Status == status);
        return new SettlementStatsDto(Of(SettlementStatus.Draft)?.Count ?? 0, Of(SettlementStatus.PendingReview)?.Count ?? 0,
            Of(SettlementStatus.Approved)?.Count ?? 0, Of(SettlementStatus.Paid)?.Count ?? 0, rows.Sum(r => r.Blocked),
            Of(SettlementStatus.PendingReview)?.NetPayout ?? 0, Of(SettlementStatus.Approved)?.PositiveNetPayout ?? 0);
    }
}
