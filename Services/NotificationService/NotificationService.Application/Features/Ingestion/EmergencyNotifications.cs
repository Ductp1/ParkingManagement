using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Ingestion;

/// <summary>
/// US-086: thông báo KHẨN CẤP đa kênh + theo dõi trạng thái gửi. Nguồn: event US-069/US-068 từ
/// ParkingService (LotEmergencyStopped/Lifted, LotDataMarkedStale/Recovered) qua TransactionalInbox.
/// Mỗi (người nhận × kênh) là 1 row Notification với DedupeKey riêng "evt:{EventId}:u{UserId}:{Channel}"
/// (replay idempotent); kênh gửi lỗi vĩnh viễn → fallback sang kênh kế tiếp (chain configurable),
/// DedupeKey fallback "evt:{EventId}:u{UserId}:fb:{Channel}". Trạng thái gửi tổng hợp theo BatchKey.
/// </summary>
public interface IEmergencyNotifications
{
    Task HandleLotEmergencyStoppedAsync(LotEmergencyStopped evt, Guid eventId, CancellationToken cancellationToken = default);
    Task HandleLotEmergencyLiftedAsync(LotEmergencyLifted evt, Guid eventId, CancellationToken cancellationToken = default);
    Task HandleLotDataMarkedStaleAsync(LotDataMarkedStale evt, Guid eventId, CancellationToken cancellationToken = default);
    Task HandleLotDataRecoveredAsync(LotDataRecovered evt, Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>Cấu hình kênh gửi khẩn cấp (appsettings → "EmergencyNotifications").</summary>
public sealed class EmergencyNotificationOptions
{
    public const string SectionName = "EmergencyNotifications";
    /// <summary>Kênh BROADCAST cho KHÁCH bị ảnh hưởng — gửi đồng thời, tracking trạng thái từng kênh.</summary>
    public NotificationChannel[] CustomerChannels { get; set; } =
        [NotificationChannel.Push, NotificationChannel.Sms, NotificationChannel.Email];
    /// <summary>Kênh gửi cho CHỦ BÃI khi đóng/mở bãi.</summary>
    public NotificationChannel[] OwnerChannels { get; set; } = [NotificationChannel.InApp, NotificationChannel.Email];
    /// <summary>Kênh cảnh báo chủ bãi khi dữ liệu stale (US-068).</summary>
    public NotificationChannel[] StaleOwnerChannels { get; set; } = [NotificationChannel.InApp, NotificationChannel.Email];
    /// <summary>Kênh FALLBACK — chỉ dùng khi TOÀN BỘ kênh chính của 1 người nhận FAILED vĩnh viễn
    /// (dispatcher đã retry 3 lần với backoff). InApp là kênh cuối vì chạy nội bộ nền tảng.</summary>
    public NotificationChannel[] FallbackChannels { get; set; } = [NotificationChannel.InApp];
}

/// <summary>Port lưu metadata đợt khẩn cấp (Infrastructure EF cài bằng NotificationDbContext).</summary>
public interface IEmergencyAlertStore
{
    Task AddAsync(EmergencyAlert alert, CancellationToken cancellationToken = default);
    Task<EmergencyAlert?> GetByBatchKeyAsync(string batchKey, CancellationToken cancellationToken = default);
    Task<List<EmergencyAlert>> ListByLotAsync(int parkingLotId, int take, CancellationToken cancellationToken = default);
}

/// <summary>1 row trạng thái gửi (người nhận × kênh) — dùng cho tổng hợp US-086.</summary>
public sealed record EmergencyDeliveryRow(
    int UserId, NotificationChannel Channel, NotificationStatus Status,
    int RetryCount, string? LastError, DateTime? SentAtUtc, DateTime CreatedAtUtc);

/// <summary>Port đọc trạng thái gửi chi tiết theo đợt (Infrastructure EF).</summary>
public interface IEmergencyDeliveryQueries
{
    Task<IReadOnlyList<EmergencyDeliveryRow>> GetRowsByBatchKeyAsync(string batchKey, CancellationToken cancellationToken = default);
}

/// <summary>US-067: port đẩy real-time trạng thái bãi tới dashboard qua SignalR /hubs/parking
/// (implementation ở API layer — IHubContext&lt;ParkingHub&gt;; đăng ký DI BẮT BUỘC ở composition root).</summary>
public interface ILotHubBroadcaster
{
    Task BroadcastLotStatusAsync(LotStatusMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Payload event SignalR "LotStatusChanged" cho group "lot-{parkingLotId}".</summary>
public sealed record LotStatusMessage(
    int ParkingLotId, string LotName, string Status, bool EmergencyStopped, string? EmergencyReason,
    bool IsDataStale, DateTime TimestampUtc);

/// <summary>
/// Fan-out khẩn cấp. Gửi TRỰC TIẾP qua INotificationDispatcher (không qua ITransactionalNotificationSender)
/// vì cần đọc response.Status để thực hiện fallback channel — dispatcher vẫn giữ retry/backoff/idempotency.
/// </summary>
public sealed class EmergencyNotifications(
    INotificationDispatcher dispatcher,
    ITemplateRenderer renderer,
    IUserNotificationSettingsPort settings,
    IUserDirectory userDirectory,
    IEmergencyAlertStore alertStore,
    ILotHubBroadcaster hub,
    EmergencyNotificationOptions options) : IEmergencyNotifications
{
    public async Task HandleLotEmergencyStoppedAsync(LotEmergencyStopped evt, Guid eventId, CancellationToken cancellationToken = default)
    {
        var batchKey = $"evt:{eventId}";
        var ownerUserId = await userDirectory.GetOwnerUserIdAsync(evt.OwnerProfileId, cancellationToken);
        var recipients = evt.AffectedBookings.Select(b => b.UserId).Distinct().ToList();
        if (ownerUserId is > 0)
            recipients.Add(ownerUserId.Value);

        await alertStore.AddAsync(new EmergencyAlert
        {
            BatchKey = batchKey,
            ParkingLotId = evt.ParkingLotId,
            LotName = evt.LotName,
            OwnerProfileId = evt.OwnerProfileId,
            Reason = evt.Reason,
            StoppedAtUtc = evt.StoppedAtUtc,
            TotalRecipients = recipients.Count,
            StoppedByUserId = evt.StoppedByUserId,
        }, cancellationToken);

        // Mỗi người nhận nhận 1 bộ thông báo đa kênh (nhóm theo user — user có nhiều booking bị
        // ảnh hưởng vẫn chỉ nhận 1 lần/kênh; nội dung dùng booking sớm nhất, dataJson kèm batchKey).
        foreach (var booking in evt.AffectedBookings
                     .GroupBy(b => b.UserId)
                     .Select(g => g.OrderBy(b => b.StartAtUtc).First()))
        {
            var placeholders = new Dictionary<string, string>
            {
                ["BookingCode"] = booking.Code,
                ["LotName"] = evt.LotName,
                ["Reason"] = evt.Reason,
            };
            var dataJson = System.Text.Json.JsonSerializer.Serialize(
                new { bookingId = booking.BookingId, lotId = evt.ParkingLotId, batchKey });
            await FanOutAsync(booking.UserId, "EMERGENCY_CLOSURE", placeholders, dataJson,
                batchKey, eventId, options.CustomerChannels, cancellationToken);
        }

        if (ownerUserId is > 0)
        {
            var ownerPlaceholders = new Dictionary<string, string>
            {
                ["LotName"] = evt.LotName,
                ["Reason"] = evt.Reason,
                ["StoppedAt"] = VietnamTime.Format(evt.StoppedAtUtc),
                ["Count"] = evt.AffectedBookings.Count.ToString(),
            };
            var ownerData = System.Text.Json.JsonSerializer.Serialize(new { lotId = evt.ParkingLotId, batchKey });
            await FanOutAsync(ownerUserId.Value, "EMERGENCY_CLOSURE_OWNER", ownerPlaceholders, ownerData,
                batchKey, eventId, options.OwnerChannels, cancellationToken);
        }

        await hub.BroadcastLotStatusAsync(new LotStatusMessage(
            evt.ParkingLotId, evt.LotName, "Suspended", EmergencyStopped: true, evt.Reason,
            IsDataStale: false, evt.StoppedAtUtc), cancellationToken);
    }

    public async Task HandleLotEmergencyLiftedAsync(LotEmergencyLifted evt, Guid eventId, CancellationToken cancellationToken = default)
    {
        var batchKey = $"evt:{eventId}";
        var ownerUserId = await userDirectory.GetOwnerUserIdAsync(evt.OwnerProfileId, cancellationToken);
        if (ownerUserId is > 0)
        {
            var placeholders = new Dictionary<string, string>
            {
                ["LotName"] = evt.LotName,
                ["ReopenedAt"] = VietnamTime.Format(evt.LiftedAtUtc),
            };
            var dataJson = System.Text.Json.JsonSerializer.Serialize(new { lotId = evt.ParkingLotId, batchKey });
            await FanOutAsync(ownerUserId.Value, "LOT_REOPENED", placeholders, dataJson,
                batchKey, eventId, options.OwnerChannels, cancellationToken);
        }

        await hub.BroadcastLotStatusAsync(new LotStatusMessage(
            evt.ParkingLotId, evt.LotName, "Active", EmergencyStopped: false, null,
            IsDataStale: false, evt.LiftedAtUtc), cancellationToken);
    }

    public async Task HandleLotDataMarkedStaleAsync(LotDataMarkedStale evt, Guid eventId, CancellationToken cancellationToken = default)
    {
        var ownerUserId = await userDirectory.GetOwnerUserIdAsync(evt.OwnerProfileId, cancellationToken);
        if (ownerUserId is > 0)
        {
            var placeholders = new Dictionary<string, string>
            {
                ["LotName"] = evt.LotName,
                ["LastHeartbeat"] = evt.LastHeartbeatAtUtc is null ? "chưa từng" : VietnamTime.Format(evt.LastHeartbeatAtUtc.Value),
            };
            var dataJson = System.Text.Json.JsonSerializer.Serialize(
                new { lotId = evt.ParkingLotId, batchKey = $"evt:{eventId}" });
            await FanOutAsync(ownerUserId.Value, "LOT_DATA_STALE", placeholders, dataJson,
                $"evt:{eventId}", eventId, options.StaleOwnerChannels, cancellationToken);
        }

        await hub.BroadcastLotStatusAsync(new LotStatusMessage(
            evt.ParkingLotId, evt.LotName, "Active", EmergencyStopped: false, null,
            IsDataStale: true, evt.DetectedAtUtc), cancellationToken);
    }

    public async Task HandleLotDataRecoveredAsync(LotDataRecovered evt, Guid eventId, CancellationToken cancellationToken = default)
    {
        var ownerUserId = await userDirectory.GetOwnerUserIdAsync(evt.OwnerProfileId, cancellationToken);
        if (ownerUserId is > 0)
        {
            var placeholders = new Dictionary<string, string>
            {
                ["LotName"] = evt.LotName,
                ["Heartbeat"] = VietnamTime.Format(evt.HeartbeatAtUtc),
            };
            var dataJson = System.Text.Json.JsonSerializer.Serialize(
                new { lotId = evt.ParkingLotId, batchKey = $"evt:{eventId}" });
            await FanOutAsync(ownerUserId.Value, "LOT_DATA_RECOVERED", placeholders, dataJson,
                $"evt:{eventId}", eventId, [NotificationChannel.InApp], cancellationToken);
        }

        await hub.BroadcastLotStatusAsync(new LotStatusMessage(
            evt.ParkingLotId, evt.LotName, "Active", EmergencyStopped: false, null,
            IsDataStale: false, evt.HeartbeatAtUtc), cancellationToken);
    }

    /// <summary>
    /// Broadcast tới TOÀN BỘ channel list (multi-channel, tracking từng kênh). Nếu CẢ đợt kênh chính
    /// FAILED vĩnh viễn (sau retry của dispatcher) → fallback sang FallbackChannels (DedupeKey ":fb:").
    /// Pending = lỗi tạm thời còn dispatcher retry → không fallback chồng.
    /// </summary>
    private async Task FanOutAsync(int userId, string templateKey,
        IReadOnlyDictionary<string, string> placeholders, string dataJson,
        string batchKey, Guid eventId, IReadOnlyList<NotificationChannel> channels,
        CancellationToken cancellationToken)
    {
        var language = await settings.GetLanguageAsync(userId, cancellationToken);
        var attempted = new List<NotificationChannel>();
        var allFailed = true;

        foreach (var channel in channels)
        {
            var response = await SendAsync(userId, templateKey, channel, language, placeholders, dataJson,
                $"evt:{eventId}:u{userId}:{channel}", cancellationToken);
            if (response is null) continue; // thiếu mẫu cho kênh này (chưa seed vi/en) → bỏ qua kênh.
            attempted.Add(channel);
            allFailed &= response.Status == NotificationStatus.Failed;
        }

        if (attempted.Count == 0 || !allFailed)
            return; // có kênh thành công/còn retry, hoặc thiếu mẫu hoàn toàn → không fallback.

        foreach (var channel in options.FallbackChannels)
        {
            if (attempted.Contains(channel)) continue; // kênh fallback đã nằm trong đợt chính → không gửi nhấy lại.
            var response = await SendAsync(userId, templateKey, channel, language, placeholders, dataJson,
                $"evt:{eventId}:u{userId}:fb:{channel}", cancellationToken);
            if (response is { Status: not NotificationStatus.Failed })
                return; // fallback thành công (hoặc còn retry) → dừng chuỗi.
        }
    }

    /// <summary>Render + gửi 1 kênh; trả null khi thiếu mẫu (không chết cả đợt vì thiếu seed vi/en).</summary>
    private async Task<SendNotificationResponse?> SendAsync(int userId, string templateKey, NotificationChannel channel,
        string language, IReadOnlyDictionary<string, string> placeholders, string dataJson,
        string dedupeKey, CancellationToken cancellationToken)
    {
        RenderedTemplate rendered;
        try
        {
            rendered = await renderer.RenderAsync(templateKey, channel, language, placeholders, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return await dispatcher.SendAsync(new SendNotificationRequest(
            UserId: userId, Channel: channel, TemplateKey: templateKey,
            Title: rendered.Title, Body: rendered.Body, DataJson: dataJson,
            TemplateId: rendered.TemplateId, DedupeKey: dedupeKey), cancellationToken);
    }
}

