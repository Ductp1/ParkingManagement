using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>PORT dữ liệu kết nối PMS của bãi (US-052, UC-51) – chỉ lưu prefix + hash của API key / webhook secret.</summary>
public interface ILotIntegrationRepository
{
    /// <summary>Tra theo ApiKeyPrefix (unique) khi PMS gọi vào bằng API key.</summary>
    Task<LotIntegration?> GetByApiKeyPrefixAsync(string apiKeyPrefix, CancellationToken cancellationToken = default);

    /// <summary>Kết nối PMS của 1 bãi (mỗi bãi tối đa 1 kết nối).</summary>
    Task<LotIntegration?> GetByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Kết quả gọi webhook PMS: thành công hoặc lỗi kèm mô tả để lưu LastError.</summary>
public sealed record PmsWebhookResult(bool Succeeded, int? StatusCode, string? Error);

/// <summary>
/// PORT gửi webhook về PMS (UC-51 bước 1). Infrastructure ký HMAC-SHA256 trên body bằng signingKey
/// và gửi kèm header chữ ký + timestamp; lỗi mạng/HTTP trả về PmsWebhookResult thay vì ném exception.
/// </summary>
public interface IPmsWebhookSender
{
    Task<PmsWebhookResult> SendAsync(string webhookUrl, string? signingKey, string eventType, string payloadJson,
        CancellationToken cancellationToken = default);
}
