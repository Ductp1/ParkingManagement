using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Domain.Services;

/// <summary>Một dòng chi tiết biểu giá trong hóa đơn để minh bạch chi phí cho khách hàng.</summary>
public sealed record PriceLine(string Description, int Minutes, int Blocks, decimal UnitPrice, decimal Amount);

/// <summary>Kết quả báo giá chi tiết gồm tiền gốc, hệ số phụ thu và tổng tiền cuối cùng.</summary>
public sealed record PriceQuote(int DurationMinutes, decimal BaseAmount, decimal Multiplier, decimal FinalAmount, bool IsWithinGracePeriod,
    IReadOnlyList<PriceLine> Lines);

/// <summary>
/// Kết quả chốt phí một lượt gửi xe thực tế tại cổng ra (UC-22, US-045 → US-047):
/// tiền gốc theo bậc, phần quá giờ (Overstay) × hệ số, và tổng phải thu.
/// </summary>
public sealed record SessionFeeQuote(int DurationMinutes, bool IsWithinBillingGrace, decimal BaseAmount, decimal Multiplier,
    int OverstayMinutes, decimal OverstayAmount, decimal FinalAmount, IReadOnlyList<PriceLine> Lines);

/// <summary>
/// Công cụ tính giá đỗ xe (SRS v2 F3.1, Đặc tả v3 §4.4, US-070, US-071):
/// - Lượt đỗ ≤ 15 phút (Parking Billing Grace Period) → 0đ.
/// - Giá lũy tiến theo bậc: mỗi bậc tính theo block, block lẻ làm tròn lên.
/// - Phụ thu qua đêm (Overnight Surcharge): cộng cố định nếu đỗ qua khung 22:00–06:00 giờ Việt Nam.
/// - Hệ số ngày lễ (Holiday Multiplier): áp dụng nếu ngày vào là ngày nghỉ lễ quốc gia.
/// - Hệ số cuối tuần (Weekend Multiplier): áp dụng nếu ngày vào là Thứ Bảy hoặc Chủ Nhật (không trùng lễ).
/// - Hệ số xe quá khổ (Oversized Multiplier): áp dụng khi xe quá khổ chưa có bảng giá riêng.
/// - Khống chế trần giá theo ngày (MaxDailyAmount × số ngày).
/// </summary>
public static class PriceCalculator
{
    /// <summary>Thời gian ân hạn miễn phí mặc định là 15 phút đầu tiên.</summary>
    public const int DefaultGracePeriodMinutes = 15;

    /// <summary>
    /// Tính toán báo giá tiền đỗ xe dựa trên biểu giá và khoảng thời gian đỗ.
    /// </summary>
    /// <param name="card">Biểu giá hiệu lực của bãi đỗ xe.</param>
    /// <param name="vehicleType">Phân loại phương tiện gửi xe.</param>
    /// <param name="startUtc">Thời điểm bắt đầu đỗ theo chuẩn giờ quốc tế UTC.</param>
    /// <param name="endUtc">Thời điểm kết thúc đỗ theo chuẩn giờ quốc tế UTC.</param>
    /// <param name="holidays">Tập hợp các ngày nghỉ lễ theo lịch Việt Nam.</param>
    /// <param name="gracePeriodMinutes">Số phút miễn phí khi vào rồi ra ngay (mặc định 15 phút).</param>
    /// <returns>Đối tượng PriceQuote chứa chi tiết các dòng tiền và tổng số tiền phải trả.</returns>
    public static PriceQuote Calculate(
        RateCard card, 
        VehicleType vehicleType, 
        DateTime startUtc, 
        DateTime endUtc,
        IReadOnlyCollection<DateOnly>? holidays = null,
        int gracePeriodMinutes = DefaultGracePeriodMinutes)
    {
        // Ngăn chặn lỗi logic nghiệp vụ khi thời gian gửi lên bị đảo ngược
        if (endUtc <= startUtc) 
            throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.", nameof(endUtc));

        var minutes = (int)Math.Ceiling((endUtc - startUtc).TotalMinutes);
        if (minutes <= gracePeriodMinutes)
            return new PriceQuote(minutes, 0, 1, 0, true, []);

        var ruleType = ResolveRuleType(card, vehicleType);
        var rules = ResolveRules(card, vehicleType, ruleType);
        var (lines, baseAmount) = BuildTieredLines(rules, minutes);

        // Quy đổi về múi giờ thực tế của bãi xe tại Việt Nam để đối chiếu đúng lịch sinh hoạt
        var startVn = startUtc.AddHours(7);
        var endVn = endUtc.AddHours(7);

        // Bù đắp chi phí trực đêm và an ninh cho bãi xe khi xe lưu lại trong khung giờ giới nghiêm 22:00–06:00
        var overnightNights = CountOvernightWindows(startVn, endVn);
        decimal overnightAmount = 0m;
        if (card.OvernightSurcharge > 0 && overnightNights > 0)
        {
            overnightAmount = card.OvernightSurcharge * overnightNights;
            lines.Add(new PriceLine(
                overnightNights == 1
                    ? "Phụ thu qua đêm (22:00–06:00)"
                    : $"Phụ thu qua đêm ({overnightNights} đêm × {card.OvernightSurcharge:N0}đ)",
                minutes,
                overnightNights,
                card.OvernightSurcharge,
                overnightAmount));
        }

        // Ưu tiên chính sách ngày lễ quốc gia trước, nếu không phải lễ mới xét đến ngày nghỉ cuối tuần thông thường
        var multiplier = ResolveEntryMultiplier(card, startVn, holidays);

        // Bù đắp diện tích chiếm dụng vượt tiêu chuẩn khi dùng giá SUV thay thế
        if (vehicleType == VehicleType.Oversized && ruleType != VehicleType.Oversized)
        {
            multiplier *= card.OversizedMultiplier;
        }

        // Khống chế tổng chi phí không vượt quá mức trần bảo vệ quyền lợi người dùng gửi dài ngày
        var final = Math.Round((baseAmount + overnightAmount) * multiplier, 0);
        if (card.MaxDailyAmount is { } cap)
        {
            var dayCount = (int)Math.Ceiling(minutes / 1440.0);
            final = Math.Min(final, cap * dayCount);
        }

        return new PriceQuote(minutes, baseAmount, multiplier, final, false, lines);
    }

