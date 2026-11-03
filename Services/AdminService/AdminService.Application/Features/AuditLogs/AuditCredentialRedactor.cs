using System.Text.Json;
using System.Text.Json.Nodes;

namespace AdminService.Application.Features.AuditLogs;

/// <summary>
/// T-802: che thông tin xác thực trong Old/NewValuesJson TRƯỚC KHI ghi. AuditLogs chỉ thêm, không sửa / xóa được,
/// nên bí mật lỡ ghi vào sẽ nằm lại vĩnh viễn. Chỉ che khóa credential (so khớp nguyên tên, không phân biệt hoa thường,
/// đệ quy trong object / array); dữ liệu nghiệp vụ (số tài khoản, số điện thoại...) giữ nguyên để US-100 che lúc đọc.
/// </summary>
public static class AuditCredentialRedactor
{
    public const string Mask = "***";

    private static readonly HashSet<string> CredentialKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwordHash", "otp", "otpCode", "token", "accessToken", "refreshToken",
        "secret", "apiKey", "cardNumber", "cvv",
    };

    /// <summary>JSON hợp lệ → JSON đã che; không có khóa credential thì trả nguyên chuỗi gốc (giữ đúng định dạng).</summary>
    public static string Redact(string json)
    {
        var root = JsonNode.Parse(json);
        return root is not null && RedactNode(root) ? root.ToJsonString() : json;
    }

    private static bool RedactNode(JsonNode node)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (value is null) continue;                      // null không chứa bí mật nào
                    if (CredentialKeys.Contains(key))
                    {
                        if (value is JsonValue v && v.GetValueKind() == JsonValueKind.String && v.GetValue<string>() == Mask) continue;
                        obj[key] = Mask;
                        changed = true;
                    }
                    else changed |= RedactNode(value);
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                    if (item is not null) changed |= RedactNode(item);
                break;
        }
        return changed;
    }
}
