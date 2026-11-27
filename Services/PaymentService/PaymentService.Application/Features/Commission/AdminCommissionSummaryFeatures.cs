using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Reports;

namespace PaymentService.Application.Features.Commission;

// ===== DTO =====

/// <summary>
/// Tổng của một nhóm. Net = Gross − Refunded; EffectiveRate = Commission / Net (PHÂN SỐ, 6 chữ số), null khi Net = 0.
/// PendingCount &gt; 0 = còn giao dịch chờ job tính → Commission CHƯA đủ (không phải 0).
/// </summary>
public sealed record AdminCommissionTotalsDto(decimal GrossAmount, decimal RefundedAmount, decimal NetAmount, decimal CommissionAmount,
    decimal? EffectiveRate, int TransactionCount, int PendingCount, string Status);

public sealed record AdminCommissionByLotDto(int ParkingLotId, int OwnerProfileId, decimal GrossAmount, decimal RefundedAmount, decimal NetAmount,
    decimal CommissionAmount, decimal? EffectiveRate, int TransactionCount, int PendingCount, string Status, IReadOnlyList<string> RateSources);

public sealed record AdminCommissionByOwnerDto(int OwnerProfileId, int LotCount, decimal GrossAmount, decimal RefundedAmount, decimal NetAmount,
    decimal CommissionAmount, decimal? EffectiveRate, int TransactionCount, int PendingCount, string Status);

/// <summary>Theo nguồn tỷ lệ của Accrual (LotContract / DefaultConfig) – chỉ giao dịch đã tính.</summary>
public sealed record AdminCommissionByRateSourceDto(string Source, int TransactionCount, decimal BaseAmount, decimal RefundedAmount,
    decimal CommissionAmount, decimal? EffectiveRate);

/// <summary>Một ngày theo lịch Việt Nam (đủ mọi ngày trong khoảng, kể cả ngày không có giao dịch) để vẽ biểu đồ.</summary>
public sealed record AdminCommissionDayDto(DateOnly Date, decimal GrossAmount, decimal NetAmount, decimal CommissionAmount, int TransactionCount,
    int PendingCount);

/// <summary>
/// US-079: hoa hồng toàn sàn cho Admin trong [FromUtc, ToUtc) theo PaidAtUtc của giao dịch gốc. Chỉ số tổng hợp theo bãi / chủ bãi /
/// nguồn tỷ lệ / ngày – không có dữ liệu từng khách (US-081 AC2).
/// </summary>
public sealed record AdminCommissionSummaryDto(DateTime AsOfUtc, DateTime FromUtc, DateTime ToUtc, string Currency, AdminCommissionTotalsDto Totals,
    IReadOnlyList<AdminCommissionByLotDto> ByLot, IReadOnlyList<AdminCommissionByOwnerDto> ByOwner,
    IReadOnlyList<AdminCommissionByRateSourceDto> ByRateSource, IReadOnlyList<AdminCommissionDayDto> Series);

// ===== PORT (đọc) – GROUP BY / SUM ở database =====

public sealed record AdminRevenueLotRow(int ParkingLotId, int OwnerProfileId, decimal GrossAmount, decimal RefundedAmount, int TransactionCount);

public sealed record AdminCommissionLotSourceRow(int ParkingLotId, int OwnerProfileId, string RateSource, decimal CommissionAmount,
    decimal AccruedBase, decimal ReversedBase, int AccrualCount);

public sealed record AdminPendingLotDayRow(int ParkingLotId, int OwnerProfileId, int Year, int Month, int Day, int PendingCount);

public sealed record AdminRevenueDayRow(int Year, int Month, int Day, decimal GrossAmount, decimal RefundedAmount, int TransactionCount);

public sealed record AdminCommissionDayRow(int Year, int Month, int Day, decimal CommissionAmount);

