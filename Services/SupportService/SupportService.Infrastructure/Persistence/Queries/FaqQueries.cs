using Microsoft.EntityFrameworkCore;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;

namespace SupportService.Infrastructure.Persistence.Queries;

public sealed class FaqQueries(SupportDbContext db) : IFaqQueries
{
    public async Task<IReadOnlyList<FaqItemDto>> ListPublishedAsync(IReadOnlyCollection<string> audiences, string? category, CancellationToken cancellationToken = default)
    {
        var query = db.FaqArticles.AsNoTracking().Where(f => f.IsPublished && audiences.Contains(f.Audience));
        // PostgreSQL so sánh chuỗi phân biệt hoa/thường → "booking" cũng phải khớp "Booking".
        if (category is not null) query = query.Where(f => f.Category.ToLower() == category.ToLower());

        return await query
            .OrderBy(f => f.SortOrder)
            .Select(f => new FaqItemDto(f.Id, f.Category, f.Audience, f.Question, f.Answer, f.SortOrder, f.ViewCount))
            .ToListAsync(cancellationToken);
    }

    public async Task<FaqItemDto?> GetPublishedAndCountViewAsync(int id, CancellationToken cancellationToken = default)
    {
        // Một câu UPDATE nguyên tử – không mất lượt đếm khi nhiều người xem cùng lúc.
        var updated = await db.FaqArticles
            .Where(f => f.Id == id && f.IsPublished)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.ViewCount, f => f.ViewCount + 1), cancellationToken);
        if (updated == 0) return null;

        return await db.FaqArticles.AsNoTracking()
            .Where(f => f.Id == id)
            .Select(f => new FaqItemDto(f.Id, f.Category, f.Audience, f.Question, f.Answer, f.SortOrder, f.ViewCount))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
