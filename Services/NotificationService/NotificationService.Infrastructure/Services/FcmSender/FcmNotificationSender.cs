using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.DeviceTokens;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Services.FcmSender;

/// <summary>
/// Firebase Cloud Messaging (FCM) push sender - mock mode (mặc định) hoặc FCM HTTP v1 thật.
/// Đường thật KHÔNG cần SDK FirebaseAdmin: tự ký JWT RS256 từ service account JSON
/// (client_email + private_key) → đổi lấy OAuth2 access_token → POST
/// /v1/projects/{projectId}/messages:send cho TỪNG device token của user (token lấy từ
/// bảng DeviceTokens qua IDeviceTokenRepository; token Google thu hồi (404/410) bị đánh dấu
/// IsRevoked). Secret đặt qua env FcmConfiguration__ServiceAccountKey – KHÔNG commit.
/// Module: TV6 (S1-T602).
/// </summary>
public sealed class FcmNotificationSender : IFcmNotificationSender
{
    private const string OAuthScope = "https://www.googleapis.com/auth/firebase.messaging";
    private const string OAuthTokenUrl = "https://oauth2.googleapis.com/token";
    private const string FcmSendUrl = "https://fcm.googleapis.com/v1/projects/{0}/messages:send";

    private readonly HttpClient _httpClient;
    private readonly FcmConfiguration _config;
    private readonly IDeviceTokenRepository _deviceTokens;
    private readonly ILogger<FcmNotificationSender> _logger;

    public NotificationChannel SupportedChannel => NotificationChannel.Push;

    public FcmNotificationSender(HttpClient httpClient, FcmConfiguration config, IDeviceTokenRepository deviceTokens, ILogger<FcmNotificationSender> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _deviceTokens = deviceTokens ?? throw new ArgumentNullException(nameof(deviceTokens));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChannelSendResult> SendAsync(
        Notification notification,
        CancellationToken cancellationToken = default
    )
    {
        if (_config.UseMock)
        {
            _logger.LogInformation(
                "🔔 FCM MOCK: userId={UserId}, title={Title}, templateKey={TemplateKey}",
                notification.UserId, notification.Title, notification.TemplateKey
            );
            return ChannelSendResult.Ok();
        }

        // UseMock=false: kiểm tra cấu hình trước – thiếu → PermanentFailure (retry vô ích).
        if (string.IsNullOrWhiteSpace(_config.ProjectId) || string.IsNullOrWhiteSpace(_config.ServiceAccountKey))
        {
            return ChannelSendResult.PermanentFailure(
                "FCM chưa được cấu hình. Cần: 1) FcmConfiguration__ProjectId, " +
                "2) FcmConfiguration__ServiceAccountKey (path file service-account JSON, JSON thuần hoặc base64 – đặt qua env). " +
                "Để chạy dev/test, đặt FcmConfiguration__UseMock=true.");
        }

        var devices = await _deviceTokens.GetActiveByUserAsync(notification.UserId, cancellationToken);
        if (devices.Count == 0)
        {
            return ChannelSendResult.PermanentFailure(
                $"User {notification.UserId} chưa đăng ký device token nào (POST /api/v1/devices) nên không thể gửi push.");
        }

        string accessToken;
        try
        {
            accessToken = await GetGoogleAccessTokenAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Lỗi OAuth (mạng chập chờn, Google lỗi tạm thời) → transient để retry.
            _logger.LogError(ex, "❌ FCM OAuth failed: userId={UserId}", notification.UserId);
            return ChannelSendResult.TransientFailure($"Lỗi lấy access token từ Google: {ex.Message}");
        }

        var sent = 0;
        var lastError = string.Empty;
        var lastErrorTransient = false;

        foreach (var device in devices)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_config.TimeoutMs);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, string.Format(FcmSendUrl, _config.ProjectId));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = JsonContent.Create(new
                {
                    message = new
                    {
                        token = device.Token,
                        notification = new { title = notification.Title, body = notification.Body },
                        data = ParseDataPayload(notification.DataJson)
                    }
                });

                using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                if (response.IsSuccessStatusCode)
                {
                    sent++;
                    continue;
                }

                var error = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                lastError = $"HTTP {(int)response.StatusCode}: {error}";
                lastErrorTransient = (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests;

                // Google báo token không còn hợp lệ → thu hồi để lần sau không gửi lại.
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                    await _deviceTokens.RevokeByUserAndTokenAsync(device.UserId, device.Token, CancellationToken.None);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                lastError = $"FCM timeout sau {_config.TimeoutMs} ms";
                lastErrorTransient = true;
            }
            catch (HttpRequestException ex)
            {
                lastError = ex.Message;
                lastErrorTransient = true;
            }
        }

        if (sent > 0)
        {
            _logger.LogInformation("✅ FCM SENT: userId={UserId}, sent={Sent}/{Total}",
                notification.UserId, sent, devices.Count);
            return ChannelSendResult.Ok();
        }

        _logger.LogError("❌ FCM SEND FAILED: userId={UserId}, devices={Count}, error={Error}",
            notification.UserId, devices.Count, lastError);
        return lastErrorTransient
            ? ChannelSendResult.TransientFailure(lastError)
            : ChannelSendResult.PermanentFailure(lastError);
    }

    /// <summary>Đổi service account JSON lấy OAuth2 access_token qua JWT RS256 tự ký (không cần SDK).</summary>
    private async Task<string> GetGoogleAccessTokenAsync(CancellationToken cancellationToken)
    {
        var (clientEmail, privateKeyPem) = ReadServiceAccount();
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var now = DateTimeOffset.UtcNow;
        string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string B64Json(object value) => B64(JsonSerializer.SerializeToUtf8Bytes(value));

        var unsigned = $"{B64Json(new { alg = "RS256", typ = "JWT" })}." +
                       B64Json(new
                       {
                           iss = clientEmail,
                           scope = OAuthScope,
                           aud = OAuthTokenUrl,
                           iat = now.ToUnixTimeSeconds(),
                           exp = now.AddHours(1).ToUnixTimeSeconds()
                       });
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var assertion = $"{unsigned}.{B64(signature)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, OAuthTokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion
            })
        };
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken);
        return token?.AccessToken ?? throw new InvalidOperationException("Google không trả access_token.");
    }

    /// <summary>ServiceAccountKey = path file JSON, JSON thuần, hoặc base64 của JSON.</summary>
    private (string ClientEmail, string PrivateKeyPem) ReadServiceAccount()
    {
        var raw = _config.ServiceAccountKey.Trim();
        var json = File.Exists(raw) ? File.ReadAllText(raw)
            : raw.Contains("client_email") ? raw
            : Encoding.UTF8.GetString(Convert.FromBase64String(raw));

        using var doc = JsonDocument.Parse(json);
        return (GetString(doc, "client_email"), GetString(doc, "private_key"));

        static string GetString(JsonDocument doc, string name)
            => doc.RootElement.GetProperty(name).GetString()
               ?? throw new InvalidOperationException($"Service account JSON thiếu '{name}'.");
    }

    /// <summary>DataJson → data payload của FCM (mọi giá trị phải là string).</summary>
    private static Dictionary<string, string> ParseDataPayload(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return new Dictionary<string, string>();

        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            return doc.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.ToString() ?? string.Empty);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private sealed record GoogleTokenResponse([property: JsonPropertyName("access_token")] string? AccessToken);
}
