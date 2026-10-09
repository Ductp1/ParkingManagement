using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using ParkingManagement.SharedKernel.Exceptions;

namespace UserService.Infrastructure.Integrations;

public sealed class SmtpOtpSender(IConfiguration config)
{
    public void EnsureAvailable()
    {
        if (string.IsNullOrWhiteSpace(config["Otp:Smtp:Host"])
            || !MailboxAddress.TryParse(config["Otp:Smtp:FromAddress"], out _)
            || string.IsNullOrWhiteSpace(config["Otp:Smtp:Username"])
            || string.IsNullOrWhiteSpace(config["Otp:Smtp:Password"])
            || config.GetValue("Otp:Smtp:Port", 587) is < 1 or > 65535)
            throw new DependencyUnavailableException("Chưa cấu hình tài khoản SMTP gửi OTP email.");
    }

    public async Task SendAsync(Guid eventId, string destination, string code, DateTime expiresAtUtc, CancellationToken ct)
    {
        EnsureAvailable();
        if (!MailboxAddress.TryParse(destination, out var recipient))
            throw new DependencyUnavailableException("Kênh OTP email không hỗ trợ số điện thoại.");
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Otp:Smtp:FromName"] ?? "ParkingManagement", config["Otp:Smtp:FromAddress"]!));
        message.To.Add(recipient);
        message.MessageId = $"otp-{eventId:N}@parkingmanagement.local";
        message.Subject = "Mã xác minh ParkingManagement";
        message.Body = new TextPart("plain") { Text = $"Mã xác minh của bạn: {code}\nMã hết hạn lúc {expiresAtUtc:HH:mm:ss} UTC. Không chia sẻ mã này với người khác." };
        using var client = new SmtpClient { Timeout = 10000 };
        var port = config.GetValue("Otp:Smtp:Port", 587);
        await client.ConnectAsync(config["Otp:Smtp:Host"]!, port,
            port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(config["Otp:Smtp:Username"]!, config["Otp:Smtp:Password"]!, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
