using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.API.Controllers;
using PaymentService.Application.Features.Settlements;
using PaymentService.Infrastructure.Settlements;

namespace PaymentService.Test;

// US-080: endpoint Admin sinh / tính lại kỳ qua HTTP thật (Kestrel 127.0.0.1, cổng ngẫu nhiên) với pipeline thật (JWT RS256 + RequireAdmin + xử lý lỗi chuẩn); use case giả.
public sealed class AdminSettlementsEndpointTests : IAsyncLifetime
{
    private sealed class StubGenerate : IGenerateSettlementsUseCase
    {
        public List<(GenerateSettlementsCommand Command, int? AdminId)> Calls { get; } = [];
        public Exception? Throw { get; set; }

        public Task<SettlementGenerationResultDto> ExecuteAsync(GenerateSettlementsCommand command, int? adminUserId, CancellationToken cancellationToken)
        {
            Calls.Add((command, adminUserId));
            if (Throw is not null) throw Throw;
            return Task.FromResult(new SettlementGenerationResultDto(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), "WEEKLY",
                [new OwnerSettlementOutcomeDto(1, SettlementOutcomes.Created, null, 5, "STL-20261102-0001", 85_000, false, [], "AAAA")]));
        }
    }

    private sealed class StubRegenerate : IRegenerateSettlementUseCase
    {
        public List<(int Id, string? RowVersion, int? AdminId)> Calls { get; } = [];

        public Task<OwnerSettlementOutcomeDto> ExecuteAsync(int settlementId, string? rowVersion, int? adminUserId, CancellationToken cancellationToken)
        {
            Calls.Add((settlementId, rowVersion, adminUserId));
            if (rowVersion is null) throw new ValidationException("rowVersion không được để trống.");
            return Task.FromResult(new OwnerSettlementOutcomeDto(1, SettlementOutcomes.Regenerated, null, settlementId, "STL-20261102-0001", 1, false, [],
                "BBBB"));
        }
    }

    private readonly RSA _signingKey = RSA.Create(2048);
    private readonly string _publicKeyPath = Path.Combine(Path.GetTempPath(), $"payment-settlements-{Guid.NewGuid():N}.pem");
    private readonly StubGenerate _generate = new();
    private readonly StubRegenerate _regenerate = new();
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        File.WriteAllText(_publicKeyPath, _signingKey.ExportSubjectPublicKeyInfoPem());
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ApplicationName = typeof(AdminSettlementsController).Assembly.GetName().Name,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.AddServiceDefaults("PaymentService");
        builder.Services.AddJwtAuth(_publicKeyPath);
        builder.Services.AddSingleton<IGenerateSettlementsUseCase>(_generate);
        builder.Services.AddSingleton<IRegenerateSettlementUseCase>(_regenerate);
        _app = builder.Build();
        _app.UseServiceDefaults();
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        _signingKey.Dispose();
        try { File.Delete(_publicKeyPath); } catch (IOException) { }
    }

    private string Token(string role, string sub = "7")
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", sub), new Claim("role", role)]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(30),
            Issuer = "ParkingManagement",
            Audience = "ParkingManagement.Clients",
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_signingKey), SecurityAlgorithms.RsaSha256),
        });
    }

    private Task<HttpResponseMessage> Post(string url, string body, string? role = "Admin")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (role is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(role));
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task Generate_Admin_PassesBodyAndAdminFromToken()
    {
        var response = await Post("/api/v1/payments/admin/settlements/generate", """{ "periodStart": "2026-11-02", "ownerProfileId": 3 }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (command, adminId) = Assert.Single(_generate.Calls);
        Assert.Equal(new GenerateSettlementsCommand(new DateOnly(2026, 11, 2), 3), command);
        Assert.Equal(7, adminId);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-11-02", body.GetProperty("periodStart").GetString());
        Assert.Equal("Created", body.GetProperty("owners")[0].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Generate_EmptyBody_MeansLatestPeriodAllOwners()
    {
        Assert.Equal(HttpStatusCode.OK, (await Post("/api/v1/payments/admin/settlements/generate", "{}")).StatusCode);
        Assert.Equal(new GenerateSettlementsCommand(null, null), Assert.Single(_generate.Calls).Command);
    }

    [Fact]
    public async Task Generate_WithoutToken_401_NonAdmin_403()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("/api/v1/payments/admin/settlements/generate", "{}", role: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post("/api/v1/payments/admin/settlements/generate", "{}", role: "LotOwner")).StatusCode);
        Assert.Empty(_generate.Calls);
    }

    [Theory]
    [InlineData(typeof(ConflictException), HttpStatusCode.Conflict)]
    [InlineData(typeof(DependencyUnavailableException), HttpStatusCode.ServiceUnavailable)]
    [InlineData(typeof(ValidationException), HttpStatusCode.BadRequest)]
    public async Task Generate_UseCaseErrors_MapToStandardStatus(Type exception, HttpStatusCode expected)
    {
        _generate.Throw = (Exception)Activator.CreateInstance(exception, "x")!;
        Assert.Equal(expected, (await Post("/api/v1/payments/admin/settlements/generate", "{}")).StatusCode);
    }

    [Fact]
    public async Task Regenerate_PassesIdRowVersionAndAdmin()
    {
        var response = await Post("/api/v1/payments/admin/settlements/12/regenerate", """{ "rowVersion": "AAAA" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((12, "AAAA", 7), Assert.Single(_regenerate.Calls));
        Assert.Equal("BBBB", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rowVersion").GetString());
    }

    [Fact]
    public async Task Regenerate_MissingRowVersion_400()
        => Assert.Equal(HttpStatusCode.BadRequest, (await Post("/api/v1/payments/admin/settlements/12/regenerate", "{}")).StatusCode);

    [Fact]
    public async Task Regenerate_NonAdmin_403()
        => Assert.Equal(HttpStatusCode.Forbidden,
            (await Post("/api/v1/payments/admin/settlements/12/regenerate", """{ "rowVersion": "AAAA" }""", role: "Staff")).StatusCode);
}

// US-080: job nền chỉ log lỗi của một lượt (lượt sau thử lại), không làm sập service.
public class SettlementGenerationJobTests
{
    private sealed class ThrowingRun(Exception exception) : IRunSettlementGenerationUseCase
    {
        public int Calls { get; private set; }

        public Task<SettlementRunResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            throw exception;
        }
    }

    private static (SettlementGenerationJob Job, ThrowingRun Run) Create(Exception exception)
    {
        var run = new ThrowingRun(exception);
        var services = new ServiceCollection().AddScoped<IRunSettlementGenerationUseCase>(_ => run).BuildServiceProvider();
        var job = new SettlementGenerationJob(services.GetRequiredService<IServiceScopeFactory>(), new SettlementJobSchedule(true, TimeSpan.FromHours(1)),
            TimeProvider.System, NullLogger<SettlementGenerationJob>.Instance);
        return (job, run);
    }

    [Fact]
    public async Task SourceUnavailable_IsLoggedNotThrown()
    {
        var (job, run) = Create(new DependencyUnavailableException("AdminService trả HTTP 503."));
        Assert.Null(await job.RunOnceAsync(CancellationToken.None));
        Assert.Equal(1, run.Calls);
    }

    [Fact]
    public async Task UnexpectedError_IsLoggedNotThrown()
    {
        var (job, _) = Create(new InvalidOperationException("boom"));
        Assert.Null(await job.RunOnceAsync(CancellationToken.None));
    }
}
