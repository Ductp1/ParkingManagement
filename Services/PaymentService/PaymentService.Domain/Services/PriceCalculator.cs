using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Domain.Services;

/// <summary>1 dòng chi tiết cách tính tiền – trả về cho client để minh bạch giá.</summary>
public sealed record PriceLine(string Description, int Minutes, int Blocks, decimal UnitPrice, decimal Amount);

public sealed record PriceQuote(int DurationMinutes, decimal BaseAmount, decimal Multiplier, decimal FinalAmount, bool IsWithinGracePeriod,
    IReadOnlyList<PriceLine> Lines);

/// <summary>
/// Pricing Rule Engine (SRS v2 F3.1, Đặc tả v3 §4.4):
///  - Lượt đỗ ≤ 15 phút (Parking Billing Grace Period) → 0đ.
///  - Giá lũy tiến theo bậc: mỗi bậc [FromMinute, ToMinute) tính theo block, block lẻ làm tròn lên.
///  - Nhân hệ số cuối tuần (theo giờ Việt Nam) và hệ số xe quá khổ.
///  - Không vượt trần giá/ngày (MaxDailyAmount × số ngày).
/// </summary>
public static class PriceCalculator
{
    public const int DefaultGracePeriodMinutes = 15;

    public static PriceQuote Calculate(RateCard card, VehicleType vehicleType, DateTime startUtc, DateTime endUtc,
        int gracePeriodMinutes = DefaultGracePeriodMinutes)
    {
        if (endUtc <= startUtc) throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.", nameof(endUtc));

        var minutes = (int)Math.Ceiling((endUtc - startUtc).TotalMinutes);
        if (minutes <= gracePeriodMinutes)
            return new PriceQuote(minutes, 0, 1, 0, true, []);

        // Xe quá khổ chưa có bảng giá riêng thì dùng giá SUV × hệ số quá khổ.
        var ruleType = card.Rules.Any(r => r.VehicleType == vehicleType) ? vehicleType : VehicleType.Suv;
        var rules = card.Rules.Where(r => r.VehicleType == ruleType).OrderBy(r => r.FromMinute).ToList();
        if (rules.Count == 0) throw new InvalidOperationException($"Bảng giá chưa có giá cho loại xe {vehicleType}.");

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

        var baseAmount = lines.Sum(l => l.Amount);
        var multiplier = 1m;
        var startVn = startUtc.AddHours(7);
        if (startVn.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) multiplier *= card.WeekendMultiplier;
        if (vehicleType == VehicleType.Oversized && ruleType != VehicleType.Oversized) multiplier *= card.OversizedMultiplier;

        var final = Math.Round(baseAmount * multiplier, 0);
        if (card.MaxDailyAmount is { } cap)
            final = Math.Min(final, cap * (int)Math.Ceiling(minutes / 1440.0));

        return new PriceQuote(minutes, baseAmount, multiplier, final, false, lines);
    }
}
