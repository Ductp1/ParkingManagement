using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Services.SmsSender;

/// <summary>
/// SMS notification sender - mock mode (mặc định) hoặc Twilio REST API (không cần SDK:
/// Basic auth + form POST /2010-04-01/Accounts/{sid}/Messages.json).
/// ApiKey = Twilio Account SID, ApiSecret = Auth Token (env SmsConfiguration__ApiSecret – KHÔNG commit secret).
/// Số người nhận tra từ UserService qua IUserDirectory. Provider khác Mock/Twilio → PermanentFailure
/// rõ ràng (không giả success). Module: TV6 (S1-T602).
/// </summary>
public sealed class SmsNotificationSender : ISmsNotificationSender
{
    private readonly HttpClient _httpClient;
    private readonly SmsConfiguration _config;
    private readonly IUserDirectory _userDirectory;
    private readonly ILogger<SmsNotificationSender> _logger;
    private int _mockAttemptCounter;

    public NotificationChannel SupportedChannel => NotificationChannel.Sms;

    public SmsNotificationSender(HttpClient httpClient, SmsConfiguration config, IUserDirectory userDirectory, ILogger<SmsNotificationSender> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _userDirectory = userDirectory ?? throw new ArgumentNullException(nameof(userDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChannelSendResult> SendAsync(
        Notification notification,
        CancellationToken cancellationToken = default
    )
    {
        if (_config.UseMock)
            return SendMock(notification);

        // Chỉ Twilio được hỗ trợ ở đường thật; provider khác → PermanentFailure rõ ràng.
        if (!string.Equals(_config.Provider, "Twilio", StringComparison.OrdinalIgnoreCase))
        {
            return ChannelSendResult.PermanentFailure(
                $"SMS provider '{_config.Provider}' chưa được hỗ trợ. " +
                "Các provider khả dụng: Mock, Twilio (đặt SmsConfiguration__Provider=Twilio + ApiKey/ApiSecret).");
        }

        if (string.IsNullOrWhiteSpace(_config.ApiKey) || string.IsNullOrWhiteSpace(_config.ApiSecret))
        {
            return ChannelSendResult.PermanentFailure(
                "Twilio thiếu cấu hình. Cần SmsConfiguration__ApiKey (Account SID) và " +
                "SmsConfiguration__ApiSecret (Auth Token) – đặt qua env, KHÔNG commit secret.");
        }

        var to = await _userDirectory.GetPhoneNumberAsync(notification.UserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(to))
        {
            return ChannelSendResult.PermanentFailure(
                $"Không tìm thấy số điện thoại của userId={notification.UserId} trên UserService " +
                "(user chưa khai báo SĐT, tài khoản bị khoá, hoặc UserService chưa chạy).");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_config.TimeoutMs);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.twilio.com/2010-04-01/Accounts/{_config.ApiKey}/Messages.json")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["To"] = to,
                    ["From"] = _config.FromNumber,
                    ["Body"] = notification.Body
                })
            };
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ApiKey}:{_config.ApiSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
            var responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ SMS SENT via Twilio: userId={UserId}, to={To}", notification.UserId, to);
                return ChannelSendResult.Ok();
            }

            // 5xx / 429 = lỗi tạm thời (retry được); 4xx khác = từ chối vĩnh viễn (sai số, sai thông số).
            return response.StatusCode >= HttpStatusCode.InternalServerError || response.StatusCode == HttpStatusCode.TooManyRequests
                ? ChannelSendResult.TransientFailure($"Twilio {(int)response.StatusCode}: {responseBody}")
                : ChannelSendResult.PermanentFailure($"Twilio từ chối ({(int)response.StatusCode}): {responseBody}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ChannelSendResult.TransientFailure($"Twilio timeout sau {_config.TimeoutMs} ms");
        }
        catch (HttpRequestException ex)
        {
            return ChannelSendResult.TransientFailure($"Không kết nối được Twilio: {ex.Message}");
        }
    }

    /// <summary>
    /// Mock: MockAlwaysFail → luôn transient-fail;
    /// MockFailFirstAttempts=n → n lần đầu transient-fail, các lần sau thành công.
    /// </summary>
    private ChannelSendResult SendMock(Notification notification)
    {
        if (_config.MockAlwaysFail)
        {
            _logger.LogWarning(
                "📱 SMS MOCK FAIL (always): userId={UserId}, title={Title}",
                notification.UserId, notification.Title
            );
            return ChannelSendResult.TransientFailure("SMS mock: MockAlwaysFail=true (giả lập lỗi mạng)");
        }

        var attempt = Interlocked.Increment(ref _mockAttemptCounter);
        if (attempt <= _config.MockFailFirstAttempts)
        {
            _logger.LogWarning(
                "📱 SMS MOCK FAIL (attempt {Attempt}/{FailFirst}): userId={UserId}",
                attempt, _config.MockFailFirstAttempts, notification.UserId
            );
            return ChannelSendResult.TransientFailure(
                $"SMS mock: lỗi tạm thời lần {attempt} (MockFailFirstAttempts={_config.MockFailFirstAttempts})");
        }

        _logger.LogInformation(
            "📱 SMS MOCK SENT: userId={UserId}, title={Title}, body={Body}",
            notification.UserId, notification.Title, notification.Body
        );
        return ChannelSendResult.Ok();
    }
}
