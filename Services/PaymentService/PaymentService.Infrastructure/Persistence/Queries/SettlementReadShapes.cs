using System.Globalization;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Settlements;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Queries;

/// <summary>Dòng chi tiết đọc từ database (LineType còn là enum – đổi sang chuỗi ở bộ nhớ).</summary>
public sealed record SettlementLineRow(int Id, SettlementLineType LineType, int? PaymentId, int? RefundId, int ParkingLotId, string? ReferenceCode,
    decimal Amount, decimal CommissionAmount, bool IsOwnerCollected, DateTime? OccurredAtUtc, string? BlockedReason)
{
    public SettlementLineDto ToDto() => new(Id, LineType.ToString(), PaymentId, RefundId, ParkingLotId, ReferenceCode, Amount, CommissionAmount,
        IsOwnerCollected, OccurredAtUtc, BlockedReason);
}

public sealed record SettlementLineTotalRow(SettlementLineType LineType, int Count, decimal Amount, decimal CommissionAmount);

public sealed record SettlementStatusTotalRow(SettlementStatus Status, int Count, int Blocked, decimal NetPayout, decimal PositiveNetPayout);

public sealed record AdjustmentRow(FinancialAdjustment Adjustment, string? SettlementCode);

/// <summary>
/// US-080: hình dạng truy vấn đọc kỳ quyết toán / bút toán (lọc, đếm, GROUP BY ở database). Viết trên IQueryable để test chạy đúng logic
/// trong bộ nhớ và kiểm tra SQL bằng ToQueryString.
/// </summary>
public static class SettlementReadShapes
{
    public static IQueryable<Settlement> Filter(IQueryable<Settlement> settlements, SettlementListFilter filter)
    {
        var query = settlements;
        if (filter.Status is { } status) query = query.Where(s => s.Status == status);
        if (filter.Statuses is { Count: > 0 } statuses) query = query.Where(s => statuses.Contains(s.Status));
        if (filter.OwnerProfileId is { } owner) query = query.Where(s => s.OwnerProfileId == owner);
        if (filter.PeriodStart is { } start) query = query.Where(s => s.PeriodStart == start);
        if (filter.Blocked is { } blocked) query = query.Where(s => s.IsBlocked == blocked);
        if (filter.Search is { } search)
        {
            var code = search.ToUpperInvariant();
            query = int.TryParse(search, NumberStyles.None, CultureInfo.InvariantCulture, out var ownerId)
                ? query.Where(s => s.OwnerProfileId == ownerId)                         // số → chủ bãi (mã kỳ luôn có "STL-")
                : query.Where(s => s.Code.Contains(code));
        }
        return query.OrderByDescending(s => s.PeriodStart).ThenBy(s => s.OwnerProfileId).ThenByDescending(s => s.Id);
    }

    public static IQueryable<SettlementStatusTotalRow> TotalsByStatus(IQueryable<Settlement> settlements, DateOnly? periodStart)
        => (periodStart is { } start ? settlements.Where(s => s.PeriodStart == start) : settlements)
            .GroupBy(s => s.Status)
            .Select(g => new SettlementStatusTotalRow(g.Key, g.Count(), g.Count(s => s.IsBlocked), g.Sum(s => s.NetPayout),
                g.Sum(s => s.NetPayout > 0 ? s.NetPayout : 0)));

    public static IQueryable<SettlementLineTotalRow> LineTotals(IQueryable<SettlementLine> lines, int settlementId)
        => lines.Where(l => l.SettlementId == settlementId)
            .GroupBy(l => l.LineType)
            .Select(g => new SettlementLineTotalRow(g.Key, g.Count(), g.Sum(l => l.Amount), g.Sum(l => l.CommissionAmount)));

    public static IQueryable<SettlementLineRow> Lines(IQueryable<SettlementLine> lines, int settlementId, SettlementLineType? lineType)
    {
        var query = lines.Where(l => l.SettlementId == settlementId);
        if (lineType is { } type) query = query.Where(l => l.LineType == type);
        return query.OrderBy(l => l.LineType).ThenBy(l => l.OccurredAtUtc).ThenBy(l => l.Id)
            .Select(l => new SettlementLineRow(l.Id, l.LineType, l.PaymentId, l.RefundId, l.ParkingLotId, l.ReferenceCode, l.Amount,
                l.CommissionAmount, l.IsOwnerCollected, l.OccurredAtUtc, l.BlockedReason));
    }

    public static IQueryable<AdjustmentRow> Adjustments(IQueryable<FinancialAdjustment> adjustments, IQueryable<Settlement> settlements,
        AdjustmentListFilter filter)
    {
        var query = adjustments;
        if (filter.OwnerProfileId is { } owner) query = query.Where(a => a.OwnerProfileId == owner);
        if (filter.Status is { } status) query = query.Where(a => a.Status == status);
        if (filter.Attached is { } attached) query = attached ? query.Where(a => a.SettlementId != null) : query.Where(a => a.SettlementId == null);
        if (filter.SettlementId is { } settlementId) query = query.Where(a => a.SettlementId == settlementId);
        if (filter.Search is { } search) query = query.Where(a => a.Reason.Contains(search) || a.SourceReference == search);

        return from a in query
               join s in settlements on a.SettlementId equals (int?)s.Id into attachedTo
               from s in attachedTo.DefaultIfEmpty()
               orderby a.Id descending
               select new AdjustmentRow(a, s == null ? null : s.Code);
    }

    public static IQueryable<SettlementPeriodCountRow> Periods(IQueryable<SettlementPeriod> periods, IQueryable<Settlement> settlements, int take)
        => periods.OrderByDescending(p => p.PeriodStart).Take(take)
            .Select(p => new SettlementPeriodCountRow(p.Id, p.PeriodStart, p.PeriodEnd, p.Cycle, settlements.Count(s => s.SettlementPeriodId == p.Id)));
}

public sealed record SettlementPeriodCountRow(int Id, DateOnly PeriodStart, DateOnly PeriodEnd, string Cycle, int SettlementCount);
