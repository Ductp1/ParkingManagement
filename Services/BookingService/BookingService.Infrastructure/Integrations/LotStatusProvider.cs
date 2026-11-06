using System.Net.Http.Json;
using BookingService.Application.Features.Bookings;
using BookingService.Infrastructure.Persistence;

namespace BookingService.Infrastructure.Integrations;

/// <summary>US-069: check trạng thái bãi qua ParkingService trước khi tạo booking. Fail-open khi ParkingService chết.</summary>
public sealed class LotStatusProvider(HttpClient httpClient) : ILotStatusProvider
{
    public async Task<bool> IsBookableAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = await httpClient.GetFromJsonAsync<LotStatusDto>(
                $"/api/v1/parking-lots/{parkingLotId}/operations/status", cancellationToken);
            return payload?.Status == "Active";
        }
        catch
        {
            return true; // fail-open: ưu tiên availability, ghi chú trong docs US-069
        }
    }

    private sealed record LotStatusDto(int LotId, string Status, bool EmergencyStopped, string? EmergencyStopReason);
}
