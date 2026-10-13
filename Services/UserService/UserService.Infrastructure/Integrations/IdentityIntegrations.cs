using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
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
    ISecretCipher cipher, UserDbContext db, TimeProvider clock) : IOutboxTransport
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
                    cipher.Decrypt(otp.EncryptedCode, "otp-delivery"), otp.ExpiresAtUtc, ct);
                return;
            }
            url = OtpDeliveryConfiguration.Endpoint(config["Otp:DeliveryUrl"], environment);
            apiKey = config["Otp:ApiKey"];
            payload = new { message.Id, otp.Destination, otp.Purpose, otp.ExpiresAtUtc, Code = cipher.Decrypt(otp.EncryptedCode, "otp-delivery") };
        }
        else
        {
            url = OtpDeliveryConfiguration.Endpoint(config["EventBus:PublishUrl"], environment);
            apiKey = config["EventBus:ApiKey"];
            using var document = JsonDocument.Parse(message.PayloadJson);
            payload = new { EventId = message.Id, message.EventType, message.OccurredAtUtc, Payload = document.RootElement.Clone() };
        }
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        requestMessage.Headers.Add("Idempotency-Key", message.Id.ToString());
        if (!string.IsNullOrWhiteSpace(apiKey)) requestMessage.Headers.Authorization = new("Bearer", apiKey);
        using var response = await clients.CreateClient("IdentityIntegrations").SendAsync(requestMessage, ct);
        response.EnsureSuccessStatusCode();
    }
    private sealed record OtpPayload(int OtpId, int UserId, string Destination, string Purpose, DateTime ExpiresAtUtc, string EncryptedCode);
}

