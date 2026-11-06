using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Features.Preferences;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementation của INotificationPreferenceRepository (EF Core + LINQ). Module: TV6.
/// </summary>
public sealed class NotificationPreferenceRepository(NotificationDbContext dbContext) : INotificationPreferenceRepository
{
    public async Task<IReadOnlyList<NotificationPreference>> GetByUserAsync(int userId, CancellationToken cancellationToken = default)
        => await dbContext.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task UpsertAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.NotificationPreferences.FirstOrDefaultAsync(
            p => p.UserId == preference.UserId
              && p.TemplateKey == preference.TemplateKey
              && p.Channel == preference.Channel,
            cancellationToken);

        if (existing is null)
            dbContext.NotificationPreferences.Add(preference);
        else
            existing.IsEnabled = preference.IsEnabled;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
