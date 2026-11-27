using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Identity;
using UserService.Application.Features.Staff;
using UserService.Infrastructure.Persistence;

namespace UserService.Infrastructure.Integrations;

public sealed class OtpDeliveryConfiguration(IConfiguration config, IHostEnvironment environment) : IOtpDeliveryConfiguration
{
    public void EnsureDestination(string contact)
    {
        if (config["Otp:Transport"] == "Smtp" && !contact.Contains('@'))
            throw new ValidationException("Đăng ký hiện sử dụng OTP email. Vui lòng nhập địa chỉ email.");
    }
    public void EnsureAvailable()
    {
        if (config["Otp:Transport"] == "Smtp") new SmtpOtpSender(config).EnsureAvailable();
        else Endpoint(config["Otp:DeliveryUrl"], environment);
        if (!config.GetValue<bool>("EventBus:Enabled")) throw new DependencyUnavailableException("Outbox Dispatcher phải được bật để gửi OTP.");
    }
    public static Uri Endpoint(string? url, IHostEnvironment environment)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0
            || (uri.Scheme != "https" && !(environment.IsDevelopment() && uri.Scheme == "http" && uri.IsLoopback)))
            throw new DependencyUnavailableException("Chưa cấu hình endpoint HTTPS cho dịch vụ tích hợp.");
        return uri;
    }
}

