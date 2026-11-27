using NotificationService.Application.Features.Ingestion;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Reminders;

/// <summary>US-084: xử lý job đến hạn (phần 2 của BookingReminderScheduler — partial để file gọn).</summary>
public sealed partial class BookingReminderScheduler
{
    public async Task<int> ProcessDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var due = await repository.GetDueAsync(nowUtc, options.BatchSize, cancellationToken);
        foreach (var job in due)
        {
            try
            {
                var templateKey = job.Kind == ReminderKind.BeforeArrival
                    ? "BOOKING_REMINDER_ARRIVAL"
                    : "BOOKING_REMINDER_END";
                var language = await settings.GetLanguageAsync(job.UserId, cancellationToken);
                var placeholders = new Dictionary<string, string>
                {
                    ["BookingCode"] = job.BookingCode,
                    ["LotName"] = job.LotName,
                    ["SlotCode"] = job.SlotCode ?? "tự chọn",
                    ["StartAt"] = VietnamTime.Format(job.TargetTimeUtc),
                    ["EndAt"] = VietnamTime.Format(job.TargetTimeUtc),
                };

                var rendered = await renderer.RenderAsync(templateKey, NotificationChannel.InApp, language, placeholders, cancellationToken);
                await sender.SendAsync(job.UserId, NotificationChannel.InApp, templateKey, rendered.Title, rendered.Body,
                    System.Text.Json.JsonSerializer.Serialize(new { bookingId = job.BookingId, kind = job.Kind.ToString() }),
                    $"reminder:{job.Id}", cancellationToken);

                job.Status = ReminderJobStatus.Sent;
                job.SentAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            }
            catch
            {
                // Lỗi tạm thời → giữ Pending, vòng poll sau thử lại (dispatcher đã retry nội bộ 3 lần).
            }
        }

        if (due.Count > 0)
            await repository.SaveChangesAsync(cancellationToken);
        return due.Count(j => j.Status == ReminderJobStatus.Sent);
    }

    private static ReminderJob NewJob(BookingConfirmed evt, ReminderKind kind, DateTime targetTimeUtc, DateTime dueAtUtc) => new()
    {
        BookingId = evt.BookingId,
        BookingCode = evt.Code,
        UserId = evt.UserId,
        ParkingLotId = evt.ParkingLotId,
        LotName = evt.ParkingLotName,
        SlotCode = evt.SlotCode,
        Kind = kind,
        TargetTimeUtc = targetTimeUtc,
        DueAtUtc = dueAtUtc,
        Status = ReminderJobStatus.Pending,
    };

    private ReminderJob NewJob(BookingModified evt, ReminderKind kind, DateTime targetTimeUtc, DateTime dueAtUtc) => new()
    {
        BookingId = evt.BookingId,
        BookingCode = evt.Code,
        UserId = evt.UserId,
        ParkingLotId = evt.ParkingLotId,
        LotName = evt.ParkingLotName,
        SlotCode = null,
        Kind = kind,
        TargetTimeUtc = targetTimeUtc,
        DueAtUtc = dueAtUtc,
        Status = ReminderJobStatus.Pending,
    };
}
