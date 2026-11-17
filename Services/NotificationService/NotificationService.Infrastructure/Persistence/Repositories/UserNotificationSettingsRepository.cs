using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Features.Ingestion;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Persistence.Repositories;

/// <summary>US-084: EF repository cho ReminderJob.</summary>
public sealed class ReminderJobRepository(NotificationDbContext db) : Application.Features.Reminders.IReminderJobRepository
{
    public async Task CancelPendingForBookingAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        var pending = await db.ReminderJobs
            .Where(j => j.BookingId == bookingId && j.Status == ReminderJobStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var job in pending)
            job.Status = ReminderJobStatus.Cancelled;
        if (pending.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyList<ReminderJob> jobs, CancellationToken cancellationToken = default)
    {
        db.ReminderJobs.AddRange(jobs);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<ReminderJob>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
        => db.ReminderJobs
            .Where(j => j.Status == ReminderJobStatus.Pending && j.DueAtUtc <= nowUtc)
            .OrderBy(j => j.DueAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}

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
