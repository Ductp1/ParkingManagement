using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;

namespace PaymentService.Test;

// US-080: biên kỳ quyết toán theo ICT cho 3 chu kỳ (hàm thuần).
public class SettlementCalendarTests
{
    private static readonly DateOnly Anchor = new(2026, 1, 5);   // Thứ Hai

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Theory]
    [InlineData(2026, 11, 9, 2026, 11, 9, 2026, 11, 15)]      // Thứ Hai → chính nó .. Chủ nhật
    [InlineData(2026, 11, 10, 2026, 11, 9, 2026, 11, 15)]     // Thứ Ba
    [InlineData(2026, 11, 15, 2026, 11, 9, 2026, 11, 15)]     // Chủ nhật vẫn thuộc tuần bắt đầu Thứ Hai trước đó
    [InlineData(2026, 12, 31, 2026, 12, 28, 2027, 1, 3)]      // tuần vắt qua năm
    public void Weekly_MondayToSunday(int y, int m, int d, int sy, int sm, int sd, int ey, int em, int ed)
    {
        Assert.Equal(D(sy, sm, sd), SettlementCalendar.BlockStart(SettlementCycle.Weekly, D(y, m, d), Anchor));
        Assert.Equal(D(ey, em, ed), SettlementCalendar.BlockEnd(SettlementCycle.Weekly, D(y, m, d), Anchor));
    }

    [Theory]
    [InlineData(2026, 1, 5, 2026, 1, 5, 2026, 1, 18)]         // đúng ngày gốc
    [InlineData(2026, 1, 18, 2026, 1, 5, 2026, 1, 18)]        // ngày cuối của khối 14 ngày
    [InlineData(2026, 1, 19, 2026, 1, 19, 2026, 2, 1)]        // khối kế tiếp
    [InlineData(2026, 11, 10, 2026, 11, 9, 2026, 11, 22)]     // 308 ngày sau gốc = 22 khối
    [InlineData(2026, 1, 4, 2025, 12, 22, 2026, 1, 4)]        // trước ngày gốc: chia lấy phần nguyên xuống
    public void Biweekly_FourteenDaysFromAnchor(int y, int m, int d, int sy, int sm, int sd, int ey, int em, int ed)
    {
        Assert.Equal(D(sy, sm, sd), SettlementCalendar.BlockStart(SettlementCycle.Biweekly, D(y, m, d), Anchor));
        Assert.Equal(D(ey, em, ed), SettlementCalendar.BlockEnd(SettlementCycle.Biweekly, D(y, m, d), Anchor));
    }

    [Theory]
    [InlineData(2026, 11, 10, 2026, 11, 30)]
    [InlineData(2026, 2, 1, 2026, 2, 28)]
    [InlineData(2028, 2, 15, 2028, 2, 29)]                    // năm nhuận
    public void Monthly_FirstToLastDay(int y, int m, int d, int ey, int em, int ed)
    {
        Assert.Equal(D(y, m, 1), SettlementCalendar.BlockStart(SettlementCycle.Monthly, D(y, m, d), Anchor));
        Assert.Equal(D(ey, em, ed), SettlementCalendar.BlockEnd(SettlementCycle.Monthly, D(y, m, d), Anchor));
    }

    [Fact]
    public void Cutoff_IsMidnightIctAfterLastDay()
    {
        // Chủ nhật 15/11 24:00 ICT = 17:00Z cùng ngày.
        Assert.Equal(new DateTime(2026, 11, 15, 17, 0, 0, DateTimeKind.Utc), SettlementCalendar.CutoffUtc(D(2026, 11, 15)));
        Assert.Equal(new DateTime(2026, 11, 8, 17, 0, 0, DateTimeKind.Utc), SettlementCalendar.StartOfDayUtc(D(2026, 11, 9)));
    }

    [Fact]
    public void HasEnded_SwitchesExactlyAtIctMidnight()
    {
        Assert.False(SettlementCalendar.HasEnded(D(2026, 11, 15), new DateTime(2026, 11, 15, 16, 59, 59, DateTimeKind.Utc)));
        Assert.True(SettlementCalendar.HasEnded(D(2026, 11, 15), new DateTime(2026, 11, 15, 17, 0, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData("WEEKLY", SettlementCycle.Weekly)]
    [InlineData(" biweekly ", SettlementCycle.Biweekly)]
    [InlineData("Monthly", SettlementCycle.Monthly)]
    public void Parse_IsCaseInsensitive(string value, SettlementCycle expected)
        => Assert.Equal(expected, SettlementCalendar.Parse(value));

    [Theory]
    [InlineData("DAILY")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_Unknown_Throws(string? value)
        => Assert.Throws<FormatException>(() => SettlementCalendar.Parse(value));

    [Fact]
    public void SettlementCode_IsStartDateAndOwner()
        => Assert.Equal("STL-20261102-0007", SettlementCalendar.SettlementCode(D(2026, 11, 2), 7));

    [Fact]
    public void Period_EndsAtBlockEndOfItsStart_PartialAfterCycleChange()
    {
        var now = new DateTime(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc);
        // Đổi sang MONTHLY sau một kỳ tuần kết thúc 25/10 → kỳ tháng đầu tiên chỉ 26/10 – 31/10.
        var partial = SettlementPeriod.Create(D(2026, 10, 26), SettlementCycle.Monthly, null, Anchor, now);
        Assert.Equal(D(2026, 10, 31), partial.PeriodEnd);
        Assert.Equal("MONTHLY", partial.Cycle);
        Assert.Equal(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), partial.CutoffUtc);
    }
}
