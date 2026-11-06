using ParkingManagement.SharedKernel.Exceptions;

namespace PaymentService.Application.Features.Reports;

// ===== DTO =====
public enum OwnerRevenueGranularity { Hour, Day, Week, Month }

/// <summary>
/// Một điểm của chuỗi thời gian. PeriodStart (bao gồm) / PeriodEnd (loại trừ) theo giờ Việt Nam (+07:00);
/// điểm đầu/cuối của Week/Month được cắt theo from/to nên có thể ngắn hơn 1 tuần / 1 tháng.
/// </summary>
public sealed record OwnerRevenuePointDto(DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, decimal GrossRevenue, decimal RefundAmount,
    decimal NetRevenue, int TransactionCount);

/// <summary>US-078 AC1: doanh thu của chủ bãi theo giờ/ngày/tuần/tháng cho biểu đồ – đủ mọi điểm (kể cả không có giao dịch).</summary>
public sealed record OwnerRevenueSeriesDto(DateOnly From, DateOnly To, int? ParkingLotId, string Granularity, string Currency,
    string RefundBasis, IReadOnlyList<OwnerRevenuePointDto> Points);

/// <summary>US-078: quy tắc riêng của chuỗi thời gian (bộ lọc chung ở <see cref="OwnerRevenueRules"/>).</summary>
public static class OwnerRevenueSeriesRules
{
    /// <summary>Theo giờ tối đa 7 ngày = 168 điểm.</summary>
    public const int MaxHourRangeDays = 7;

    /// <summary>
    /// Bỏ trống → Day. Chỉ nhận đúng tên (không phân biệt hoa thường): không dùng Enum.TryParse vì nó nhận cả số ("1")
    /// và chuỗi kiểu cờ ("Day,Week" → Day|Week = Month).
    /// </summary>
    public static OwnerRevenueGranularity ParseGranularity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return OwnerRevenueGranularity.Day;
        foreach (var granularity in Enum.GetValues<OwnerRevenueGranularity>())
            if (string.Equals(granularity.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                return granularity;
        throw new ValidationException("granularity phải là Hour, Day, Week hoặc Month.");
    }

    /// <summary>Các khoảng [start, end) liên tiếp phủ kín [from, to) theo lịch Việt Nam. Tuần bắt đầu thứ Hai.</summary>
    public static IEnumerable<(DateOnly Start, DateOnly End)> DayRanges(DateOnly from, DateOnly to, OwnerRevenueGranularity granularity)
    {
        for (var start = from; start < to;)
        {
            var next = granularity switch
            {
                OwnerRevenueGranularity.Week => start.AddDays(7 - ((int)start.DayOfWeek + 6) % 7),
                OwnerRevenueGranularity.Month => new DateOnly(start.Year, start.Month, 1).AddMonths(1),
                _ => start.AddDays(1),
            };
            var end = next < to ? next : to;
            yield return (start, end);
            start = end;
        }
    }

    public static DateTimeOffset IctTime(DateOnly day, int hour = 0)
        => new(day.ToDateTime(new TimeOnly(hour, 0)), OwnerRevenueRules.IctOffset);
}

// ===== USE CASE (đọc) =====
public interface IGetOwnerRevenueSeriesUseCase
{
    Task<OwnerRevenueSeriesDto> ExecuteAsync(int? ownerProfileId, OwnerRevenueQuery query, string? granularity,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-078 (UC-35) AC1: chuỗi doanh thu theo giờ/ngày/tuần/tháng cho biểu đồ. Database GROUP BY theo ngày (hoặc ngày + giờ) Việt Nam;
/// tuần/tháng gộp từ tối đa 366 dòng ngày. Cùng bộ lọc, cùng cách tính hoàn tiền với summary nên tổng các điểm = totals của summary.
/// </summary>
public sealed class GetOwnerRevenueSeriesUseCase(IOwnerRevenueQueries queries, TimeProvider timeProvider) : IGetOwnerRevenueSeriesUseCase
{
    public async Task<OwnerRevenueSeriesDto> ExecuteAsync(int? ownerProfileId, OwnerRevenueQuery query, string? granularity,
        CancellationToken cancellationToken = default)
    {
        var filter = OwnerRevenueRules.Normalize(ownerProfileId, query, timeProvider.GetUtcNow().UtcDateTime);
        var unit = OwnerRevenueSeriesRules.ParseGranularity(granularity);
        if (unit == OwnerRevenueGranularity.Hour && filter.To.DayNumber - filter.From.DayNumber > OwnerRevenueSeriesRules.MaxHourRangeDays)
            throw new ValidationException(
                $"granularity=Hour chỉ áp dụng cho khoảng tối đa {OwnerRevenueSeriesRules.MaxHourRangeDays} ngày; hãy truyền from/to ngắn hơn.");

        var points = unit == OwnerRevenueGranularity.Hour
            ? await HourPointsAsync(filter, cancellationToken)
            : await DayBasedPointsAsync(filter, unit, cancellationToken);

        return new OwnerRevenueSeriesDto(filter.From, filter.To, filter.ParkingLotId, unit.ToString(), OwnerRevenueRules.Currency,
            OwnerRevenueRules.RefundBasis, points);
    }

    private async Task<IReadOnlyList<OwnerRevenuePointDto>> DayBasedPointsAsync(OwnerRevenueFilter filter, OwnerRevenueGranularity unit,
        CancellationToken cancellationToken)
    {
        var rows = await queries.SumByVietnamDayAsync(filter, cancellationToken);
        var byDay = rows.ToLookup(r => new DateOnly(r.Year, r.Month, r.Day));

        return OwnerRevenueSeriesRules.DayRanges(filter.From, filter.To, unit)
            .Select(range =>
            {
                var inRange = byDay.Where(g => g.Key >= range.Start && g.Key < range.End).SelectMany(g => g).ToList();
                return Point(OwnerRevenueSeriesRules.IctTime(range.Start), OwnerRevenueSeriesRules.IctTime(range.End),
                    inRange.Sum(r => r.GrossAmount), inRange.Sum(r => r.RefundedAmount), inRange.Sum(r => r.TransactionCount));
            })
            .ToList();
    }

    private async Task<IReadOnlyList<OwnerRevenuePointDto>> HourPointsAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken)
    {
        var rows = await queries.SumByVietnamDayHourAsync(filter, cancellationToken);
        var byHour = rows.ToDictionary(r => (new DateOnly(r.Year, r.Month, r.Day), r.HourOfDay));

        return OwnerRevenueSeriesRules.DayRanges(filter.From, filter.To, OwnerRevenueGranularity.Day)
            .SelectMany(day => Enumerable.Range(0, 24).Select(hour =>
            {
                var start = OwnerRevenueSeriesRules.IctTime(day.Start, hour);
                return byHour.TryGetValue((day.Start, hour), out var r)
                    ? Point(start, start.AddHours(1), r.GrossAmount, r.RefundedAmount, r.TransactionCount)
                    : Point(start, start.AddHours(1), 0, 0, 0);
            }))
            .ToList();
    }

    private static OwnerRevenuePointDto Point(DateTimeOffset start, DateTimeOffset end, decimal gross, decimal refund, int count)
        => new(start, end, OwnerRevenueRules.Vnd(gross), OwnerRevenueRules.Vnd(refund), OwnerRevenueRules.Vnd(gross - refund), count);
}
