using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Constants;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Services.SmsSender;

/// <summary>
/// SMS notification sender - hỗ trợ mock mode và provider integration.
/// Module: TV6 (S1-T602).
/// </summary>
public sealed class SmsNotificationSender : ISmsNotificationSender
{
    private readonly SmsConfiguration _config;
    private readonly ILogger<SmsNotificationSender> _logger;

    public NotificationChannel SupportedChannel => NotificationChannel.Sms;

    public SmsNotificationSender(SmsConfiguration config, ILogger<SmsNotificationSender> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(bool Success, string? ErrorMessage)> SendAsync(
        int userId,
        string title,
        string body,
        string? templateKey = null,
        string? dataJson = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (_config.UseMock)
            {
                // Mock mode: log và trả về success
                _logger.LogInformation(
                    "📱 SMS MOCK: userId={UserId}, title={Title}, body={Body}",
                    userId, title, body
                );
                return (true, null);
            }

            // Real SMS provider mode
            return _config.Provider.ToLower() switch
            {
                "twilio" => await SendViaTwilioAsync(userId, title, body, cancellationToken),
                "aws-sns" => await SendViaAwsSnsAsync(userId, title, body, cancellationToken),
                _ => (false, $"Unknown SMS provider: {_config.Provider}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ SMS SEND FAILED: userId={UserId}", userId);
            return (false, ex.Message);
        }
    }

    private async Task<(bool Success, string? ErrorMessage)> SendViaTwilioAsync(
        int userId,
        string title,
        string body,
        CancellationToken cancellationToken
    )
    {
        // TODO: Implement Twilio integration
        _logger.LogWarning("⚠️ Twilio integration not yet implemented");
        return (true, null); // Fallback to mock
    }

    private async Task<(bool Success, string? ErrorMessage)> SendViaAwsSnsAsync(
        int userId,
        string title,
        string body,
        CancellationToken cancellationToken
    )
    {
        // TODO: Implement AWS SNS integration
        _logger.LogWarning("⚠️ AWS SNS integration not yet implemented");
        return (true, null); // Fallback to mock
    }
}
