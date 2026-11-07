using System.Net.Http.Json;
using System.Text.Json;
using AdminService.Application.Features.Sanctions;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Integrations;

/// <summary>
/// US-097: gửi thông báo in-app cho chủ bãi qua Gateway – POST api/v1/notifications/send (NotificationService, chưa yêu cầu JWT nên
/// SanctionExpiryJob cũng gọi được). Status Sent / Pending (NotificationService tự gửi lại) = đã giao; Failed = lỗi.
/// </summary>
public sealed class NotificationClient(HttpClient http, ILogger<NotificationClient> logger) : INotificationClient
{
    private const string Path = "api/v1/notifications/send";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SendAsync(OwnerNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(Path, new
            {
                userId = notification.UserId,
                channel = "InApp",
                templateKey = notification.TemplateKey,
                title = notification.Title,
                body = notification.Body,
                dataJson = notification.DataJson,
            }, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("NotificationService trả HTTP {Status}: {Body}", (int)response.StatusCode, body);
                throw new DependencyUnavailableException($"NotificationService trả HTTP {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<SendResultJson>(JsonOptions, cancellationToken);
            if (string.Equals(result?.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("NotificationService không gửi được thông báo {TemplateKey} cho user {UserId}: {Message}",
                    notification.TemplateKey, notification.UserId, result?.Message);
                throw new DependencyUnavailableException("NotificationService không gửi được thông báo.");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Không gọi được NotificationService (POST {Path}).", Path);
            throw new DependencyUnavailableException("NotificationService hiện không phản hồi.");
        }
    }

    /// <summary>Status là enum dạng chuỗi (JsonStringEnumConverter của ServiceDefaults).</summary>
    private sealed record SendResultJson(int NotificationId, string? Status, string? Message);
}