public interface IAdminCommissionQueries
{
    /// <summary>Doanh thu theo (bãi, chủ bãi) của giao dịch đã thu tiền trong khoảng.</summary>
    Task<IReadOnlyList<AdminRevenueLotRow>> SumRevenueByLotAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);

    /// <summary>Hoa hồng theo (bãi, chủ bãi, nguồn tỷ lệ).</summary>
    Task<IReadOnlyList<AdminCommissionLotSourceRow>> SumCommissionByLotSourceAsync(DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken);

    /// <summary>Giao dịch còn chờ tính theo (bãi, chủ bãi, ngày Việt Nam).</summary>
    Task<IReadOnlyList<AdminPendingLotDayRow>> CountPendingByLotDayAsync(DateTime fromUtc, DateTime toUtc,
        IReadOnlyCollection<PaymentPurpose> excludedPurposes, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminRevenueDayRow>> SumRevenueByDayAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminCommissionDayRow>> SumCommissionByDayAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}

// ===== USE CASE =====

public interface IGetAdminCommissionSummaryUseCase
{
    Task<AdminCommissionSummaryDto> ExecuteAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-079: tổng hợp hoa hồng toàn sàn cho trang Tài chính → Hoa hồng của Admin portal. Bắt buộc fromUtc &lt; toUtc, tối đa 366 ngày.
/// Giá trị không ghi múi giờ được hiểu là UTC. Chỉ đọc.
/// </summary>
public sealed class GetAdminCommissionSummaryUseCase(IAdminCommissionQueries queries, TimeProvider timeProvider, CommissionPolicy? policy = null)
    : IGetAdminCommissionSummaryUseCase
{
    public const int MaxRangeDays = 366;
    private readonly CommissionPolicy policy = policy ?? CommissionPolicy.Default;

    public async Task<AdminCommissionSummaryDto> ExecuteAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var (from, to) = Validate(fromUtc, toUtc);

        var revenue = await queries.SumRevenueByLotAsync(from, to, cancellationToken);
        var commission = await queries.SumCommissionByLotSourceAsync(from, to, cancellationToken);
        var pending = await queries.CountPendingByLotDayAsync(from, to, policy.ExcludedPurposes, cancellationToken);
        var revenueByDay = await queries.SumRevenueByDayAsync(from, to, cancellationToken);
        var commissionByDay = await queries.SumCommissionByDayAsync(from, to, cancellationToken);

        var lots = revenue.Select(r => (r.ParkingLotId, r.OwnerProfileId))
            .Union(commission.Select(c => (c.ParkingLotId, c.OwnerProfileId)))
            .Union(pending.Select(p => (p.ParkingLotId, p.OwnerProfileId)))
            .Select(key =>
            {
                var rev = revenue.Where(r => r.ParkingLotId == key.ParkingLotId && r.OwnerProfileId == key.OwnerProfileId).ToList();
                var com = commission.Where(c => c.ParkingLotId == key.ParkingLotId && c.OwnerProfileId == key.OwnerProfileId).ToList();
                var pend = pending.Where(p => p.ParkingLotId == key.ParkingLotId && p.OwnerProfileId == key.OwnerProfileId).Sum(p => p.PendingCount);
                var t = Totals(rev.Sum(r => r.GrossAmount), rev.Sum(r => r.RefundedAmount), com.Sum(c => c.CommissionAmount),
                    rev.Sum(r => r.TransactionCount), pend);
                return new AdminCommissionByLotDto(key.ParkingLotId, key.OwnerProfileId, t.GrossAmount, t.RefundedAmount, t.NetAmount,
                    t.CommissionAmount, t.EffectiveRate, t.TransactionCount, t.PendingCount, t.Status,
                    [.. com.Select(c => c.RateSource).Distinct().Order(StringComparer.Ordinal)]);
            })
            .OrderByDescending(l => l.CommissionAmount).ThenByDescending(l => l.GrossAmount).ThenBy(l => l.ParkingLotId)
            .ToList();

        var owners = lots.GroupBy(l => l.OwnerProfileId)
            .Select(g =>
            {
                var t = Totals(g.Sum(l => l.GrossAmount), g.Sum(l => l.RefundedAmount), g.Sum(l => l.CommissionAmount),
                    g.Sum(l => l.TransactionCount), g.Sum(l => l.PendingCount));
                return new AdminCommissionByOwnerDto(g.Key, g.Count(), t.GrossAmount, t.RefundedAmount, t.NetAmount, t.CommissionAmount,
                    t.EffectiveRate, t.TransactionCount, t.PendingCount, t.Status);
            })
            .OrderByDescending(o => o.CommissionAmount).ThenByDescending(o => o.GrossAmount).ThenBy(o => o.OwnerProfileId)
            .ToList();

        var bySource = commission.GroupBy(c => c.RateSource)
            .Select(g =>
            {
                var (baseAmount, refunded, amount) = (g.Sum(c => c.AccruedBase), g.Sum(c => c.ReversedBase), g.Sum(c => c.CommissionAmount));
                return new AdminCommissionByRateSourceDto(g.Key, g.Sum(c => c.AccrualCount), Vnd(baseAmount), Vnd(refunded), Vnd(amount),
                    EffectiveRate(amount, baseAmount - refunded));
            })
            .OrderBy(s => s.Source, StringComparer.Ordinal)
            .ToList();

        var totals = Totals(revenue.Sum(r => r.GrossAmount), revenue.Sum(r => r.RefundedAmount), commission.Sum(c => c.CommissionAmount),
            revenue.Sum(r => r.TransactionCount), pending.Sum(p => p.PendingCount));

        return new AdminCommissionSummaryDto(timeProvider.GetUtcNow().UtcDateTime, from, to, OwnerRevenueRules.Currency, totals, lots, owners,
            bySource, Series(from, to, revenueByDay, commissionByDay, pending));
    }

