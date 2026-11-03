using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Features.Ingestion;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Persistence.Repositories;

/// <summary>US-083: cài đặt ngôn ngữ 1 row/user; chưa có row → mặc định "vi".</summary>
public sealed class UserNotificationSettingsRepository(NotificationDbContext db) : IUserNotificationSettingsPort
{
    public async Task<string> GetLanguageAsync(int userId, CancellationToken cancellationToken = default)
    {
        var language = await db.UserNotificationSettings
            .Where(s => s.UserId == userId)
            .Select(s => s.Language)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(language) ? "vi" : language;
    }
}
