using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

using NotificationService.Domain.Entities;
using NotificationService.Application.Features.Ingestion;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Reminders;

/// <summary>US-084: truy cập job nhắc lịch (Infrastructure EF implement).</summary>
public interface IReminderJobRepository
{
    Task CancelPendingForBookingAsync(int bookingId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IReadOnlyList<ReminderJob> jobs, CancellationToken cancellationToken = default);
    Task<List<ReminderJob>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>US-084: đặt/hủy/reschedule 2 nhắc lịch mỗi booking + xử lý job đến hạn (worker gọi ProcessDueAsync).</summary>
public interface IBookingReminderScheduler
{
    Task ScheduleForBookingAsync(BookingConfirmed evt, CancellationToken cancellationToken = default);
    Task CancelForBookingAsync(BookingCancelled evt, CancellationToken cancellationToken = default);
    Task RescheduleForBookingAsync(BookingModified evt, CancellationToken cancellationToken = default);
    Task<int> ProcessDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}

/// <summary>Cấu hình lead time nhắc lịch (appsettings → "Reminders").</summary>
public sealed class ReminderOptions
{
    public const string SectionName = "Reminders";
    /// <summary>Nhắc trước giờ đến bao nhiêu phút (mặc định 60).</summary>
    public int ArrivalLeadMinutes { get; set; } = 60;
    /// <summary>Nhắc trước giờ hết hạn bao nhiêu phút (mặc định 30).</summary>
    public int EndLeadMinutes { get; set; } = 30;
    /// <summary>US-084: poll interval (giây) — mặc định 15.</summary>
    public int PollSeconds { get; set; } = 15;
    /// <summary>Số job xử lý mỗi vòng poll.</summary>
    public int BatchSize { get; set; } = 50;
}

/// <summary>US-084: đặt/hủy/reschedule job nhắc lịch — phần 1 (ProcessDue ở file partial).</summary>
public sealed partial class BookingReminderScheduler(
    IReminderJobRepository repository,
    ITemplateRenderer renderer,
    IUserNotificationSettingsPort settings,
    ITransactionalNotificationSender sender,
    ReminderOptions options,
    TimeProvider timeProvider) : IBookingReminderScheduler
{
    public async Task ScheduleForBookingAsync(BookingConfirmed evt, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var jobs = new List<ReminderJob>();

        var arrivalDue = evt.StartAtUtc.AddMinutes(-options.ArrivalLeadMinutes);
        if (arrivalDue > now)
            jobs.Add(NewJob(evt, ReminderKind.BeforeArrival, evt.StartAtUtc, arrivalDue));

        var endDue = evt.EndAtUtc.AddMinutes(-options.EndLeadMinutes);
        if (endDue > now)
            jobs.Add(NewJob(evt, ReminderKind.BeforeEnd, evt.EndAtUtc, endDue));

        if (jobs.Count > 0)
            await repository.AddRangeAsync(jobs, cancellationToken);
    }

    public Task CancelForBookingAsync(BookingCancelled evt, CancellationToken cancellationToken = default)
        => repository.CancelPendingForBookingAsync(evt.BookingId, cancellationToken);

    public async Task RescheduleForBookingAsync(BookingModified evt, CancellationToken cancellationToken = default)
    {
        // Reschedule = hủy job Pending cũ rồi tạo job mới với mốc thời gian mới (unique index thỏa sau khi cancel).
        await repository.CancelPendingForBookingAsync(evt.BookingId, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var jobs = new List<ReminderJob>();
        var arrivalDue = evt.StartAtUtc.AddMinutes(-options.ArrivalLeadMinutes);
        if (arrivalDue > now)
            jobs.Add(NewJob(evt, ReminderKind.BeforeArrival, evt.StartAtUtc, arrivalDue));
        var endDue = evt.EndAtUtc.AddMinutes(-options.EndLeadMinutes);
        if (endDue > now)
            jobs.Add(NewJob(evt, ReminderKind.BeforeEnd, evt.EndAtUtc, endDue));
        if (jobs.Count > 0)
            await repository.AddRangeAsync(jobs, cancellationToken);
    }
}