    private static (DateTime From, DateTime To) Validate(DateTime? fromUtc, DateTime? toUtc)
    {
        if (ToUtc(fromUtc) is not { } from || ToUtc(toUtc) is not { } to)
            throw new ValidationException("fromUtc và toUtc là bắt buộc (ISO 8601, UTC).");
        if (from >= to) throw new ValidationException("fromUtc phải nhỏ hơn toUtc.");
        if (to - from > TimeSpan.FromDays(MaxRangeDays)) throw new ValidationException($"Khoảng thời gian tối đa {MaxRangeDays} ngày.");
        return (from, to);
    }

    private static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Unspecified } v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
        { } v => v.ToUniversalTime(),
    };

    /// <summary>Mọi ngày Việt Nam giao với [from, to).</summary>
    private static List<AdminCommissionDayDto> Series(DateTime from, DateTime to, IReadOnlyList<AdminRevenueDayRow> revenue,
        IReadOnlyList<AdminCommissionDayRow> commission, IReadOnlyList<AdminPendingLotDayRow> pending)
    {
        var first = DateOnly.FromDateTime(from + OwnerRevenueRules.IctOffset);
        var last = DateOnly.FromDateTime(to.AddTicks(-1) + OwnerRevenueRules.IctOffset);
        var revenueByDay = revenue.ToDictionary(r => new DateOnly(r.Year, r.Month, r.Day));
        var commissionByDay = commission.GroupBy(c => new DateOnly(c.Year, c.Month, c.Day)).ToDictionary(g => g.Key, g => g.Sum(c => c.CommissionAmount));
        var pendingByDay = pending.GroupBy(p => new DateOnly(p.Year, p.Month, p.Day)).ToDictionary(g => g.Key, g => g.Sum(p => p.PendingCount));

        var days = new List<AdminCommissionDayDto>(last.DayNumber - first.DayNumber + 1);
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var r = revenueByDay.GetValueOrDefault(day);
            days.Add(new AdminCommissionDayDto(day, Vnd(r?.GrossAmount ?? 0), Vnd((r?.GrossAmount ?? 0) - (r?.RefundedAmount ?? 0)),
                Vnd(commissionByDay.GetValueOrDefault(day)), r?.TransactionCount ?? 0, pendingByDay.GetValueOrDefault(day)));
        }
        return days;
    }

    private static AdminCommissionTotalsDto Totals(decimal gross, decimal refunded, decimal commission, int count, int pendingCount)
    {
        var net = gross - refunded;
        return new AdminCommissionTotalsDto(Vnd(gross), Vnd(refunded), Vnd(net), Vnd(commission), EffectiveRate(commission, net), count,
            pendingCount, CommissionStatuses.ForGroup(count, pendingCount));
    }

    private static decimal? EffectiveRate(decimal commission, decimal net)
        => net > 0 ? Math.Round(commission / net, 6, MidpointRounding.AwayFromZero) : null;

    private static decimal Vnd(decimal amount) => OwnerRevenueRules.Vnd(amount);
}
