using System.Net;
using System.Text;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Infrastructure.Http;

namespace SupportService.Test;

public class BookingServiceClientTests
{
    private static BookingServiceClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new(new HttpClient(new StubHandler(respond)) { BaseAddress = new Uri("http://booking.test/") },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BookingServiceClient>.Instance);

    [Fact]
    public async Task Maps_booking_detail_to_snapshot()
    {
        const string json = """
            { "id": 2, "code": "BK-0002", "status": "Confirmed", "userId": 5, "parkingLotId": 1,
              "endAtUtc": "2026-10-04T05:00:00Z", "paidAmount": 63000,
              "history": [ { "atUtc": "2026-10-03T15:00:00Z" }, { "atUtc": "2026-10-03T15:30:00Z" } ] }
            """;
        var booking = await Client(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") })
            .FindByCodeAsync("BK-0002");

        Assert.NotNull(booking);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(5, booking.UserId);
        Assert.Null(booking.OwnerProfileId);   // BookingService chưa trả trường này
        Assert.Equal(new DateTime(2026, 10, 3, 15, 30, 0, DateTimeKind.Utc), booking.LastChangedAtUtc);
        Assert.Equal(63000m, booking.PaidAmount);
    }

    [Fact]
    public async Task Not_found_booking_returns_null()
        => Assert.Null(await Client(_ => new HttpResponseMessage(HttpStatusCode.NotFound)).FindByCodeAsync("BK-9999"));

    [Fact]
    public async Task Booking_service_down_becomes_service_unavailable()
        => await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            Client(_ => throw new HttpRequestException("Connection refused")).FindByCodeAsync("BK-0002"));

    [Fact]
    public async Task Booking_service_error_becomes_service_unavailable()
        => await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            Client(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)).FindByCodeAsync("BK-0002"));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
