using System.Text.Json;
using ParkingManagement.SharedKernel.Exceptions;

namespace SupportService.Application.Services;

/// <summary>Chuẩn hóa / đọc danh sách link bằng chứng (ảnh, video, log) dùng chung cho tài xế và chủ bãi.</summary>
public static class EvidenceUrls
{
    private const int MaxUrlLength = 500;

    /// <summary>Bỏ link trống và trùng; tối đa <paramref name="maxCount"/> link http/https.</summary>
    public static List<string> Normalize(IReadOnlyList<string>? urls, int maxCount)
    {
        var list = (urls ?? []).Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct().ToList();
        if (list.Count > maxCount)
            throw new ValidationException($"Chỉ được đính kèm tối đa {maxCount} bằng chứng.");
        foreach (var url in list)
        {
            if (url.Length > MaxUrlLength || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                throw new ValidationException($"Đường dẫn bằng chứng không hợp lệ: {url}");
        }
        return list;
    }

    /// <summary>Lưu dạng mảng JSON (cột jsonb); không có link → null.</summary>
    public static string? ToJson(IReadOnlyCollection<string> urls) => urls.Count == 0 ? null : JsonSerializer.Serialize(urls);
}
