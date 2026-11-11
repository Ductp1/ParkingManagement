using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using ParkingService.Application.Abstractions;

namespace ParkingService.Infrastructure.Integrations;

/// <summary>
/// ADAPTER IPmsWebhookSender (US-052, UC-51 bước 1): POST JSON về WebhookUrl của PMS.
/// Header: X-SmartParking-Event, X-SmartParking-Timestamp (unix giây) và, khi bãi có secret,
/// X-SmartParking-Signature = "sha256=" + HMAC-SHA256(key, "{timestamp}.{body}") – PMS kiểm để chống giả mạo/replay.
/// </summary>
public sealed class PmsWebhookSender(HttpClient http, TimeProvider timeProvider, ILogger<PmsWebhookSender> logger) : IPmsWebhookSender
{
    public async Task<PmsWebhookResult> SendAsync(string webhookUrl, string? signingKey, string eventType, string payloadJson,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            return new PmsWebhookResult(false, null, $"WebhookUrl không hợp lệ: {webhookUrl}");

        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-SmartParking-Event", eventType);
        request.Headers.Add("X-SmartParking-Timestamp", timestamp);
        if (!string.IsNullOrWhiteSpace(signingKey))
            request.Headers.Add("X-SmartParking-Signature", "sha256=" + Sign(signingKey, $"{timestamp}.{payloadJson}"));

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? new PmsWebhookResult(true, (int)response.StatusCode, null)
                : new PmsWebhookResult(false, (int)response.StatusCode, $"PMS trả HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Không gọi được webhook PMS {Url}", uri.Host);
            return new PmsWebhookResult(false, null, ex is TaskCanceledException ? "Timeout khi gọi webhook PMS" : ex.Message);
        }
    }

    private static string Sign(string key, string message)
        => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(message)));
}
