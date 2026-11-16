using System.Net.Http.Json;
using System.Text.Json;
using GateService.Application.Features.LotCapacity;
using Microsoft.Extensions.Logging;

namespace GateService.Infrastructure.HttpClients;

/// <summary>
/// US-069: ILotOperationalStatusProvider production — pull trạng thái vận hành bãi từ ParkingService
/// (:5103, endpoint nội bộ /operations-status). FAIL-OPEN: mọi lỗi hạ tầng (mạng/timeout/5xx/404) → null
/// → admission tiếp tục theo luật US-066, không chặn oan xe vào khi ParkingService chết.
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
                "ParkingService không phản hồi khi check operations-status của lot {LotId} → fail-open tiếp tục admission.",
                parkingLotId);
            return null;
        }
    }
}