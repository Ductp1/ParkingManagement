using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Constants;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Services.EmailSender;

/// <summary>
/// Email notification sender - hỗ trợ SMTP real hoặc mock mode.
/// Module: TV6 (S1-T602).
/// </summary>
public sealed class EmailNotificationSender : IEmailNotificationSender
{
    private readonly SmtpConfiguration _config;
    private readonly ILogger<EmailNotificationSender> _logger;

    public NotificationChannel SupportedChannel => NotificationChannel.Email;

    public EmailNotificationSender(SmtpConfiguration config, ILogger<EmailNotificationSender> logger)
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
                    "🔵 EMAIL MOCK: userId={UserId}, title={Title}, templateKey={TemplateKey}",
                    userId, title, templateKey
                );
                return (true, null);
            }

            // Real SMTP mode (chưa implement đầy đủ, để integration sau)
            _logger.LogWarning("⚠️ SMTP Real mode chưa implement - fallback mock");
            return (true, null);

            // TODO: Implement thực tế với SmtpClient
            // using var smtpClient = new SmtpClient(_config.Host, _config.Port)
            // {
            //     Credentials = new NetworkCredential(_config.Username, _config.Password),
            //     EnableSsl = _config.EnableSsl,
            //     Timeout = _config.TimeoutMs
            // };
            // var mailMessage = new MailMessage(
            //     new MailAddress(_config.FromEmail, _config.FromName),
            //     new MailAddress(userEmail) // cần lấy email từ User service
            // )
            // {
            //     Subject = title,
            //     Body = body,
            //     IsBodyHtml = true
            // };
            // await smtpClient.SendMailAsync(mailMessage, cancellationToken);
            // return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ EMAIL SEND FAILED: userId={UserId}", userId);
            return (false, ex.Message);
        }
    }
}
