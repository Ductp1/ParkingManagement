using System.Net.Http.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.LotOperations;

namespace ParkingService.Infrastructure.HttpClients;

/// <summary>
/// US-067/069: ILotBookingLookup production — HTTP nội bộ sang BookingService (:5104, không qua Gateway).
/// Lỗi HTTP bị ném ra cho use case tự degrade (dashboard về 0 / emergency stop vẫn chạy).
/// </summary>
public sealed class BookingServiceLotBookingLookup(HttpClient http) : ILotBookingLookup
{
    public async Task<IReadOnlyList<EmergencyAffectedBooking>> GetAffectedBookingsAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var affected = await http.GetFromJsonAsync<List<EmergencyAffectedBooking>>(
            $"api/v1/internal/lot-bookings/{lotId}/affected", cancellationToken);
        return affected ?? [];
    }

    public async Task<(int Active, int Upcoming)> GetBookingCountsAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var dto = await http.GetFromJsonAsync<LotBookingCountsDto>(
            $"api/v1/internal/lot-bookings/{lotId}/counts", cancellationToken);
        return dto is null ? (0, 0) : (dto.Active, dto.Upcoming);
    }

    /// <summary>Khớp payload JSON camelCase từ BookingService internal endpoint.</summary>
    private sealed record LotBookingCountsDto(int Active, int Upcoming);
}