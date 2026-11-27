using SupportService.Application.DTOs;

namespace SupportService.Application.Interfaces;

/// <summary>Port đọc dữ liệu FAQ – Infrastructure cài đặt bằng EF Core + LINQ.</summary>
public interface IFaqQueries
{
    /// <summary>FAQ đã công khai dành cho <paramref name="audiences"/>, sắp theo Category rồi SortOrder.</summary>
    Task<IReadOnlyList<FaqItemDto>> ListPublishedAsync(IReadOnlyCollection<string> audiences, string? category, CancellationToken cancellationToken = default);

    /// <summary>Lấy 1 FAQ đã công khai và tăng ViewCount; null nếu không có hoặc chưa công khai.</summary>
    Task<FaqItemDto?> GetPublishedAndCountViewAsync(int id, CancellationToken cancellationToken = default);
}
