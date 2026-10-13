using System.Globalization;
using System.Text.Json;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Settings;

/// <summary>Khoảng cho phép của một tham số (hai đầu đều tính). AllowedValues dùng cho tham số kiểu chuỗi có danh sách cố định.</summary>
public sealed record SystemConfigRule(decimal? Min = null, decimal? Max = null, IReadOnlyList<string>? AllowedValues = null);

/// <summary>Quy tắc liên khóa: giá trị của LowerKey không được lớn hơn giá trị của UpperKey tại bất kỳ thời điểm nào.</summary>
public sealed record SystemConfigOrderRule(string LowerKey, string LowerLabel, string UpperKey, string UpperLabel);

/// <summary>
/// US-098: Luật kiểm tra giá trị tham số theo kiểu dữ liệu và theo từng khóa. Đây là hàng rào chống nhập sai
/// (VD hoa hồng 150%, giữ chỗ 0 phút), không phải tham số nghiệp vụ – giá trị nghiệp vụ vẫn nằm trong pm_admin.
/// Khóa chưa có luật riêng chỉ được kiểm tra theo kiểu dữ liệu.
/// </summary>
public static class SystemConfigRules
{
    public const int MaxValueLength = 1000;

    private static readonly Dictionary<string, SystemConfigRule> ByKey = new(StringComparer.Ordinal)
    {
        // Booking / gửi xe
        ["BOOKING_HOLD_MINUTES"] = new(1, 120),
        ["CHECKIN_GRACE_PERIOD_MINUTES"] = new(0, 120),
        ["PARKING_BILLING_GRACE_PERIOD_MINUTES"] = new(0, 120),
        ["CANCELLATION_WINDOW_MINUTES"] = new(0, 10080),
        ["NO_SHOW_CANCEL_AFTER_MINUTES"] = new(1, 1440),
        ["MANUAL_LOT_APPROVAL_MINUTES"] = new(1, 1440),
        // Hoa hồng / quyết toán
        ["DEFAULT_COMMISSION_RATE"] = new(0, 1),
        ["SETTLEMENT_CYCLE"] = new(AllowedValues: ["WEEKLY", "BIWEEKLY", "MONTHLY"]),
        // Tài khoản
        ["OTP_EXPIRY_SECONDS"] = new(30, 3600),
        ["OTP_MAX_ATTEMPTS"] = new(1, 10),
        ["OTP_LOCK_MINUTES"] = new(1, 1440),
        ["JWT_ACCESS_TOKEN_HOURS"] = new(1, 168),
        ["JWT_REFRESH_TOKEN_DAYS"] = new(1, 90),
        ["MAX_VEHICLES_PER_USER"] = new(1, 100),
        // Tìm bãi / vận hành bãi
        ["SEARCH_RADIUS_KM"] = new(0.1m, 100),
        ["HEIGHT_CLEARANCE_MARGIN_CM"] = new(0, 100),
        ["HEARTBEAT_INTERVAL_SECONDS"] = new(5, 3600),
        ["HEARTBEAT_MAX_FAILURES"] = new(1, 100),
        ["OCR_AUTO_OPEN_MIN_CONFIDENCE"] = new(0.01m, 1),
        ["OWNER_DISPUTE_RESPONSE_HOURS"] = new(1, 720),
        ["COMPLAINT_WINDOW_DAYS"] = new(1, 365),
        ["LAYOUT_UPDATE_DEADLINE_HOURS"] = new(1, 720),
        ["EV_IDLE_FEE_PER_15_MINUTES"] = new(0, 10_000_000),
        // Trần / sàn giá
        ["PRICE_FLOOR_PER_HOUR_VND"] = new(0, 10_000_000),
        ["PRICE_CEILING_PER_HOUR_VND"] = new(1, 10_000_000),
        ["PRICE_INCREASE_APPROVAL_THRESHOLD_RATE"] = new(0.01m, 10),
        // Chống gian lận
        ["API_RATE_LIMIT_PER_MINUTE"] = new(1, 100_000),
        ["FRAUD_BOOKING_CANCEL_MAX_PER_HOUR"] = new(1, 1000),
        ["FRAUD_NO_SHOW_RATE_THRESHOLD"] = new(0, 1),
        ["FRAUD_STAFF_CANCEL_MAX_PER_HOUR"] = new(1, 1000),
    };

