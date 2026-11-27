using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Settlements;
using PaymentService.Infrastructure;
using PaymentService.Infrastructure.Commission;
using PaymentService.Infrastructure.Settlements;

namespace PaymentService.Test;

// US-080: client đọc nguồn ngoài cho quyết toán (AdminService chu kỳ, BookingService trạng thái, SupportService khoản giữ) + cấu hình.
public class SettlementSourceClientTests
{
    private const string Key = "payment-service-internal-key-0123456789abcdef";
    private static readonly AdminServiceConnection Admin = new(new Uri("http://admin.test:5108/"), Key, TimeSpan.FromMilliseconds(300));
    private static readonly SettlementGatewayConnection Gateway = new(new Uri("http://gateway.test:5000/"), TimeSpan.FromMilliseconds(300));

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Calls) Calls.Add(request);
            return respond(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static Handler Returning(HttpStatusCode status, string json) => new((_, _) => Task.FromResult(Json(status, json)));

    private static Handler Hanging() => new(async (_, ct) =>
    {
        await Task.Delay(Timeout.Infinite, ct);
        return new HttpResponseMessage(HttpStatusCode.OK);
    });

    // ===================== AdminService – SETTLEMENT_CYCLE =====================

    [Fact]
    public async Task Cycle_GetsWithInternalKeyAndUtcInstant()
    {
        var handler = Returning(HttpStatusCode.OK,
            """{ "key": "SETTLEMENT_CYCLE", "value": "MONTHLY", "atUtc": "2026-11-01T17:00:00Z", "effectiveFromUtc": "2026-10-25T17:00:00Z", "isDefault": false }""");

        var quote = await new AdminSettlementCycleClient(new HttpClient(handler), Admin)
            .ResolveAsync(new DateTime(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        var request = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://admin.test:5108/internal/v1/system-configs/SETTLEMENT_CYCLE/resolve?atUtc=2026-11-01T17%3A00%3A00Z",
            request.RequestUri!.AbsoluteUri);
        Assert.Equal(("Bearer", Key), (request.Headers.Authorization!.Scheme, request.Headers.Authorization.Parameter));
        Assert.Equal("MONTHLY", quote.Value);
        Assert.Equal(new DateTime(2026, 10, 25, 17, 0, 0, DateTimeKind.Utc), quote.EffectiveFromUtc);
        Assert.Equal(DateTimeKind.Utc, quote.EffectiveFromUtc!.Value.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{}")]
    [InlineData(HttpStatusCode.Forbidden, "{}")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.OK, """{ "value": "" }""")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task Cycle_ErrorOrEmpty_ThrowsUnavailable(HttpStatusCode status, string body)
        => await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            new AdminSettlementCycleClient(new HttpClient(Returning(status, body)), Admin).ResolveAsync(DateTime.UtcNow, CancellationToken.None));

    [Fact]
    public async Task Cycle_NotConfigured_ThrowsWithoutCalling()
    {
        var handler = Returning(HttpStatusCode.OK, "{}");
        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            new AdminSettlementCycleClient(new HttpClient(handler), new AdminServiceConnection(null, null, TimeSpan.FromSeconds(1)))
                .ResolveAsync(DateTime.UtcNow, CancellationToken.None));
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Cycle_Timeout_ThrowsUnavailable()
        => await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            new AdminSettlementCycleClient(new HttpClient(Hanging()), Admin).ResolveAsync(DateTime.UtcNow, CancellationToken.None));

    // ===================== BookingService – trạng thái =====================

    [Fact]
    public async Task Booking_ReadsStatusAndTimeOfEnteringIt_IgnoresOtherFields()
    {
        var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/v1/bookings/BK-0001" => Json(HttpStatusCode.OK, """
                { "id": 1, "code": "BK-0001", "status": "Completed", "userId": 5, "plateNumber": "51A-12345",
                  "history": [ { "fromStatus": null, "toStatus": "Confirmed", "atUtc": "2026-11-03T01:00:00Z" },
                               { "fromStatus": "CheckedOut", "toStatus": "Completed", "atUtc": "2026-11-05T04:00:00Z" } ] }
                """),
            "/api/v1/bookings/BK-0002" => Json(HttpStatusCode.OK, """{ "status": "Parking", "history": [] }"""),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        }));

        var statuses = await new BookingStatusClient(new HttpClient(handler), Gateway)
            .GetStatusesAsync(["BK-0001", "BK-0002", "BK-9999", "bk-0001"], CancellationToken.None);

        Assert.Equal(3, handler.Calls.Count);                                   // mã trùng (khác hoa thường) chỉ hỏi một lần
        Assert.Equal(new BookingStatusInfo(true, "Completed", new DateTime(2026, 11, 5, 4, 0, 0, DateTimeKind.Utc)), statuses["BK-0001"]);
        Assert.Equal(new BookingStatusInfo(true, "Parking", null), statuses["bk-0002"]);
        Assert.Equal(BookingStatusInfo.NotFound, statuses["BK-9999"]);
        Assert.All(handler.Calls, r => Assert.Null(r.Headers.Authorization));   // endpoint có sẵn, không cần token
    }

    [Fact]
    public async Task Booking_EscapesCode()
    {
        var handler = Returning(HttpStatusCode.NotFound, "{}");
        await new BookingStatusClient(new HttpClient(handler), Gateway).GetStatusesAsync(["BK 1/2"], CancellationToken.None);
        Assert.Equal("http://gateway.test:5000/api/v1/bookings/BK%201%2F2", Assert.Single(handler.Calls).RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.BadGateway, "{}")]
    [InlineData(HttpStatusCode.OK, """{ "status": null }""")]
    public async Task Booking_ServiceError_ThrowsUnavailable(HttpStatusCode status, string body)
        => await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            new BookingStatusClient(new HttpClient(Returning(status, body)), Gateway).GetStatusesAsync(["BK-1"], CancellationToken.None));

    [Fact]
    public async Task Booking_Timeout_ThrowsUnavailable()
        => await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            new BookingStatusClient(new HttpClient(Hanging()), Gateway).GetStatusesAsync(["BK-1"], CancellationToken.None));

    // ===================== SupportService – khoản giữ =====================

    private static IHttpContextAccessor WithAuthorization(string? value)
    {
        var context = new DefaultHttpContext();
        if (value is not null) context.Request.Headers.Authorization = value;
        return new HttpContextAccessor { HttpContext = context };
    }

    [Fact]
    public async Task Holds_ForwardsAdminJwtAndParses()
    {
        var handler = Returning(HttpStatusCode.OK, """
            [ { "complaintCode": "CP-0001", "status": "AwaitingOwner", "parkingLotId": 11, "ownerProfileId": 1, "bookingCode": "BK-1",
                "amount": 30000, "heldSinceUtc": "2026-11-05T00:00:00Z" },
              { "complaintCode": "", "status": "Open", "parkingLotId": 11, "ownerProfileId": 1, "amount": 1, "heldSinceUtc": "2026-11-05T00:00:00Z" } ]
            """);

        var holds = await new PayoutHoldClient(new HttpClient(handler), Gateway, WithAuthorization("Bearer admin.jwt.token"))
            .ListOpenHoldsAsync(1, CancellationToken.None);

        var request = Assert.Single(handler.Calls);
        Assert.Equal("http://gateway.test:5000/api/v1/complaints/payout-holds?ownerProfileId=1", request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer admin.jwt.token", request.Headers.GetValues("Authorization").Single());
        var hold = Assert.Single(holds);
        Assert.Equal(new PayoutHoldItem("CP-0001", "AwaitingOwner", 11, 1, "BK-1", 30_000, new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc)), hold);
    }

    [Fact]
    public async Task Holds_AllOwners_HasNoFilter()
    {
        var handler = Returning(HttpStatusCode.OK, "[]");
        await new PayoutHoldClient(new HttpClient(handler), Gateway, WithAuthorization("Bearer x")).ListOpenHoldsAsync(null, CancellationToken.None);
        Assert.Equal("http://gateway.test:5000/api/v1/complaints/payout-holds", Assert.Single(handler.Calls).RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Basic abc")]
    public async Task Holds_WithoutAdminJwt_ThrowsWithoutCalling(string? authorization)
    {
        var handler = Returning(HttpStatusCode.OK, "[]");
        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            new PayoutHoldClient(new HttpClient(handler), Gateway, WithAuthorization(authorization)).ListOpenHoldsAsync(null, CancellationToken.None));
        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Holds_HttpError_ThrowsUnavailable(HttpStatusCode status)
        => await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            new PayoutHoldClient(new HttpClient(Returning(status, "[]")), Gateway, WithAuthorization("Bearer x"))
                .ListOpenHoldsAsync(null, CancellationToken.None));

    // ===================== Cấu hình =====================

    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Options_DefaultsAndOverrides()
    {
        var defaults = SettlementServiceCollectionExtensions.ReadOptions(Config([]).GetSection("Settlements"));
        Assert.Equal(new DateOnly(2026, 1, 5), defaults.BiweeklyAnchor);
        Assert.Equal(60, defaults.MaxCatchUpPeriods);

        var custom = SettlementServiceCollectionExtensions.ReadOptions(Config(new()
        {
            ["Settlements:BiweeklyAnchor"] = "2026-11-02", ["Settlements:MaxCatchUpPeriods"] = "5",
        }).GetSection("Settlements"));
        Assert.Equal(new DateOnly(2026, 11, 2), custom.BiweeklyAnchor);
        Assert.Equal(5, custom.MaxCatchUpPeriods);
    }

    [Theory]
    [InlineData("2026-11-03")]      // Thứ Ba
    [InlineData("03/11/2026")]
    public void Options_InvalidAnchor_FailsAtStartup(string anchor)
        => Assert.Throws<InvalidOperationException>(() => SettlementServiceCollectionExtensions.ReadOptions(
            Config(new() { ["Settlements:BiweeklyAnchor"] = anchor }).GetSection("Settlements")));

    [Fact]
    public void Gateway_DefaultAndCustom()
    {
        var defaults = SettlementServiceCollectionExtensions.ReadGateway(Config([]), Config([]).GetSection("Settlements"));
        Assert.Equal(new Uri("http://localhost:5000/"), defaults.BaseUrl);
        Assert.Equal(TimeSpan.FromSeconds(10), defaults.Timeout);

        var config = Config(new() { ["Services:GatewayBaseUrl"] = "https://gw.example.vn", ["Settlements:SourceTimeoutSeconds"] = "3" });
        var custom = SettlementServiceCollectionExtensions.ReadGateway(config, config.GetSection("Settlements"));
        Assert.Equal(new Uri("https://gw.example.vn/"), custom.BaseUrl);
        Assert.Equal(TimeSpan.FromSeconds(3), custom.Timeout);
    }

    [Fact]
    public void Gateway_InvalidUrl_FailsAtStartup()
    {
        var config = Config(new() { ["Services:GatewayBaseUrl"] = "ftp://gw" });
        Assert.Throws<InvalidOperationException>(() => SettlementServiceCollectionExtensions.ReadGateway(config, config.GetSection("Settlements")));
    }
}