    /// <summary>
    /// Chốt phí một lượt gửi xe thực tế tại cổng ra (UC-22, US-045 → US-047):
    /// - Lượt ≤ Billing Grace (15') → miễn phí toàn bộ (US-046).
    /// - Tiền gốc tính theo bậc lũy tiến cho toàn bộ thời gian thực đỗ, cộng phụ thu qua đêm, hệ số lễ/cuối tuần.
    /// - Quá giờ kết thúc đặt + 15' ân hạn → phần quá giờ tính TỪ GIỜ KẾT THÚC theo đơn giá chuẩn ×
    ///   OverstayMultiplier 1.5–2.0 (US-047), không chịu trần giá ngày của phần đặt chỗ.
    /// - bookedEndUtc = null (khách vãng lai) → chỉ tính tiền gốc theo thời gian thực đỗ.
    /// </summary>
    public static SessionFeeQuote CalculateSession(
        RateCard card,
        VehicleType vehicleType,
        DateTime entryUtc,
        DateTime exitUtc,
        DateTime? bookedEndUtc = null,
        int billingGracePeriodMinutes = DefaultGracePeriodMinutes,
        decimal? overstayMultiplier = null,
        IReadOnlyCollection<DateOnly>? holidays = null)
    {
        if (exitUtc <= entryUtc)
            throw new ArgumentException("Giờ ra phải sau giờ vào.", nameof(exitUtc));

        var minutes = (int)Math.Ceiling((exitUtc - entryUtc).TotalMinutes);
        var startVn = entryUtc.AddHours(7);
        var endVn = exitUtc.AddHours(7);

        // US-046: vào rồi ra ngay trong ân hạn → phí 0 VND
        if (minutes <= billingGracePeriodMinutes)
            return new SessionFeeQuote(minutes, true, 0, 1, 0, 0, 0, []);

        var ruleType = ResolveRuleType(card, vehicleType);
        var rules = ResolveRules(card, vehicleType, ruleType);
        var (baseLines, baseAmount) = BuildTieredLines(rules, minutes);
        var lines = new List<PriceLine>(baseLines);

        // Phụ thu qua đêm 22:00–06:00 cho toàn lượt (phần quá giờ tính kèm đơn giá × hệ số riêng)
        var overnightNights = CountOvernightWindows(startVn, endVn);
        decimal overnightAmount = 0m;
        if (card.OvernightSurcharge > 0 && overnightNights > 0)
        {
            overnightAmount = card.OvernightSurcharge * overnightNights;
            lines.Add(new PriceLine(
                overnightNights == 1
                    ? "Phụ thu qua đêm (22:00–06:00)"
                    : $"Phụ thu qua đêm ({overnightNights} đêm × {card.OvernightSurcharge:N0}đ)",
                minutes, overnightNights, card.OvernightSurcharge, overnightAmount));
        }

        var multiplier = ResolveEntryMultiplier(card, startVn, holidays);
        if (vehicleType == VehicleType.Oversized && ruleType != VehicleType.Oversized)
            multiplier *= card.OversizedMultiplier;

        // US-047: ra sau giờ kết thúc + 15' ân hạn → cước quá giờ tính từ giờ kết thúc × hệ số quá giờ
        var stayMultiplier = overstayMultiplier ?? card.OverstayMultiplier;
        int overstayMinutes = 0;
        decimal overstayAmount = 0m;
        if (bookedEndUtc is { } endUtc && exitUtc > endUtc.AddMinutes(DefaultGracePeriodMinutes))
        {
            overstayMinutes = (int)Math.Ceiling((exitUtc - endUtc).TotalMinutes);
            var (_, overstayBase) = BuildTieredLines(rules, overstayMinutes);
            overstayAmount = Math.Round(overstayBase * stayMultiplier, 0);
            lines.Add(new PriceLine(
                $"Phụ thu quá giờ ({overstayMinutes} phút × hệ số quá giờ {stayMultiplier:0.##})",
                overstayMinutes, 0, 0, overstayAmount));
        }

        var final = Math.Round((baseAmount + overnightAmount) * multiplier, 0);
        if (card.MaxDailyAmount is { } cap)
        {
            var dayCount = (int)Math.Ceiling(minutes / 1440.0);
            final = Math.Min(final, cap * dayCount);
        }
        final += overstayAmount;

        return new SessionFeeQuote(minutes, false, baseAmount, multiplier, overstayMinutes, overstayAmount, final, lines);
    }

