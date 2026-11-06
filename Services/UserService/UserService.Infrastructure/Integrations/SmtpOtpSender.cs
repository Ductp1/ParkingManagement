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

    public Task SendAsync(Guid eventId, string destination, string code, DateTime expiresAtUtc, CancellationToken ct)
        => SendAsync(eventId, destination, code, expiresAtUtc, null, ct);

    /// <summary>Email OTP (HTML + chữ thuần), tiêu đề theo mục đích của mã.</summary>
    public Task SendAsync(Guid eventId, string destination, string code, DateTime expiresAtUtc, string? purpose, CancellationToken ct)
        => DeliverAsync($"otp-{eventId:N}", destination, EmailTemplates.Otp(code, expiresAtUtc, purpose), ct);

    /// <summary>Email gửi tài khoản + mật khẩu tạm cho nhân viên trực cổng mới (US-075).</summary>
    public Task SendStaffCredentialsAsync(Guid eventId, string destination, string fullName, string temporaryPassword,
        string ownerName, string lotName, CancellationToken ct)
        => DeliverAsync($"staff-{eventId:N}", destination,
            EmailTemplates.StaffCredentials(fullName, destination, temporaryPassword, ownerName, lotName, LoginUrl()), ct);

    /// <summary>Trang đăng nhập trong email; cấu hình App:LoginUrl, mặc định frontend chạy local.</summary>
    private string LoginUrl() => string.IsNullOrWhiteSpace(config["App:LoginUrl"]) ? "http://localhost:5173/login" : config["App:LoginUrl"]!;

    private async Task DeliverAsync(string messageId, string destination, EmailTemplates.Email email, CancellationToken ct)
    {
        EnsureAvailable();
        if (!MailboxAddress.TryParse(destination, out var recipient))
            throw new DependencyUnavailableException("Kênh email không hỗ trợ số điện thoại.");
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Otp:Smtp:FromName"] ?? "ParkMaster", config["Otp:Smtp:FromAddress"]!));
        message.To.Add(recipient);
        message.MessageId = $"{messageId}@parkingmanagement.local";
        message.Subject = email.Subject;
        message.Body = new BodyBuilder { HtmlBody = email.Html, TextBody = email.Text }.ToMessageBody();
        // Kiểm tra thu hồi chứng chỉ (CRL/OCSP) hay bị mạng/phần mềm diệt virus chặn → SslHandshakeException.
        // Mặc định tắt; chuỗi chứng chỉ và tên máy chủ vẫn được kiểm tra. Bật lại bằng Otp:Smtp:CheckCertificateRevocation = true.
        string? certProblem = null;
        var allowInvalid = config.GetValue("Otp:Smtp:AllowInvalidCertificate", false);
        using var client = new SmtpClient
        {
            Timeout = 10000,
            CheckCertificateRevocation = config.GetValue("Otp:Smtp:CheckCertificateRevocation", false),
            // Ghi lại ai cấp chứng chỉ để biết phần mềm nào đang chặn SSL. Chỉ bỏ qua lỗi khi bật
            // Otp:Smtp:AllowInvalidCertificate (CHỈ dùng khi dev, không bật trên production).
            ServerCertificateValidationCallback = (_, cert, _, errors) =>
            {
                if (errors == System.Net.Security.SslPolicyErrors.None) return true;
                certProblem = $"Chứng chỉ SMTP bị từ chối ({errors}); cấp bởi: {cert?.Issuer ?? "không rõ"}; chủ thể: {cert?.Subject ?? "không rõ"}";
                return allowInvalid;
            }
        };
        var port = config.GetValue("Otp:Smtp:Port", 587);
        try
        {
            await client.ConnectAsync(config["Otp:Smtp:Host"]!, port,
                port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        }
        catch (SslHandshakeException ex) when (certProblem is not null)
        {
            throw new InvalidOperationException(certProblem, ex);
        }
        await client.AuthenticateAsync(config["Otp:Smtp:Username"]!, config["Otp:Smtp:Password"]!, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
