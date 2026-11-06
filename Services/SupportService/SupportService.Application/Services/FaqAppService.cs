using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;

namespace SupportService.Application.Services;

/// <summary>
/// US-092: Trung tâm trợ giúp – FAQ theo chủ đề + kênh liên hệ.
/// Driver thấy FAQ "Driver" và "All"; LotOwner thấy "LotOwner" và "All"; "All" chỉ thấy FAQ chung.
/// </summary>
public sealed class FaqAppService(IFaqQueries queries, HelpCenterSettings settings) : IFaqService
{
    private static readonly string[] Audiences = ["Driver", "LotOwner", "All"];

    public async Task<HelpCenterDto> GetHelpCenterAsync(string? audience, string? category, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAudience(audience);
        var visibleTo = normalized == "All" ? new[] { "All" } : new[] { normalized, "All" };
        var cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();

        var items = await queries.ListPublishedAsync(visibleTo, cat, cancellationToken);

        var categories = items
            .GroupBy(f => f.Category)
            .OrderBy(g => g.Min(f => f.SortOrder))
            .Select(g => new FaqCategoryDto(g.Key, g.OrderBy(f => f.SortOrder).ToList()))
            .ToList();

        return new HelpCenterDto(normalized, categories, settings.Contacts);
    }

    public async Task<FaqItemDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        return await queries.GetPublishedAndCountViewAsync(id, cancellationToken)
               ?? throw new NotFoundException("FaqArticle", id);
    }

    private static string NormalizeAudience(string? audience)
    {
        if (string.IsNullOrWhiteSpace(audience)) return "All";
        return Audiences.FirstOrDefault(a => a.Equals(audience.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? throw new ValidationException("audience chỉ nhận Driver, LotOwner hoặc All.");
    }
}
