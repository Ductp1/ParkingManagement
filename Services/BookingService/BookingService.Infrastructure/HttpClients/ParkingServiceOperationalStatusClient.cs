using System.Net.Http.Json;
using System.Text.Json;
using BookingService.Application.Features.Bookings;
using Microsoft.Extensions.Logging;

namespace BookingService.Infrastructure.HttpClients;

/// <summary>
/// US-069: ILotOperationalStatusProvider production — HTTP nội bộ sang ParkingService (:5103).
/// FAIL-OPEN: mọi lỗi hạ tầng (mạng/timeout/5xx/404) → null, booking không bị chặn oan
/// (spec: check lot status trước khi tạo booking phải fail-open khi ParkingService chết).
/// </summary>
public sealed class ParkingServiceOperationalStatusClient(
    HttpClient http, ILogger<ParkingServiceOperationalStatusClient> logger) : ILotOperationalStatusProvider
{
    public async Task<LotOperationalStatusSnapshot?> GetAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await http.GetFromJsonAsync<LotOperationalStatusSnapshot>(
                $"api/v1/parking-lots/{parkingLotId}/operations-status", cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex,
                "ParkingService không phản hồi khi check operations-status của lot {LotId} → fail-open cho phép đặt chỗ.",
                parkingLotId);
            return null;
        }
    }
}