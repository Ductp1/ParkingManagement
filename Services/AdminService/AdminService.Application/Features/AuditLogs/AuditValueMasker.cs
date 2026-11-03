using System.Text.Json;
using System.Text.Json.Nodes;

namespace AdminService.Application.Features.AuditLogs;

/// <summary>
/// US-100: che dữ liệu nhạy cảm trong OldValuesJson / NewValuesJson khi đọc ra (dữ liệu gốc trong pm_admin giữ nguyên).
/// So tên khóa không phân biệt hoa thường, bỏ "_", "-", ".", khoảng trắng; áp dụng ở mọi cấp lồng nhau và trong mảng.
///  - Che hết ("***"): mật khẩu, token, secret, OTP, API key, mã PIN / CVV.
///  - Giữ 4 ký tự cuối ("****1234"): số tài khoản ngân hàng, số thẻ, CCCD / CMND / căn cước / hộ chiếu.
/// Hàm thuần – không đọc database.
/// </summary>
public static class AuditValueMasker
{
    public const string Hidden = "***";
    private const string TailPrefix = "****";

    // Khóa dài, đặc trưng → khớp khi chứa chuỗi (VD passwordHash, refreshToken, bookingToken, clientSecret).
    private static readonly string[] FullContains = ["password", "passwd", "secret", "token", "apikey", "privatekey", "credential"];
    // Khóa ngắn → chỉ khớp nguyên tên, tránh che nhầm (VD "pin" nằm trong "shipping").
    private static readonly HashSet<string> FullExact = ["otp", "otpcode", "pin", "pincode", "cvv", "cvc"];
    private static readonly string[] TailContains =
        ["bankaccount", "accountnumber", "accountno", "cardnumber", "cccd", "cmnd", "nationalid", "citizenid", "idnumber", "passport"];

    private enum Sensitivity { None, Full, Tail }

    /// <summary>Đọc chuỗi JSON đã lưu; null/rỗng → null. Chuỗi không phải JSON (không xảy ra với cột jsonb) → che hết.</summary>
    public static JsonNode? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return JsonValue.Create(Hidden);
        }
    }

    /// <summary>Bản sao đã che; node gốc không bị sửa.</summary>
    public static JsonNode? Mask(JsonNode? node) => node switch
    {
        null => null,
        JsonObject obj => new JsonObject(obj.Select(p => KeyValuePair.Create(p.Key, MaskProperty(p.Key, p.Value)))),
        JsonArray array => new JsonArray([.. array.Select(Mask)]),
        _ => node.DeepClone(),
    };

    /// <summary>Giá trị của một khóa: khóa nhạy cảm → che, còn lại → che đệ quy bên trong.</summary>
    internal static JsonNode? MaskProperty(string key, JsonNode? value) => SensitivityOf(key) switch
    {
        _ when value is null => null,
        Sensitivity.Full => JsonValue.Create(Hidden),
        Sensitivity.Tail => JsonValue.Create(MaskTail(value)),
        _ => Mask(value),
    };

    internal static bool IsSensitive(string key) => SensitivityOf(key) != Sensitivity.None;

    private static Sensitivity SensitivityOf(string key)
    {
        var normalized = new string([.. key.Where(c => c is not ('_' or '-' or '.' or ' ')).Select(char.ToLowerInvariant)]);
        if (FullExact.Contains(normalized) || FullContains.Any(normalized.Contains)) return Sensitivity.Full;
        return TailContains.Any(normalized.Contains) ? Sensitivity.Tail : Sensitivity.None;
    }

    /// <summary>Chuỗi / số → "****" + 4 ký tự cuối (không lộ độ dài); ≤ 4 ký tự hoặc kiểu khác (object, mảng, bool) → che hết.</summary>
    private static string MaskTail(JsonNode value)
    {
        var text = value is JsonValue v && v.GetValueKind() is JsonValueKind.String or JsonValueKind.Number
            ? (v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : v.ToJsonString()).Trim()
            : null;
        return text is { Length: > 4 } ? TailPrefix + text[^4..] : Hidden;
    }
}
