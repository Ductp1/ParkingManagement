using NotificationService.Application.Features.Ingestion;
using NotificationService.Application.Features.Dispatcher;
using NSubstitute;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Test;

/// <summary>
/// US-086 (T-861): EmergencyNotifications — fan-out đa kênh cho khách bị ảnh hưởng + chủ bãi,
/// DedupeKey theo (event × user × channel), fallback kênh khi FAILED vĩnh viễn, tạo EmergencyAlert
/// (tracking) và broadcast LotStatusChanged lên hub. Fakes theo pattern BookingConfirmationNotificationsTests.
/// </summary>
public class EmergencyNotificationsTests
{
    private static readonly Guid EventId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly ITemplateRenderer _renderer = Substitute.For<ITemplateRenderer>();
    private readonly IUserNotificationSettingsPort _settings = Substitute.For<IUserNotificationSettingsPort>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly FakeAlertStore _alertStore = new();
    private readonly RecordingHub _hub = new();
    private readonly RecordingDispatcher _dispatcher = new();

    public EmergencyNotificationsTests()
    {
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<NotificationChannel>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedTemplate("Tiêu đề", "Nội dung", 7));
        _settings.GetLanguageAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("vi");
        _userDirectory.GetOwnerUserIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(9);
    }

    private EmergencyNotifications Create() => new(
        _dispatcher, _renderer, _settings, _userDirectory, _alertStore, _hub, new EmergencyNotificationOptions());

    private static EmergencyAffectedBooking Affected(int bookingId, string code, int userId) =>
        new(bookingId, code, userId, "Confirmed", new DateTime(2026, 11, 6, 2, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 6, 5, 0, 0, DateTimeKind.Utc));

    private static LotEmergencyStopped StoppedEvent(params EmergencyAffectedBooking[] affected) => new(
        ParkingLotId: 3, LotName: "Bãi Vincom", OwnerProfileId: 7, StoppedByUserId: 10,
        Reason: "Cháy hệ thống điện tầng B1", StoppedAtUtc: new DateTime(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc),
        AffectedBookings: affected);

    [Fact]
    public async Task Emergency_stopped_fans_out_multi_channel_to_customers_and_owner()
    {
        await Create().HandleLotEmergencyStoppedAsync(
            StoppedEvent(Affected(12, "BK-0001", 5), Affected(13, "BK-0002", 6)), EventId);

        // 2 khách × 3 kênh (Push/Sms/Email broadcast) + chủ bãi × 2 kênh (InApp + Email) = 8
        Assert.Equal(8, _dispatcher.Requests.Count);

        // Mỗi (user × channel) đúng 1 DedupeKey — idempotent khi replay.
        var keys = _dispatcher.Requests.Select(r => r.DedupeKey).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Contains($"evt:{EventId}:u5:Push", keys);
        Assert.Contains($"evt:{EventId}:u5:Sms", keys);
        Assert.Contains($"evt:{EventId}:u5:Email", keys);
        Assert.Contains($"evt:{EventId}:u6:Push", keys);
        Assert.Contains($"evt:{EventId}:u9:InApp", keys);
        Assert.Contains($"evt:{EventId}:u9:Email", keys);
        // InApp cho khách KHÔNG có ở đợt chính (chỉ dùng khi fallback — test riêng bên dưới).
        Assert.DoesNotContain(keys, k => k.EndsWith("u5:InApp"));

        // Người nhận template khác nhau: khách EMERGENCY_CLOSURE, chủ bãi EMERGENCY_CLOSURE_OWNER.
        Assert.All(_dispatcher.Requests.Where(r => r.UserId is 5 or 6), r => Assert.Equal("EMERGENCY_CLOSURE", r.TemplateKey));
        Assert.All(_dispatcher.Requests.Where(r => r.UserId == 9), r => Assert.Equal("EMERGENCY_CLOSURE_OWNER", r.TemplateKey));

        // Tracking: 1 EmergencyAlert với BatchKey + tổng số người nhận (2 khách + 1 chủ bãi).
        var alert = Assert.Single(_alertStore.Alerts);
        Assert.Equal($"evt:{EventId}", alert.BatchKey);
        Assert.Equal(3, alert.ParkingLotId);
        Assert.Equal(7, alert.OwnerProfileId);
        Assert.Equal(3, alert.TotalRecipients);

        // Real-time: dashboard nhận LotStatusChanged Suspended (US-067).
        var message = Assert.Single(_hub.Messages);
        Assert.True(message.EmergencyStopped);
        Assert.Equal("Suspended", message.Status);
        Assert.Equal(3, message.ParkingLotId);
    }

    [Fact]
    public async Task Duplicate_user_across_bookings_is_notified_once_per_channel()
    {
        await Create().HandleLotEmergencyStoppedAsync(
            StoppedEvent(Affected(12, "BK-0001", 5), Affected(13, "BK-0002", 5)), EventId);

        // 1 khách × 3 + chủ bãi × 2 = 5 (không nhân đôi theo số booking của cùng user)
        Assert.Equal(5, _dispatcher.Requests.Count);
    }


    [Fact]
    public async Task Failed_channel_is_tracked_and_broadcast_continues_without_fallback()
    {
        _dispatcher.StatusByChannel[NotificationChannel.Push] = NotificationStatus.Failed; // Push chết vĩnh viễn

        await Create().HandleLotEmergencyStoppedAsync(StoppedEvent(Affected(12, "BK-0001", 5)), EventId);

        var user5 = _dispatcher.Requests.Where(r => r.UserId == 5).ToList();
        // Broadcast vẫn đẩy đủ 3 kênh chính (tracking Push=Failed), KHÔNG fallback vì Sms/Email còn thành công.
        Assert.Equal(3, user5.Count);
        Assert.Contains(user5, r => r.Channel == NotificationChannel.Push);
        Assert.DoesNotContain(user5, r => r.DedupeKey!.Contains(":fb:"));
    }

    [Fact]
    public async Task All_channels_failed_triggers_inapp_fallback_with_fallback_dedupe_key()
    {
        _dispatcher.StatusByChannel[NotificationChannel.Push] = NotificationStatus.Failed;
        _dispatcher.StatusByChannel[NotificationChannel.Sms] = NotificationStatus.Failed;
        _dispatcher.StatusByChannel[NotificationChannel.Email] = NotificationStatus.Failed;

        await Create().HandleLotEmergencyStoppedAsync(StoppedEvent(Affected(12, "BK-0001", 5)), EventId);

        var user5 = _dispatcher.Requests.Where(r => r.UserId == 5).ToList();
        // Cả 3 kênh chính FAILED → fallback InApp với DedupeKey ":fb:InApp" (kênh nội bộ nền tảng).
        Assert.Equal(4, user5.Count);
        Assert.Contains(user5, r => r.Channel == NotificationChannel.InApp && r.DedupeKey == $"evt:{EventId}:u5:fb:InApp");
        // Chủ bãi vẫn nhận đủ kênh của mình.
        Assert.Equal(2, _dispatcher.Requests.Count(r => r.UserId == 9));
    }

    [Fact]
    public async Task Pending_channels_are_still_retrying_and_do_not_fall_back()
    {
        _dispatcher.StatusByChannel[NotificationChannel.Push] = NotificationStatus.Pending;
        _dispatcher.StatusByChannel[NotificationChannel.Sms] = NotificationStatus.Pending;
        _dispatcher.StatusByChannel[NotificationChannel.Email] = NotificationStatus.Pending;

        await Create().HandleLotEmergencyStoppedAsync(StoppedEvent(Affected(12, "BK-0001", 5)), EventId);

        // Lỗi tạm thời — dispatcher còn retry với backoff → không fallback chồng.
        Assert.Equal(3, _dispatcher.Requests.Count(r => r.UserId == 5));
        Assert.DoesNotContain(_dispatcher.Requests, r => r.DedupeKey!.Contains(":fb:"));
    }

    [Fact]
    public async Task Unmapped_owner_skips_owner_notifications_but_still_notifies_customers()
    {
        _userDirectory.GetOwnerUserIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((int?)null);

        await Create().HandleLotEmergencyStoppedAsync(StoppedEvent(Affected(12, "BK-0001", 5)), EventId);

        Assert.Equal(3, _dispatcher.Requests.Count); // chỉ khách
        Assert.DoesNotContain(_dispatcher.Requests, r => r.UserId == 9);
        var alert = Assert.Single(_alertStore.Alerts);
        Assert.Equal(1, alert.TotalRecipients);
    }

    [Fact]
    public async Task Lifted_notifies_owner_reopened_and_broadcasts_active()
    {
        var lifted = new LotEmergencyLifted(3, "Bãi Vincom", OwnerProfileId: 7, LiftedByUserId: 10,
            Reason: "Đã xử lý xong", LiftedAtUtc: new DateTime(2026, 11, 5, 4, 0, 0, DateTimeKind.Utc));

        await Create().HandleLotEmergencyLiftedAsync(lifted, EventId);

        Assert.All(_dispatcher.Requests, r =>
        {
            Assert.Equal("LOT_REOPENED", r.TemplateKey);
            Assert.Equal(9, r.UserId);
        });
        Assert.Contains(_dispatcher.Requests, r => r.Channel == NotificationChannel.InApp);
        Assert.Contains(_dispatcher.Requests, r => r.Channel == NotificationChannel.Email);
        var message = Assert.Single(_hub.Messages);
        Assert.False(message.EmergencyStopped);
        Assert.Equal("Active", message.Status);
    }

    [Fact]
    public async Task Stale_data_alerts_owner_and_broadcasts_stale_state()
    {
        var stale = new LotDataMarkedStale(3, "Bãi Vincom", OwnerProfileId: 7,
            LastHeartbeatAtUtc: new DateTime(2026, 11, 5, 1, 0, 0, DateTimeKind.Utc),
            DetectedAtUtc: new DateTime(2026, 11, 5, 1, 30, 0, DateTimeKind.Utc));

        await Create().HandleLotDataMarkedStaleAsync(stale, EventId);

        Assert.All(_dispatcher.Requests, r => Assert.Equal("LOT_DATA_STALE", r.TemplateKey));
        Assert.Contains(_dispatcher.Requests, r => r.Channel == NotificationChannel.InApp && r.DedupeKey == $"evt:{EventId}:u9:InApp");
        Assert.Contains(_dispatcher.Requests, r => r.Channel == NotificationChannel.Email);
        var message = Assert.Single(_hub.Messages);
        Assert.True(message.IsDataStale);
    }

    [Fact]
    public async Task Recovered_notifies_owner_inapp_and_clears_stale_broadcast()
    {
        var recovered = new LotDataRecovered(3, "Bãi Vincom", OwnerProfileId: 7,
            HeartbeatAtUtc: new DateTime(2026, 11, 5, 1, 40, 0, DateTimeKind.Utc));

        await Create().HandleLotDataRecoveredAsync(recovered, EventId);

        var sent = Assert.Single(_dispatcher.Requests);
        Assert.Equal("LOT_DATA_RECOVERED", sent.TemplateKey);
        Assert.Equal(NotificationChannel.InApp, sent.Channel);
        var message = Assert.Single(_hub.Messages);
        Assert.False(message.IsDataStale);
    }

    private sealed class FakeAlertStore : IEmergencyAlertStore
    {
        public List<NotificationService.Domain.Entities.EmergencyAlert> Alerts { get; } = [];

        public Task AddAsync(NotificationService.Domain.Entities.EmergencyAlert alert, CancellationToken cancellationToken = default)
        {
            Alerts.Add(alert);
            return Task.CompletedTask;
        }

        public Task<NotificationService.Domain.Entities.EmergencyAlert?> GetByBatchKeyAsync(string batchKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Alerts.FirstOrDefault(a => a.BatchKey == batchKey));

        public Task<List<NotificationService.Domain.Entities.EmergencyAlert>> ListByLotAsync(int parkingLotId, int take, CancellationToken cancellationToken = default)
            => Task.FromResult(Alerts.Where(a => a.ParkingLotId == parkingLotId).Take(take).ToList());
    }

    private sealed class RecordingHub : ILotHubBroadcaster
    {
        public List<LotStatusMessage> Messages { get; } = [];

        public Task BroadcastLotStatusAsync(LotStatusMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>Dispatcher ghi lại request; StatusByChannel mô phỏng kênh lỗi (mặc định Sent).</summary>
    private sealed class RecordingDispatcher : INotificationDispatcher
    {
        public List<SendNotificationRequest> Requests { get; } = [];
        public Dictionary<NotificationChannel, NotificationStatus> StatusByChannel { get; } = [];

        public Task<SendNotificationResponse> SendAsync(SendNotificationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var status = StatusByChannel.TryGetValue(request.Channel, out var s) ? s : NotificationStatus.Sent;
            return Task.FromResult(new SendNotificationResponse(1, status, "ok"));
        }

        public Task<Dictionary<NotificationChannel, SendNotificationResponse>> SendMultiChannelAsync(
            int userId, string templateKey, string title, string body, NotificationChannel[] channels,
            string? dataJson = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("US-086 gửi từng kênh qua SendAsync để đọc trạng thái fallback.");

        public Task<SendNotificationResponse> RetryAsync(int notificationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Retry nằm trong dispatcher nội bộ.");
    }
}

