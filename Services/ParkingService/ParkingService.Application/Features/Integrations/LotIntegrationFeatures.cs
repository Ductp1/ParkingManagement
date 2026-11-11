using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.Integrations;

/// <summary>
/// Quy ước API key PMS (US-052): "{prefix}.{secret}" – prefix (≤ 12 ký tự) để tra nhanh, DB chỉ lưu
/// "SHA256:{hex}" của toàn bộ key. So sánh hash bằng thời gian hằng định để tránh timing attack.
/// </summary>
public static class PmsApiKey
{
    public const string HashPrefix = "SHA256:";

    public static bool TrySplitPrefix(string apiKey, out string prefix)
    {
        prefix = string.Empty;
        var dot = apiKey.IndexOf('.');
        if (dot <= 0 || dot > 12 || dot == apiKey.Length - 1) return false;
        prefix = apiKey[..dot];
        return true;
    }

    public static string Hash(string apiKey) => HashPrefix + HexDigest(apiKey);

    public static bool Matches(string apiKey, string storedHash)
    {
        var stored = storedHash.Trim();
        if (!stored.StartsWith(HashPrefix, StringComparison.OrdinalIgnoreCase)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(HexDigest(apiKey)),
            Encoding.ASCII.GetBytes(stored[HashPrefix.Length..].ToLowerInvariant()));
    }

    private static string HexDigest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

// ===== XÁC THỰC API KEY CỦA PMS (GateService gọi khi nhận event xe vào/ra) =====
public sealed record VerifyPmsApiKeyCommand(string ApiKey);

public sealed record PmsIdentityDto(int ParkingLotId, int IntegrationId, string ProviderName);

public interface IVerifyPmsApiKeyUseCase
{
    /// <summary>Key hợp lệ và kết nối đang Active → định danh bãi; ngược lại AuthenticationException.</summary>
    Task<PmsIdentityDto> ExecuteAsync(VerifyPmsApiKeyCommand command, CancellationToken cancellationToken = default);
}

/// <summary>US-052 (Authentication): xác thực API key PMS theo prefix + hash, cập nhật LastSyncAtUtc.</summary>
public sealed class VerifyPmsApiKeyUseCase(ILotIntegrationRepository repository, TimeProvider timeProvider) : IVerifyPmsApiKeyUseCase
{
    private const string Invalid = "API key PMS không hợp lệ hoặc kết nối đã bị vô hiệu.";

    public async Task<PmsIdentityDto> ExecuteAsync(VerifyPmsApiKeyCommand command, CancellationToken cancellationToken = default)
    {
        var apiKey = command.ApiKey?.Trim() ?? string.Empty;
        if (!PmsApiKey.TrySplitPrefix(apiKey, out var prefix))
            throw new AuthenticationException(Invalid);

        var integration = await repository.GetByApiKeyPrefixAsync(prefix, cancellationToken);
        if (integration is null || integration.Status != IntegrationStatus.Active || !PmsApiKey.Matches(apiKey, integration.ApiKeyHash))
            throw new AuthenticationException(Invalid);

        integration.LastSyncAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await repository.SaveChangesAsync(cancellationToken);
        return new PmsIdentityDto(integration.ParkingLotId, integration.Id, integration.ProviderName);
    }
}

// ===== ĐẨY BOOKING MỚI VỀ PMS (BookingService gọi sau khi tạo booking – UC-51 bước 1) =====
public sealed record PushBookingToPmsCommand(int ParkingLotId, string BookingCode, string PlateNumber, string VehicleType,
    DateTime StartAtUtc, DateTime EndAtUtc, decimal TotalAmount);

public sealed record PushBookingToPmsResultDto(int ParkingLotId, string BookingCode, bool Delivered, int? StatusCode, DateTime AtUtc);

public interface IPushBookingToPmsUseCase
{
    Task<PushBookingToPmsResultDto> ExecuteAsync(PushBookingToPmsCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-052 (AC1): đẩy booking mới về webhook PMS của bãi, ký HMAC bằng WebhookSecretHash (PMS tự băm secret của mình
/// để kiểm chữ ký). Bãi chưa cấu hình/không Active → NotFoundException (BookingService hiểu là "không có PMS").
/// Gửi thất bại → lưu LastError + DependencyUnavailableException (503) để bên gọi ghi log/retry; không đổi Status.
/// </summary>
public sealed class PushBookingToPmsUseCase(
    ILotIntegrationRepository repository,
    IPmsWebhookSender webhookSender,
    TimeProvider timeProvider,
    ILogger<PushBookingToPmsUseCase> logger) : IPushBookingToPmsUseCase
{
    public const string EventType = "booking.created";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PushBookingToPmsResultDto> ExecuteAsync(PushBookingToPmsCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(command.BookingCode)) throw new ValidationException("bookingCode là bắt buộc.");

        var integration = await repository.GetByLotIdAsync(command.ParkingLotId, cancellationToken);
        if (integration is null || integration.Status != IntegrationStatus.Active || string.IsNullOrWhiteSpace(integration.WebhookUrl))
            throw new NotFoundException("Kết nối PMS (webhook) của bãi", command.ParkingLotId);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var payload = JsonSerializer.Serialize(new
        {
            @event = EventType,
            occurredAtUtc = now,
            parkingLotId = command.ParkingLotId,
            booking = new
            {
                code = command.BookingCode, plateNumber = command.PlateNumber, vehicleType = command.VehicleType,
                startAtUtc = command.StartAtUtc, endAtUtc = command.EndAtUtc, totalAmount = command.TotalAmount
            }
        }, Json);

        var result = await webhookSender.SendAsync(integration.WebhookUrl, integration.WebhookSecretHash, EventType, payload, cancellationToken);
        if (!result.Succeeded)
        {
            integration.LastError = Truncate($"{now:O} push {command.BookingCode}: {result.Error ?? $"HTTP {result.StatusCode}"}", 1000);
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Đẩy booking {Code} về PMS bãi {LotId} thất bại: {Error}", command.BookingCode, command.ParkingLotId, integration.LastError);
            throw new DependencyUnavailableException($"PMS của bãi {command.ParkingLotId} chưa nhận được booking {command.BookingCode}.");
        }

        integration.LastSyncAtUtc = now;
        integration.LastError = null;
        await repository.SaveChangesAsync(cancellationToken);
        return new PushBookingToPmsResultDto(command.ParkingLotId, command.BookingCode, true, result.StatusCode, now);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
