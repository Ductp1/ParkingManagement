using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using BookingService.Application.Features.MonthlyPasses;

namespace BookingService.Infrastructure.Integrations;

/// <summary>
/// Đọc hạn mức thuê bao (Q) từ cấu hình US-066 của GateService
/// (GET /api/v1/internal/lot-capacity/{lotId}). Bãi chưa cấu hình (configured = false) → null = không giới hạn.
/// Lỗi HTTP → null để đăng ký không bị chặn oan khi GateService tắt (quota chỉ là hạn mức bán).
/// Base URL: appsettings → "GateService:BaseUrl" (mặc định http://localhost:5107).
/// </summary>
public sealed class GateCapacityQuotaProvider(HttpClient http, ILogger<GateCapacityQuotaProvider> logger)
    : ILotSubscriberQuotaProvider
{
    private sealed record GateCapacitySnapshot(bool Configured, int SubscriberQuota);

    public async Task<int?> GetSubscriberQuotaAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync($"/api/v1/internal/lot-capacity/{parkingLotId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("US-066: đọc Q lot {LotId} → HTTP {Status} (coi như không giới hạn).",
                    parkingLotId, response.StatusCode);
                return null;
            }

            var snapshot = await response.Content.ReadFromJsonAsync<GateCapacitySnapshot>(cancellationToken: cancellationToken);
            return snapshot is { Configured: true } ? snapshot.SubscriberQuota : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "US-066: đọc Q lot {LotId} lỗi — coi như không giới hạn.", parkingLotId);
            return null;
        }
    }
}