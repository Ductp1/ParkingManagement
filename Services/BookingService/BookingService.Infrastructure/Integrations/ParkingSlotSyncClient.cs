using System.Net.Http.Json;
using BookingService.Application.Features.Bookings;
using Microsoft.Extensions.Logging;

namespace BookingService.Infrastructure.Integrations;

/// <summary>
/// Best-effort đồng bộ trạng thái slot sang ParkingService (:5103) và broadcast SignalR SlotStateChanged qua NotificationService (:5106).
/// Lỗi mạng tạm thời chỉ log warning, không làm hỏng giao dịch booking đã commit.
/// </summary>
public sealed class ParkingSlotSyncClient(
    IHttpClientFactory httpClientFactory,
    ILogger<ParkingSlotSyncClient> logger) : IParkingSlotSyncClient
{
    public const string ParkingClientName = "ParkingSlotSync_Parking";
    public const string NotifyClientName = "ParkingSlotSync_Notify";

    public async Task SyncSlotStateAsync(
        int parkingLotId,
        int slotId,
        string newState,
        string reason,
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        if (parkingLotId <= 0 || slotId <= 0) return;

        string? previousState = null;

        try
        {
            var parkingHttp = httpClientFactory.CreateClient(ParkingClientName);
            var res = await parkingHttp.PostAsJsonAsync(
                $"api/v1/parking-lots/slots/{slotId}/state-override",
                new
                {
                    parkingLotId,
                    newState,
                    source = "Booking",
                    staffUserId = (int?)null,
                    reason,
                    bookingId,
                },
                cancellationToken);

            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<SlotStateSyncResponse>(cancellationToken: cancellationToken);
                previousState = body?.FromState;
            }
            else
            {
                logger.LogWarning("ParkingService từ chối đổi trạng thái slot {SlotId} sang {NewState}: HTTP {StatusCode}",
                    slotId, newState, (int)res.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không đồng bộ được trạng thái slot {SlotId} sang ParkingService.", slotId);
        }

        try
        {
            var notifyHttp = httpClientFactory.CreateClient(NotifyClientName);
            await notifyHttp.PostAsJsonAsync(
                "api/v1/internal/parking/broadcast-slot-state",
                new
                {
                    slotId,
                    parkingLotId,
                    newState,
                    previousState,
                    reason,
                    changedAtUtc = DateTime.UtcNow,
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không broadcast được SlotStateChanged cho slot {SlotId} qua NotificationService.", slotId);
        }
    }

    private sealed record SlotStateSyncResponse(string? FromState, string? ToState);
}