public sealed class IdentityOutboxTransport(IHttpClientFactory clients, IConfiguration config, IHostEnvironment environment,
    ISecretCipher cipher, UserDbContext db, TimeProvider clock, RabbitMqOutboxTransport eventBus) : IOutboxTransport
{
    public async Task DeliverAsync(OutboxMessage message, CancellationToken ct)
    {
        object payload;
        Uri url; string? apiKey;
        if (message.EventType == "OtpDeliveryRequested")
        {
            var otp = JsonSerializer.Deserialize<OtpPayload>(message.PayloadJson)!;
            if (otp.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime) return; // Không gửi OTP đã hết hạn.
            if (!await db.OtpCodes.AnyAsync(o => o.Id == otp.OtpId && o.ConsumedAtUtc == null && o.ExpiresAtUtc > clock.GetUtcNow().UtcDateTime, ct)) return;
            if (config["Otp:Transport"] == "Smtp")
            {
                await new SmtpOtpSender(config).SendAsync(message.Id, otp.Destination,
                    cipher.Decrypt(otp.EncryptedCode, "otp-delivery"), otp.ExpiresAtUtc, otp.Purpose, ct);
                return;
            }
            url = OtpDeliveryConfiguration.Endpoint(config["Otp:DeliveryUrl"], environment);
            apiKey = config["Otp:ApiKey"];
            payload = new { message.Id, otp.Destination, otp.Purpose, otp.ExpiresAtUtc, Code = cipher.Decrypt(otp.EncryptedCode, "otp-delivery") };
        }
        else if (message.EventType == StaffUseCases.ActivationEvent)
        {
            var invite = JsonSerializer.Deserialize<StaffActivationPayload>(message.PayloadJson)!;
            var now = clock.GetUtcNow().UtcDateTime;
            // Link đã bị thu hồi (gửi lại / đã kích hoạt / xóa tài khoản) hoặc hết hạn trước khi kịp gửi → không gửi nữa.
            if (!await db.OtpCodes.AnyAsync(o => o.Id == invite.ActivationId && o.ConsumedAtUtc == null && o.ExpiresAtUtc > now, ct)) return;
            var link = SmtpOtpSender.StaffActivationLink(config, cipher.Decrypt(invite.EncryptedToken, StaffUseCases.ActivationCipherPurpose));
            if (config["Otp:Transport"] == "Smtp")
            {
                await new SmtpOtpSender(config).SendStaffActivationAsync(message.Id, invite.Destination, invite.FullName,
                    invite.OwnerName, invite.LotName, link, invite.ExpiresAtUtc, ct);
                return;
            }
            url = OtpDeliveryConfiguration.Endpoint(config["Otp:DeliveryUrl"], environment);
            apiKey = config["Otp:ApiKey"];
            payload = new { message.Id, invite.Destination, Purpose = "StaffActivation", invite.FullName, invite.OwnerName, invite.LotName,
                invite.ExpiresAtUtc, Link = link };
        }
        else if (message.EventType == OwnerAccountUseCases.ActivationEvent)
        {
            var activation = JsonSerializer.Deserialize<OwnerActivationPayload>(message.PayloadJson)!;
            var now = clock.GetUtcNow().UtcDateTime;
            // Link đã bị thu hồi (gửi lại link mới / đã kích hoạt) hoặc hết hạn trước khi kịp gửi → không gửi nữa.
            if (!await db.OtpCodes.AnyAsync(o => o.Id == activation.ActivationId && o.ConsumedAtUtc == null && o.ExpiresAtUtc > now, ct)) return;
            var link = SmtpOtpSender.OwnerActivationLink(config, cipher.Decrypt(activation.EncryptedToken, OwnerAccountUseCases.ActivationCipherPurpose));
            if (config["Otp:Transport"] == "Smtp")
            {
                await new SmtpOtpSender(config).SendOwnerActivationAsync(message.Id, activation.Destination, activation.FullName,
                    activation.BusinessName, link, activation.ExpiresAtUtc, ct);
                return;
            }
            url = OtpDeliveryConfiguration.Endpoint(config["Otp:DeliveryUrl"], environment);
            apiKey = config["Otp:ApiKey"];
            payload = new { message.Id, activation.Destination, Purpose = "OwnerActivation", activation.FullName, activation.BusinessName,
                activation.ExpiresAtUtc, Link = link };
        }
        else if (message.EventType == PartnerEmailUseCases.EmailEvent)
        {
            var mail = JsonSerializer.Deserialize<PartnerEmailPayload>(message.PayloadJson)!;
            var link = mail.EncryptedLink is null ? null : cipher.Decrypt(mail.EncryptedLink, PartnerEmailUseCases.CipherPurpose);
            if (config["Otp:Transport"] == "Smtp")
            {
                await new SmtpOtpSender(config).SendPartnerEmailAsync(message.Id, mail.Template, mail.Destination, mail.FullName,
                    mail.ApplicationCode, mail.Note, mail.Items ?? [], mail.DueAtUtc, link, ct);
                return;
            }
            url = OtpDeliveryConfiguration.Endpoint(config["Otp:DeliveryUrl"], environment);
            apiKey = config["Otp:ApiKey"];
            payload = new { message.Id, mail.Destination, Purpose = mail.Template, mail.FullName, mail.ApplicationCode, mail.Note,
                Items = mail.Items ?? [], mail.DueAtUtc, Link = link };
        }
        else
        {
            await eventBus.DeliverAsync(message, ct);
            return;
        }
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        requestMessage.Headers.Add("Idempotency-Key", message.Id.ToString());
        if (!string.IsNullOrWhiteSpace(apiKey)) requestMessage.Headers.Authorization = new("Bearer", apiKey);
        using var response = await clients.CreateClient("IdentityIntegrations").SendAsync(requestMessage, ct);
        response.EnsureSuccessStatusCode();
    }
    private sealed record PartnerEmailPayload(string Template, string Destination, string FullName, string ApplicationCode, string? Note,
        List<string>? Items, DateTime? DueAtUtc, string? EncryptedLink);
    private sealed record StaffActivationPayload(int ActivationId, int UserId, string Destination, string FullName, string OwnerName,
        string LotName, DateTime ExpiresAtUtc, string EncryptedToken);
    private sealed record OwnerActivationPayload(int ActivationId, int UserId, string Destination, string FullName, string BusinessName,
        DateTime ExpiresAtUtc, string EncryptedToken);
    private sealed record OtpPayload(int OtpId, int UserId, string Destination, string Purpose, DateTime ExpiresAtUtc, string EncryptedCode);
}

