using System.Net.Http.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.LotOperations;
using ParkingService.Infrastructure.Persistence;

namespace ParkingService.Infrastructure.Integrations;

/// <summary>
/// US-067/069: tra cứu booking của bãi qua BookingService (internal endpoint /api/v1/bookings/internal/lots/{id}/overview).
/// Lỗi HTTP → counts = 0 + affected rỗng (degrade: dashboard thiếu số liệu booking nhưng vẫn hiển thị occupancy).
/// </summary>
public sealed class BookingLotLookupClient(HttpClient httpClient) : ILotBookingLookup
{
    public sealed record AffectedBookingDto(int BookingId, string Code, int UserId, string Status, DateTime StartAtUtc, DateTime EndAtUtc);
    public sealed record LotBookingOverviewDto(int ActiveBookings, int UpcomingBookings, List<AffectedBookingDto> AffectedEmergency);

    public async Task<IReadOnlyList<EmergencyAffectedBooking>> GetAffectedBookingsAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var payload = await GetAsync(lotId, cancellationToken);
        return payload?.AffectedEmergency.Select(b => new EmergencyAffectedBooking(
            b.BookingId, b.Code, b.UserId, b.Status, b.StartAtUtc, b.EndAtUtc)).ToList()
            ?? [];
    }

    public async Task<(int Active, int Upcoming)> GetBookingCountsAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var payload = await GetAsync(lotId, cancellationToken);
        return payload is null ? (0, 0) : (payload.ActiveBookings, payload.UpcomingBookings);
    }

    private async Task<LotBookingOverviewDto?> GetAsync(int lotId, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<LotBookingOverviewDto>(
                $"/api/v1/bookings/internal/lots/{lotId}/overview", cancellationToken);
        }
        catch (Exception)
        {
            return null; // BookingService down → degrade, không chặn dashboard/emergency stop
        }
    }
}

/// <summary>US-068/069: publisher outbox — event ghi OutboxMessages của ParkingDbContext (cùng transaction).</summary>
public sealed class ParkingEventOutbox(ParkingDbContext db) : ILotEventPublisher
{
    public void Enqueue(IntegrationEvent integrationEvent)
        => db.OutboxMessages.Add(new ParkingManagement.SharedKernel.Domain.OutboxMessage
        {
            EventType = integrationEvent.GetType().Name,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            OccurredAtUtc = DateTime.UtcNow,
        });
}
