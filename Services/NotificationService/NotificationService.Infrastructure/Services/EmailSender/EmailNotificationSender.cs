using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Services.EmailSender;

/// <summary>
/// Email notification sender - SMTP thật (System.Net.Mail) hoặc mock mode.
/// Người nhận được tra từ UserService qua port IUserDirectory (email thuộc dữ liệu UserService).
/// Secret lấy từ configuration/env (SmtpConfiguration__Password), không hardcode.
/// Thiếu cấu hình / không resolve được email khi UseMock=false → lỗi VĨNH VIỄN kèm hướng dẫn.
/// Module: TV6 (S1-T602).
/// </summary>
public sealed class EmailNotificationSender : IEmailNotificationSender
{
    private readonly SmtpConfiguration _config;
    private readonly IUserDirectory _userDirectory;
    private readonly ILogger<EmailNotificationSender> _logger;

    public NotificationChannel SupportedChannel => NotificationChannel.Email;

    public EmailNotificationSender(SmtpConfiguration config, IUserDirectory userDirectory, ILogger<EmailNotificationSender> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _userDirectory = userDirectory ?? throw new ArgumentNullException(nameof(userDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChannelSendResult> SendAsync(
        Notification notification,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (_config.UseMock)
            {
                _logger.LogInformation(
                    "🔵 EMAIL MOCK: userId={UserId}, title={Title}, templateKey={TemplateKey}",
                    notification.UserId, notification.Title, notification.TemplateKey
                );
                return ChannelSendResult.Ok();
            }

            var configError = ValidateConfiguration();
            if (configError is not null)
                return ChannelSendResult.PermanentFailure(configError);

            var recipientEmail = await _userDirectory.GetEmailAsync(notification.UserId, cancellationToken);
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return ChannelSendResult.PermanentFailure(
                    $"Không tìm thấy email của userId={notification.UserId} trên UserService " +
                    "(user chưa khai báo email, tài khoản bị khoá, hoặc UserService chưa chạy).");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_config.TimeoutMs);

#pragma warning disable SYSLIB0014 // SmtpClient legacy nhưng đủ dùng, không cần thêm package ngoài.
            using var smtpClient = new SmtpClient(_config.Host, _config.Port)
            {
                EnableSsl = _config.EnableSsl,
                Timeout = _config.TimeoutMs
            };
            if (!string.IsNullOrWhiteSpace(_config.Username))
                smtpClient.Credentials = new NetworkCredential(_config.Username, _config.Password);

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_config.FromEmail, _config.FromName),
                Subject = notification.Title,
                Body = notification.Body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(recipientEmail);

            await smtpClient.SendMailAsync(mailMessage, timeoutCts.Token);
#pragma warning restore SYSLIB0014

            _logger.LogInformation("✅ EMAIL SENT: userId={UserId}, to={Email}", notification.UserId, recipientEmail);
            return ChannelSendResult.Ok();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ChannelSendResult.TransientFailure($"SMTP timeout sau {_config.TimeoutMs} ms");
        }
        // Lỗi VĨNH VIỄN (5xx: mailbox không tồn tại, user không local) → Failed ngay, không retry.
        // Lỗi 4xx tạm thời (VD LocalErrorInProcessing 451) rơi xuống catch bên dưới → TransientFailure.
        catch (SmtpException ex) when (ex.StatusCode is SmtpStatusCode.MailboxUnavailable
            or SmtpStatusCode.UserNotLocalTryAlternatePath)
        {
            return ChannelSendResult.PermanentFailure($"SMTP từ chối: {ex.Message}");
        }
        catch (SmtpException ex)
        {
            return ChannelSendResult.TransientFailure($"SMTP lỗi tạm thời ({ex.StatusCode}): {ex.Message}");
        }
        catch (SocketException ex)
        {
            return ChannelSendResult.TransientFailure($"Không kết nối được SMTP: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ EMAIL SEND FAILED: userId={UserId}", notification.UserId);
            return ChannelSendResult.TransientFailure(ex.Message);
        }
    }

    /// <summary>Kiểm tra cấu hình SMTP; trả hướng dẫn nếu thiếu.</summary>
    private string? ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_config.Host) || _config.Host == "localhost")
            return "SMTP chưa được cấu hình (SmtpConfiguration:Host trống/localhost). " +
                   "Cấu hình: đặt Host/Port/Username trong appsettings.json, " +
                   "mật khẩu qua env SmtpConfiguration__Password (KHÔNG commit secret), " +
                   "rồi đặt SmtpConfiguration__UseMock=false.";

        if (string.IsNullOrWhiteSpace(_config.FromEmail) || !_config.FromEmail.Contains('@'))
            return "SmtpConfiguration:FromEmail không hợp lệ – cần địa chỉ email người gửi.";

        return null;
    }
}
