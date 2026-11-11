namespace PaymentService.Domain.Services;

/// <summary>Chu kỳ quyết toán (tham số SETTLEMENT_CYCLE của AdminService).</summary>
public enum SettlementCycle { Weekly = 1, Biweekly = 2, Monthly = 3 }

/// <summary>
/// US-080: lịch kỳ quyết toán theo ngày giờ Việt Nam (ICT, UTC+7, không có giờ mùa hè). Hàm thuần – không đọc đồng hồ, không đọc database.
/// Một "khối" của chu kỳ: WEEKLY = Thứ Hai → Chủ nhật; BIWEEKLY = 14 ngày tính từ ngày gốc (Thứ Hai) cấu hình; MONTHLY = ngày 1 → cuối tháng.
/// PeriodEnd là ngày CUỐI CÙNG của kỳ (bao gồm) – khớp US-078 (PeriodEnd &lt; to); mốc chốt = 00:00 ICT của ngày sau PeriodEnd.
/// </summary>
public static class SettlementCalendar
{
    public static readonly TimeSpan IctOffset = TimeSpan.FromHours(7);

    public static SettlementCycle Parse(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "WEEKLY" => SettlementCycle.Weekly,
        "BIWEEKLY" => SettlementCycle.Biweekly,
        "MONTHLY" => SettlementCycle.Monthly,
        _ => throw new FormatException($"SETTLEMENT_CYCLE '{value}' không phải WEEKLY, BIWEEKLY hoặc MONTHLY."),
    };

    public static string Format(SettlementCycle cycle) => cycle switch
    {
        SettlementCycle.Weekly => "WEEKLY",
        SettlementCycle.Biweekly => "BIWEEKLY",
        SettlementCycle.Monthly => "MONTHLY",
        _ => throw new ArgumentOutOfRangeException(nameof(cycle)),
    };

    /// <summary>Ngày đầu của khối chứa <paramref name="day"/>.</summary>
    public static DateOnly BlockStart(SettlementCycle cycle, DateOnly day, DateOnly biweeklyAnchor) => cycle switch
    {
        SettlementCycle.Weekly => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
        SettlementCycle.Biweekly => biweeklyAnchor.AddDays(FloorDiv(day.DayNumber - biweeklyAnchor.DayNumber, 14) * 14),
        SettlementCycle.Monthly => new DateOnly(day.Year, day.Month, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(cycle)),
    };

    /// <summary>Ngày cuối (bao gồm) của khối chứa <paramref name="day"/>.</summary>
    public static DateOnly BlockEnd(SettlementCycle cycle, DateOnly day, DateOnly biweeklyAnchor) => cycle switch
    {
        SettlementCycle.Weekly => BlockStart(cycle, day, biweeklyAnchor).AddDays(6),
        SettlementCycle.Biweekly => BlockStart(cycle, day, biweeklyAnchor).AddDays(13),
        SettlementCycle.Monthly => new DateOnly(day.Year, day.Month, DateTime.DaysInMonth(day.Year, day.Month)),
        _ => throw new ArgumentOutOfRangeException(nameof(cycle)),
    };

    public static DateOnly TodayIct(DateTime nowUtc) => DateOnly.FromDateTime(nowUtc + IctOffset);

    /// <summary>00:00 ICT của <paramref name="day"/>, quy ra UTC.</summary>
    public static DateTime StartOfDayUtc(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - IctOffset;

    /// <summary>Mốc chốt của kỳ kết thúc ngày <paramref name="periodEnd"/>: 24:00 ICT ngày đó. Giao dịch / hoàn tiền trước mốc này thuộc kỳ.</summary>
    public static DateTime CutoffUtc(DateOnly periodEnd) => StartOfDayUtc(periodEnd.AddDays(1));

    /// <summary>Kỳ đã kết thúc khi ngày cuối của kỳ đã qua (theo ICT).</summary>
    public static bool HasEnded(DateOnly periodEnd, DateTime nowUtc) => periodEnd < TodayIct(nowUtc);

    /// <summary>Mã kỳ quyết toán của chủ bãi: STL-{ngày đầu kỳ}-{chủ bãi}, duy nhất vì mỗi chủ bãi một kỳ / ngày bắt đầu.</summary>
    public static string SettlementCode(DateOnly periodStart, int ownerProfileId)
        => $"STL-{periodStart:yyyyMMdd}-{ownerProfileId:D4}";

    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
}