    /// <summary>Xe quá khổ chiếm diện tích lớn: nếu bãi chưa cấu hình riêng thì lấy giá dòng SUV làm chuẩn gốc.</summary>
    private static VehicleType ResolveRuleType(RateCard card, VehicleType vehicleType)
        => card.Rules.Any(r => r.VehicleType == vehicleType) ? vehicleType : VehicleType.Suv;

    /// <summary>Danh sách bậc giá của loại xe (hoặc chuẩn gốc), sắp theo phút bắt đầu; ném lỗi nếu bãi chưa có giá.</summary>
    private static List<RateRule> ResolveRules(RateCard card, VehicleType vehicleType, VehicleType ruleType)
    {
        var rules = card.Rules.Where(r => r.VehicleType == ruleType).OrderBy(r => r.FromMinute).ToList();
        if (rules.Count == 0)
            throw new InvalidOperationException($"Bảng giá chưa có giá cho loại xe {vehicleType}.");
        return rules;
    }

    /// <summary>Sinh các dòng tiền bậc lũy tiến cho <paramref name="minutes"/> phút và trả tổng tiền gốc.</summary>
    private static (List<PriceLine> Lines, decimal BaseAmount) BuildTieredLines(List<RateRule> rules, int minutes)
    {
        var lines = new List<PriceLine>();
        foreach (var r in rules)
        {
            var to = Math.Min(minutes, r.ToMinute ?? int.MaxValue);
            var portion = to - r.FromMinute;
            if (portion <= 0) break;
            var blocks = (int)Math.Ceiling(portion / (double)r.BlockMinutes);
            lines.Add(new PriceLine(
                r.ToMinute is null ? $"Từ phút {r.FromMinute}" : $"Phút {r.FromMinute}–{r.ToMinute}",
                portion, blocks, r.PricePerBlock, blocks * r.PricePerBlock));
        }

        return (lines, lines.Sum(l => l.Amount));
    }

    /// <summary>Hệ số ngày: ngày lễ quốc gia ưu tiên trước, không trùng lễ thì xét cuối tuần (Thứ Bảy/Chủ Nhật).</summary>
    private static decimal ResolveEntryMultiplier(RateCard card, DateTime startVn, IReadOnlyCollection<DateOnly>? holidays)
    {
        var startDateOnly = DateOnly.FromDateTime(startVn);
        var isHoliday = holidays is not null && holidays.Contains(startDateOnly);

        if (isHoliday && card.HolidayMultiplier > 1.0m)
            return card.HolidayMultiplier;
        if (startVn.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return card.WeekendMultiplier;
        return 1m;
    }

    /// <summary>
    /// Đếm số buổi tối (khung giờ đêm từ 22:00 hôm trước đến 06:00 sáng hôm sau) mà xe đã gửi tại bãi.
    /// </summary>
    /// <param name="startVn">Thời điểm vào theo giờ địa phương Việt Nam.</param>
    /// <param name="endVn">Thời điểm ra theo giờ địa phương Việt Nam.</param>
    /// <returns>Số buổi tối xe đã đỗ qua (0, 1, 2, ... đêm).</returns>
    public static int CountOvernightWindows(DateTime startVn, DateTime endVn)
    {
        var count = 0;
        // Quét từ đêm trước ngày bắt đầu (xử lý xe vào rạng sáng 00:00-06:00) đến ngày kết thúc
        for (var currentDay = startVn.Date.AddDays(-1); currentDay <= endVn.Date; currentDay = currentDay.AddDays(1))
        {
            var nightStart = currentDay.AddHours(22);
            var nightEnd = currentDay.AddDays(1).AddHours(6);

            // Hai khoảng thời gian giao nhau khi điểm đầu nằm trước mốc kết thúc và điểm cuối nằm sau mốc bắt đầu
            if (startVn < nightEnd && endVn > nightStart)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Xác định lượt gửi xe có giao cắt với khung giờ đêm hay không (giữ lại để tương thích ngược).
    /// </summary>
    /// <param name="startVn">Thời điểm vào theo giờ địa phương Việt Nam.</param>
    /// <param name="endVn">Thời điểm ra theo giờ địa phương Việt Nam.</param>
    /// <returns>True nếu phiên đỗ đi qua ít nhất 1 buổi tối, ngược lại False.</returns>
    public static bool CrossesOvernightWindow(DateTime startVn, DateTime endVn)
        => CountOvernightWindows(startVn, endVn) > 0;
}
