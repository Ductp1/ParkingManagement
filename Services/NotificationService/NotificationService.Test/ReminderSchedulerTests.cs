using Microsoft.Extensions.Time.Testing;
using NotificationService.Application.Features.Ingestion;
using NotificationService.Application.Features.Reminders;
using NotificationService.Domain.Entities;
using NSubstitute;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Test;

/// <summary>
/// US-084 (T-841): đặt/hủy/reschedule 2 nhắc lịch mỗi booking + xử lý job đến hạn.
/// FakeTimeProvider điều khiển "bây giờ"; fake repo in-memory; sender ghi lại (pattern US-083).
/// </summary>
public class ReminderSchedulerTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 1, 0, 0, DateTimeKind.Utc);

    private readonly FakeReminderJobRepo _repo = new();
    private readonly ITemplateRenderer _renderer = Substitute.For<ITemplateRenderer>();
    private readonly IUserNotificationSettingsPort _settings = Substitute.For<IUserNotificationSettingsPort>();
    private readonly List<(int UserId, NotificationChannel Channel, string TemplateKey, string Title, string Body, string? DataJson, string? DedupeKey)> _sent = [];

    private BookingReminderScheduler Create()
    {
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<NotificationChannel>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedTemplate("t", "b", 1));
        _settings.GetLanguageAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("vi");
        return new BookingReminderScheduler(_repo, _renderer, _settings,
            new RecordingSender(_sent), new ReminderOptions(), new FixedClock(Now));
    }

    private static BookingConfirmed Confirmed(DateTime startUtc, DateTime endUtc) => new(
        BookingId: 12, Code: "BK-084", UserId: 5, ParkingLotId: 2, OwnerProfileId: 7, ParkingLotName: "Bãi A",
        SlotId: null, SlotCode: "A-01", PlateNumber: "51F-1", StartAtUtc: startUtc, EndAtUtc: endUtc, TotalAmount: 100000m);

    [Fact]
    public async Task Schedule_creates_two_jobs_when_both_due_times_in_future()
    {
        var evt = Confirmed(Now.AddHours(3), Now.AddHours(5));

        await Create().ScheduleForBookingAsync(evt);

        Assert.Equal(2, _repo.Jobs.Count);
        Assert.Contains(_repo.Jobs, j => j.Kind == ReminderKind.BeforeArrival && j.DueAtUtc == Now.AddHours(2));
        Assert.Contains(_repo.Jobs, j => j.Kind == ReminderKind.BeforeEnd && j.DueAtUtc == Now.AddHours(4).AddMinutes(30));
    }

    [Fact]
    public async Task Schedule_skips_job_whose_due_time_already_past()
    {
        // Bắt đầu sau 30 phút → nhắc "trước giờ đến" (60') đã quá kỳ → chỉ tạo nhắc "trước giờ hết hạn".
        var evt = Confirmed(Now.AddMinutes(30), Now.AddHours(4));

        await Create().ScheduleForBookingAsync(evt);

        var job = Assert.Single(_repo.Jobs);
        Assert.Equal(ReminderKind.BeforeEnd, job.Kind);
        Assert.Equal(Now.AddHours(3).AddMinutes(30), job.DueAtUtc);
    }

    [Fact]
    public async Task Cancel_marks_all_pending_jobs_cancelled()
    {
        await Create().ScheduleForBookingAsync(Confirmed(Now.AddHours(3), Now.AddHours(5)));

        await Create().CancelForBookingAsync(new BookingCancelled(12, "BK-084", 5, 2, 7, "Bãi A", Now.AddHours(3), 0m, "Đổi kế hoạch"));

        Assert.All(_repo.Jobs, j => Assert.Equal(ReminderJobStatus.Cancelled, j.Status));
    }

    [Fact]
    public async Task Reschedule_cancels_old_jobs_and_creates_new_ones_with_new_times()
    {
        var scheduler = Create();
        await scheduler.ScheduleForBookingAsync(Confirmed(Now.AddHours(3), Now.AddHours(5)));

        var modified = new BookingModified(12, "BK-084", 5, 2, 7, "Bãi A", Now.AddHours(6), Now.AddHours(9), "Đổi giờ đỗ");
        await scheduler.RescheduleForBookingAsync(modified);

        Assert.Equal(2, _repo.Jobs.Count(j => j.Status == ReminderJobStatus.Cancelled));
        var newArrival = Assert.Single(_repo.Jobs, j => j.Status == ReminderJobStatus.Pending && j.Kind == ReminderKind.BeforeArrival);
        Assert.Equal(Now.AddHours(5), newArrival.DueAtUtc);
    }

    [Fact]
    public async Task ProcessDue_sends_inapp_and_marks_job_sent()
    {
        var scheduler = Create();
        await scheduler.ScheduleForBookingAsync(Confirmed(Now.AddHours(3), Now.AddHours(5)));

        var sentCount = await scheduler.ProcessDueAsync(Now.AddHours(2)); // đến hạn nhắc arrival

        Assert.Equal(1, sentCount);
        var job = Assert.Single(_repo.Jobs, j => j.Kind == ReminderKind.BeforeArrival);
        Assert.Equal(ReminderJobStatus.Sent, job.Status);
        Assert.NotNull(job.SentAtUtc);
        var sent = Assert.Single(_sent);
        Assert.Equal("BOOKING_REMINDER_ARRIVAL", sent.TemplateKey);
        Assert.Equal($"reminder:{job.Id}", sent.DedupeKey); // idempotent — job gửi lại không tạo bản sao thông báo
    }

    private sealed class FakeReminderJobRepo : IReminderJobRepository
    {
        public List<ReminderJob> Jobs { get; } = [];

        public Task CancelPendingForBookingAsync(int bookingId, CancellationToken cancellationToken = default)
        {
            foreach (var job in Jobs.Where(j => j.BookingId == bookingId && j.Status == ReminderJobStatus.Pending))
                job.Status = ReminderJobStatus.Cancelled;
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IReadOnlyList<ReminderJob> jobs, CancellationToken cancellationToken = default)
        {
            foreach (var job in jobs)
                Jobs.Add(job);
            return Task.CompletedTask;
        }

        public Task<List<ReminderJob>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
            => Task.FromResult(Jobs.Where(j => j.Status == ReminderJobStatus.Pending && j.DueAtUtc <= nowUtc)
                .OrderBy(j => j.DueAtUtc).Take(take).ToList());

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingSender(List<(int UserId, NotificationChannel Channel, string TemplateKey, string Title, string Body, string? DataJson, string? DedupeKey)> sink)
        : ITransactionalNotificationSender
    {
        public List<(int UserId, NotificationChannel Channel, string TemplateKey, string Title, string Body, string? DataJson, string? DedupeKey)> Sent { get; } = sink;

        public Task SendAsync(int userId, NotificationChannel channel, string templateKey, string title, string body,
            string? dataJson, string? dedupeKey, CancellationToken cancellationToken = default)
        {
            Sent.Add((userId, channel, templateKey, title, body, dataJson, dedupeKey));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }
}
