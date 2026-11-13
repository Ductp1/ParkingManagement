using NotificationService.Application.Features.Ingestion;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Integrations;

/// <summary>
/// Gửi thông báo transactional qua dispatcher TV6 (retry/backoff có sẵn), gắn DedupeKey chống replay.
/// Result Pending/Failed đã được dispatcher persist + log — không ném exception để tránh dead-letter
/// chỉ vì SMTP/SMS chớp mắt (dispatcher retry nội bộ 3 lần với exponential backoff đã xử lý).
/// </summary>
public sealed class DispatcherTransactionalNotificationSender(Application.Features.Dispatcher.INotificationDispatcher dispatcher)
    : ITransactionalNotificationSender
{
    public async Task SendAsync(int userId, NotificationChannel channel, string templateKey, string title, string body,
        string? dataJson, string? dedupeKey, CancellationToken cancellationToken = default)
    {
        var response = await dispatcher.SendAsync(new Application.Features.Dispatcher.SendNotificationRequest(
            UserId: userId, Channel: channel, TemplateKey: templateKey, Title: title, Body: body,
            DataJson: dataJson, DedupeKey: dedupeKey), cancellationToken);

        if (response.Status == NotificationStatus.Failed)
            throw new InvalidOperationException($"Gửi thông báo vĩnh viễn thất bại: {response.Message}");
    }
}

