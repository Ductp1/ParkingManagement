using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Features.Ingestion;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Persistence;

/// <summary>
/// US-083: render mẫu i18n từ bảng NotificationTemplates — chọn (Key, Channel, Language),
/// thiếu bản dịch của ngôn ngữ user thì fallback "vi", thiếu hẳn key → ném InvalidOperationException
/// (handler coi như lỗi tạm thời → message về dead-letter để nhóm sửa, không mất im lặng).
/// </summary>
public sealed class DbTemplateRenderer(NotificationDbContext db) : ITemplateRenderer
{
    public async Task<RenderedTemplate> RenderAsync(string templateKey, NotificationChannel channel, string language,
        IReadOnlyDictionary<string, string> placeholders, CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(language) ? "vi" : language.Trim().ToLowerInvariant();
        var template = await db.NotificationTemplates
            .Where(t => t.Key == templateKey && t.Channel == channel && t.IsActive)
            .OrderBy(t => t.Language == normalized ? 0 : 1) // ưu tiên ngôn ngữ user, rồi fallback "vi"
            .ThenBy(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Thiếu mẫu thông báo '{templateKey}' cho kênh {channel} (ngôn ngữ {normalized}).");

        var title = Fill(template.TitleTemplate, placeholders);
        var body = Fill(template.BodyTemplate, placeholders);
        return new RenderedTemplate(title, body, template.Id);
    }

    private static string Fill(string template, IReadOnlyDictionary<string, string> placeholders)
        => placeholders.Aggregate(template,
            (current, pair) => current.Replace("{" + pair.Key + "}", pair.Value));
}
