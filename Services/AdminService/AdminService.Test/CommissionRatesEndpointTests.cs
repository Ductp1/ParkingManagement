using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AdminService.API.Controllers;
using AdminService.API.Security;
using AdminService.Application.Features;
using AdminService.Application.Features.Commission;
using AdminService.Application.Features.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ParkingManagement.ServiceDefaults;
using static AdminService.Test.CommissionRateResolverTests;

namespace AdminService.Test;

// US-079: hai endpoint tỷ lệ hoa hồng chạy trên TestServer với đúng pipeline của AdminService
// (AddServiceDefaults + AddJwtAuth + AddInternalEventReceiver + UseServiceDefaults); database thay bằng fake.
public sealed class CommissionRatesEndpointTests : IAsyncLifetime
{
    private const string ResolveUrl = "/internal/v1/commission-rates/resolve";
    private const string ListUrl = "/api/v1/admin/commission-rates";
    private const string PaymentKey = "payment-service-internal-key-0123456789abcdef";
    private const string SupportKey = "support-service-internal-key-0123456789abcdef";

    private readonly RSA signingKey = RSA.Create(2048);
    private readonly string publicKeyPath = Path.Combine(Path.GetTempPath(), $"admin-commission-{Guid.NewGuid():N}.pem");
    private WebApplication? app;
    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        File.WriteAllText(publicKeyPath, signingKey.ExportSubjectPublicKeyInfoPem());
        return StartAsync(new()
        {
            ["InternalEvents:ApiKeys:PaymentService"] = PaymentKey,
            ["InternalEvents:ApiKeys:SupportService"] = SupportKey,
        });
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        if (app is not null) await app.DisposeAsync();
        signingKey.Dispose();
        try { File.Delete(publicKeyPath); } catch (IOException) { }
    }

    private async Task StartAsync(Dictionary<string, string?> configuration, IParkingDashboardClient? parking = null)
    {
        if (app is not null)
        {
            client.Dispose();
            await app.DisposeAsync();
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ApplicationName = typeof(InternalCommissionRatesController).Assembly.GetName().Name,
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Logging.ClearProviders();

        builder.AddServiceDefaults("AdminService");
        builder.Services.AddJwtAuth(publicKeyPath);
        builder.Services.AddInternalEventReceiver();
        builder.Services.AddSingleton<ICommissionRateQueries>(new FakeCommissionRateQueries(
            Contract(lotId: 7, percent: 7.5m, from: new DateOnly(2026, 10, 1), to: new DateOnly(2027, 9, 30))));
        builder.Services.AddSingleton<ISettingsQueries>(new FakeCommissionSettings("0.10"));
        builder.Services.AddSingleton(parking ?? FakeParkingLots.Returning(
            new ParkingLotSource(1, "Vincom Bà Triệu", "Hà Nội", "Hai Bà Trưng", "Active", "Standard", 100, false, false)));
        builder.Services.AddScoped<IResolveCommissionRatesUseCase, ResolveCommissionRatesUseCase>();
        builder.Services.AddScoped<IListCommissionRatesUseCase, ListCommissionRatesUseCase>();

        app = builder.Build();
        app.UseServiceDefaults();
        await app.StartAsync();
        client = app.GetTestClient();
    }

    private static HttpRequestMessage ResolveRequest(string? apiKey, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ResolveUrl) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (apiKey is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    private static readonly string ValidBody = """{ "items": [ { "parkingLotId": 7, "atUtc": "2026-11-05T03:00:00Z" }, { "parkingLotId": 1, "atUtc": "2026-11-05T03:00:00Z" } ] }""";

    /// <summary>JWT RS256 đúng hợp đồng UserService (issuer / audience của AuthExtensions, claim "role").</summary>
    private string Token(string role)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", "1"), new Claim("role", role)]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(30),
            Issuer = "ParkingManagement",
            Audience = "ParkingManagement.Clients",
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(signingKey), SecurityAlgorithms.RsaSha256),
        });
    }

    private Task<HttpResponseMessage> GetList(string? role, string query = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ListUrl + query);
        if (role is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(role));
        return client.SendAsync(request);
    }

    // ==================== POST /internal/v1/commission-rates/resolve ====================

    [Fact]
    public async Task Resolve_WithPaymentServiceKey_Returns200WithFractionRates()
    {
        var response = await client.SendAsync(ResolveRequest(PaymentKey, ValidBody));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        Assert.Equal(0.075m, items[0].GetProperty("rate").GetDecimal());
        Assert.Equal("LotContract", items[0].GetProperty("source").GetString());
        Assert.Equal("HD-SP-2026-0001", items[0].GetProperty("contractNumber").GetString());
        Assert.Equal("2026-10-01", items[0].GetProperty("effectiveFrom").GetString());
        Assert.Equal(0.10m, items[1].GetProperty("rate").GetDecimal());
        Assert.Equal("DefaultConfig", items[1].GetProperty("source").GetString());
        Assert.Equal("NoSignedContract", items[1].GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key-wrong-key-wrong-key-wrong-key-0000")]
    public async Task Resolve_MissingOrWrongKey_Returns401(string? apiKey)
    {
        var response = await client.SendAsync(ResolveRequest(apiKey, ValidBody));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_UserJwtInsteadOfInternalKey_Returns401()
    {
        var response = await client.SendAsync(ResolveRequest(Token("Admin"), ValidBody));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_KeyOfAnotherService_Returns403()
    {
        var response = await client.SendAsync(ResolveRequest(SupportKey, ValidBody));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "items": [] }""")]
    [InlineData("""{ "items": [ { "parkingLotId": 0, "atUtc": "2026-11-05T03:00:00Z" } ] }""")]
    [InlineData("""{ "items": [ { "parkingLotId": 1, "atUtc": "2099-01-01T00:00:00Z" } ] }""")]
    public async Task Resolve_InvalidBody_Returns400(string body)
    {
        var response = await client.SendAsync(ResolveRequest(PaymentKey, body));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_NoKeysConfigured_Returns503()
    {
        await StartAsync([]);
        var response = await client.SendAsync(ResolveRequest(PaymentKey, ValidBody));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // ==================== GET /api/v1/admin/commission-rates ====================

    [Fact]
    public async Task List_WithoutToken_Returns401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await GetList(role: null)).StatusCode);

    [Theory]
    [InlineData("LotOwner")]
    [InlineData("Staff")]
    [InlineData("Driver")]
    public async Task List_NonAdmin_Returns403(string role)
        => Assert.Equal(HttpStatusCode.Forbidden, (await GetList(role)).StatusCode);

    [Fact]
    public async Task List_Admin_Returns200WithLotsAndDefault()
    {
        var response = await GetList("Admin", "?page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0.10m, body.GetProperty("defaultRate").GetDecimal());
        Assert.False(body.GetProperty("partial").GetBoolean());
        var lots = body.GetProperty("lots");
        Assert.Equal(2, lots.GetProperty("totalCount").GetInt32());
        var row = Assert.Single(lots.GetProperty("items").EnumerateArray());
        Assert.Equal("Bãi Xanh Ba Đình", row.GetProperty("lotName").GetString());      // sắp theo tên: "Bãi…" trước "Vincom…"
        Assert.Equal(0.075m, row.GetProperty("currentRate").GetDecimal());
    }

    [Fact]
    public async Task List_InvalidPageSize_Returns400()
        => Assert.Equal(HttpStatusCode.BadRequest, (await GetList("Admin", "?pageSize=500")).StatusCode);

    [Fact]
    public async Task List_ParkingServiceDown_Returns200Partial()
    {
        await StartAsync(new() { ["InternalEvents:ApiKeys:PaymentService"] = PaymentKey }, FakeParkingLots.Failing());

        var response = await GetList("Admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("partial").GetBoolean());
        Assert.Equal(7, Assert.Single(body.GetProperty("lots").GetProperty("items").EnumerateArray()).GetProperty("parkingLotId").GetInt32());
    }
}