    private static readonly SystemConfigRule NoRule = new();

    public static IReadOnlyList<SystemConfigOrderRule> OrderRules { get; } =
    [
        new("PRICE_FLOOR_PER_HOUR_VND", "Sàn giá", "PRICE_CEILING_PER_HOUR_VND", "trần giá"),
        new("CHECKIN_GRACE_PERIOD_MINUTES", "Ân hạn check-in", "NO_SHOW_CANCEL_AFTER_MINUTES", "mốc hủy no-show"),
    ];

    public static bool HasRuleFor(string key) => ByKey.ContainsKey(key);

    public static SystemConfigRule For(string key) => ByKey.GetValueOrDefault(key, NoRule);

    /// <summary>Kiểm tra giá trị theo kiểu dữ liệu + luật của khóa, trả về giá trị đã chuẩn hóa để lưu. Sai → ValidationException (400).</summary>
    public static string Normalize(string key, string dataType, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) throw new ValidationException("Giá trị tham số không được để trống.");
        var value = rawValue.Trim();
        if (value.Length > MaxValueLength) throw new ValidationException($"Giá trị tham số tối đa {MaxValueLength} ký tự.");

        var rule = For(key);
        switch (dataType)
        {
            case "int":
                if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
                    throw new ValidationException($"Giá trị của {key} phải là số nguyên.");
                EnsureInRange(key, number, rule);
                return number.ToString(CultureInfo.InvariantCulture);

            case "decimal":
                if (!TryParseDecimal(value, out var amount))
                    throw new ValidationException($"Giá trị của {key} phải là số thập phân, dùng dấu chấm (VD 0.15).");
                EnsureInRange(key, amount, rule);
                return amount.ToString(CultureInfo.InvariantCulture);

            case "bool":
                if (!bool.TryParse(value, out var flag)) throw new ValidationException($"Giá trị của {key} phải là true hoặc false.");
                return flag ? "true" : "false";

            case "json":
                try
                {
                    using var _ = JsonDocument.Parse(value);
                }
                catch (JsonException)
                {
                    throw new ValidationException($"Giá trị của {key} phải là JSON hợp lệ.");
                }
                return value;

            default:
                if (rule.AllowedValues is null) return value;
                var match = rule.AllowedValues.FirstOrDefault(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ValidationException($"Giá trị của {key} phải là một trong: {string.Join(", ", rule.AllowedValues)}.");
                return match;
        }
    }

    /// <summary>So sánh hai giá trị đã chuẩn hóa theo kiểu dữ liệu (0.1 và 0.10 là một).</summary>
    public static bool AreSameValue(string dataType, string left, string right)
        => dataType is "int" or "decimal" && TryParseDecimal(left, out var l) && TryParseDecimal(right, out var r)
            ? l == r
            : string.Equals(left, right, StringComparison.Ordinal);

    public static bool TryParseDecimal(string value, out decimal result)
        => decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result);

    public static string? Format(decimal? bound) => bound?.ToString(CultureInfo.InvariantCulture);

    private static void EnsureInRange(string key, decimal value, SystemConfigRule rule)
    {
        if ((rule.Min is { } min && value < min) || (rule.Max is { } max && value > max))
            throw new ValidationException(rule switch
            {
                { Min: not null, Max: not null } => $"Giá trị của {key} phải trong khoảng {Format(rule.Min)}–{Format(rule.Max)}.",
                { Min: not null } => $"Giá trị của {key} phải ≥ {Format(rule.Min)}.",
                _ => $"Giá trị của {key} phải ≤ {Format(rule.Max)}.",
            });
    }
}
