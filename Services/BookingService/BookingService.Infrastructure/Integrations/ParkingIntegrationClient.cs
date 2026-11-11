using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingService.Application.Features.Bookings;
using Microsoft.Extensions.Logging;

namespace BookingService.Infrastructure.Integrations;

/// <summary>
/// US-052 (UC-51 bước 1): đẩy booking mới về PMS của bãi qua ParkingService
/// (POST api/v1/parking-lots/{lotId}/integrations/push-booking – ParkingService ký HMAC và gọi webhook của PMS).
/// BaseAddress = ParkingService ("Services:ParkingService:BaseUrl"); 404 (bãi chưa cấu hình tích hợp) → false, không coi là lỗi.
/// </summary>
public sealed class ParkingIntegrationClient(HttpClient http, ILogger<ParkingIntegrationClient> logger) : ILotIntegrationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> PushBookingCreatedAsync(PmsBookingPush booking, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(
                $"api/v1/parking-lots/{booking.ParkingLotId}/integrations/push-booking", booking, JsonOptions, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return false; // Bãi chưa cấu hình PMS – không phải lỗi tích hợp.
            if (response.IsSuccessStatusCode)
                return true;

            logger.LogWarning("ParkingService trả HTTP {StatusCode} khi đẩy booking {Code} về PMS.",
                (int)response.StatusCode, booking.BookingCode);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Không gọi được ParkingService khi đẩy booking {Code} về PMS.", booking.BookingCode);
            return false;
        }
    }
}