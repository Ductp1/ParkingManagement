using System.Security.Cryptography;
using System.Text.Json;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Identity;

/// <summary>
/// Link kích hoạt một lần, dùng chung cho chủ bãi (US-116) và nhân viên (US-075): token 32 byte ngẫu nhiên,
/// OtpCodes chỉ giữ SHA-256, token (đã mã hóa) nằm trong outbox cùng transaction.
/// </summary>
public static class ActivationLinks
{
    /// <param name="payload">Nội dung outbox, nhận (activationId, token bản rõ) – bên gọi phải mã hóa token.</param>
    public static async Task IssueAsync(IIdentityStore store, User user, OtpPurpose purpose, DateTime expiresAtUtc, string eventType,
        Func<int, string, object> payload, CancellationToken ct)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var activation = new OtpCode
        {
            UserId = user.Id,
            Destination = user.Email!,
            CodeHash = OwnerAccountUseCases.HashToken(token),
            Purpose = purpose,
            ExpiresAtUtc = expiresAtUtc,
        };
        store.Add(activation);
        await store.SaveAsync(ct);
        store.Add(new OutboxMessage { EventType = eventType, PayloadJson = JsonSerializer.Serialize(payload(activation.Id, token)) });
        await store.SaveAsync(ct);
    }
}
