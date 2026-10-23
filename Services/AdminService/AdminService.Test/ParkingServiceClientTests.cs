using System.Net;
using System.Text;
using System.Text.Json;
using AdminService.Application.Features.LotContracts;
using AdminService.Infrastructure;
using AdminService.Infrastructure.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-116: adapter HttpClient gọi ParkingService (KYB admin) qua Gateway – fake HttpMessageHandler, không cần ParkingService chạy.
public class ParkingServiceClientTests
{
    private const string KybJson = """
        {
          "id": 31, "status": "Approved", "businessLicenseUrl": null, "sitePhotoUrls": [], "gpsWarning": false,
          "parkingLot": { "id": 7, "name": "Bãi xe Tân Bình 1", "address": "125 Cộng Hòa", "city": "TP. Hồ Chí Minh", "district": "Tân Bình",
            "latitude": 10.8, "longitude": 106.6, "totalSlots": 240, "maxHeightCm": 210, "openTime": "05:00:00", "closeTime": "23:00:00",
            "status": "PendingApproval", "ownerProfileId": 9, "activeSlotCount": 200 }
        }
        """;

    [Fact]
    public async Task Get_kyb_reads_lot_snapshot()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, KybJson));

        var kyb = await Client(handler).GetKybAsync(31, CancellationToken.None);

        Assert.Equal("http://gateway.test/api/v1/parking-lots/kyb-applications/31", handler.Requests.Single().RequestUri!.ToString());
        Assert.Equal(new KybLotInfo(31, "Approved", 7, "Bãi xe Tân Bình 1", "125 Cộng Hòa", "TP. Hồ Chí Minh", "Tân Bình", 240,
            new TimeOnly(5, 0), new TimeOnly(23, 0), "PendingApproval", 9), kyb);
    }

    [Fact]
    public async Task Get_kyb_not_found_with_problem_body_is_null()
        => Assert.Null(await Client(new FakeHandler(_ => Json(HttpStatusCode.NotFound, """{ "status": 404 }""")))
            .GetKybAsync(31, CancellationToken.None));

    [Theory]
    [InlineData(HttpStatusCode.NotFound, false)]   // Gateway chưa có route
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, false)]
    public async Task Get_kyb_other_failures_are_unavailable(HttpStatusCode status, bool json)
        => await Assert.ThrowsAsync<DependencyUnavailableException>(() => Client(new FakeHandler(_ =>
            json ? Json(status, """{ "status": 500 }""") : new HttpResponseMessage(status))).GetKybAsync(31, CancellationToken.None));

    [Fact]
    public async Task Get_kyb_rejected_token_is_unauthenticated()
        => await Assert.ThrowsAsync<AuthenticationException>(() => Client(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
            .GetKybAsync(31, CancellationToken.None));

    [Fact]
    public async Task Approve_kyb_posts_contract_number_and_note()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, KybJson));

        await Client(handler).ApproveKybAsync(31, "HD-SP-2026-0007", "Đạt", CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://gateway.test/api/v1/parking-lots/kyb-applications/31/approve", request.RequestUri!.ToString());
        var body = JsonDocument.Parse(handler.Bodies.Single()!).RootElement;
        Assert.Equal(("HD-SP-2026-0007", "Đạt"), (body.GetProperty("contractNumber").GetString(), body.GetProperty("note").GetString()));
    }

    [Fact]
    public async Task Approve_kyb_conflict_carries_parking_service_message()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.Conflict,
            """{ "status": 409, "detail": "Bãi trên 50 chỗ phải được khảo sát thực địa trước khi duyệt." }"""));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Client(handler).ApproveKybAsync(31, "HD-SP-2026-0007", null, CancellationToken.None));
        Assert.Equal("Bãi trên 50 chỗ phải được khảo sát thực địa trước khi duyệt.", ex.Message);
    }

    [Fact]
    public async Task Approve_kyb_unknown_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Client(new FakeHandler(_ => Json(HttpStatusCode.NotFound, """{ "status": 404 }""")))
            .ApproveKybAsync(31, "HD-SP-2026-0007", null, CancellationToken.None));

    [Fact]
    public async Task Approve_kyb_connection_failure_is_unavailable()
        => await Assert.ThrowsAsync<DependencyUnavailableException>(() => Client(new FakeHandler(_ => throw new HttpRequestException("refused")))
            .ApproveKybAsync(31, "HD-SP-2026-0007", null, CancellationToken.None));

    [Fact]
    public void Infrastructure_registers_parking_service_client()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAdminInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ServiceDb"] = "Host=localhost;Database=pm_admin_unit_test",
            ["Services:GatewayBaseUrl"] = "http://localhost:5000",
        }).Build());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<ParkingServiceClient>(scope.ServiceProvider.GetRequiredService<IParkingServiceClient>());
    }

    private static ParkingServiceClient Client(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://gateway.test/") }, NullLogger<ParkingServiceClient>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body)
        => new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return respond(request);
        }
    }
}
