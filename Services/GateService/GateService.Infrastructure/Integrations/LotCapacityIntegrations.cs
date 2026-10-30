using System.Net.Http.Json;
using GateService.Application.Features.LotCapacity;
using Microsoft.Extensions.Logging;

namespace GateService.Infrastructure.Integrations;

/// <summary>
/// Publish CapacityChanged (US-065) tới NotificationService qua HTTP internal
/// (repo chưa có message broker / inbox consumer — xem báo cáo). Publisher TỰ NUỐT mọi lỗi:
/// nghiệp vụ check-in/ra và lưu cấu hình không bao giờ fail vì publish.
/// Base URL: appsettings → "NotificationService:BaseUrl" (mặc định http://localhost:5106).
/// </summary>
public sealed class NotificationCapacityPublisher(
    HttpClient http,
    ILotCapacityPolicyStore store,
    ILogger<NotificationCapacityPublisher> logger) : ILotCapacityRealtimePublisher
{
    public async Task PublishCapacityChangedAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        try
        {
            var policy = await store.GetAsync(parkingLotId, cancellationToken);
            if (policy is null)
                return; // chưa cấu hình US-066 → không có gì để phát.

            var counters = await store.GetCountersAsync(parkingLotId, cancellationToken) ?? new AdmissionCounters(0, 0, 0);
            var free = Math.Max(0, policy.Capacity - counters.Total);
            var payload = new
            {
                lotId = parkingLotId,
                capacity = policy.Capacity,
                transientBuffer = policy.TransientBuffer,
                subscriberQuota = policy.SubscriberQuota,
                availableForTransient = Math.Max(0, Math.Min(free, Math.Max(0, policy.TransientBuffer - counters.Transient))),
                availableForSubscriber = free,
                version = policy.Version,
                timestampUtc = DateTime.UtcNow,
            };

            using var response = await http.PostAsJsonAsync("/api/v1/internal/parking/broadcast-capacity", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("US-066: broadcast CapacityChanged lot {LotId} → HTTP {Status}", parkingLotId, response.StatusCode);
        }
        catch (OperationCanceledException)
        {
            // Caller hủy → nuốt luôn để không làm hỏng nghiệp vụ chính.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "US-066: broadcast CapacityChanged lot {LotId} thất bại — bỏ qua.", parkingLotId);
        }
    }
}

/// <summary>
/// Đếm thuê bao đang hiệu lực (A) qua BookingService internal API. Lỗi → 0
/// (GET cấu hình không được fail vì service phụ) — quota khi ĐĂNG KÝ được enforce
/// phía BookingService với số liệu local, không phụ thuộc endpoint này.
/// Base URL: appsettings → "BookingService:BaseUrl" (mặc định http://localhost:5104).
/// </summary>
public sealed class BookingSubscriberCounter(HttpClient http, ILogger<BookingSubscriberCounter> logger) : IActiveSubscriberCounter
{
    public async Task<int> CountActiveAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync(
                $"/api/v1/internal/monthly-passes/active-count?lotId={parkingLotId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("US-066: đếm thuê bao lot {LotId} → HTTP {Status} (coi như 0).",
                    parkingLotId, response.StatusCode);
                return 0;
            }

            return await response.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "US-066: đếm thuê bao lot {LotId} lỗi — coi như 0.", parkingLotId);
            return 0;
        }
    }
}