using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using AdminService.API.Controllers;
using AdminService.API.Security;
using AdminService.Application.Features;
using AdminService.Application.Features.Settings;
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
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

/// <summary>Tham số SETTLEMENT_CYCLE giả: trả các thay đổi có mốc ≤ changesUpToUtc như SettingsQueries thật.</summary>
internal sealed class FakeCycleSettings(string? defaultValue = "WEEKLY", params SystemConfigChangePoint[] changes) : ISettingsQueries
{
    public List<(string Key, DateTime ChangesUpToUtc)> Calls { get; } = [];

    public Task<IReadOnlyList<SystemConfigRecord>> ListConfigsAsync(DateTime changesUpToUtc, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<SystemConfigRecord?> FindConfigByKeyAsync(string key, DateTime changesUpToUtc, CancellationToken cancellationToken)
    {
        Calls.Add((key, changesUpToUtc));
        return Task.FromResult(defaultValue is null || key != ResolveInternalSystemConfigUseCase.SettlementCycleKey
            ? null
            : new SystemConfigRecord(2, key, defaultValue, "string", "Chu kỳ quyết toán cho chủ bãi",
                [.. changes.Where(c => c.EffectiveFromUtc <= changesUpToUtc)]));
    }

    public Task<IReadOnlyList<FeatureFlagDto>> ListFeatureFlagsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}

// US-080: PaymentService đọc chu kỳ quyết toán có hiệu lực tại đầu kỳ (use case thuần, đồng hồ cố định).
public class ResolveInternalSystemConfigUseCaseTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MonthlyFrom = new(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc);   // 00:00 ICT 02/11/2026

    private static ResolveInternalSystemConfigUseCase UseCase(ISettingsQueries settings)
        => new(settings, new FixedClock(new DateTimeOffset(Now)));

    [Fact]
    public async Task NoChange_ReturnsDefaultValue()
    {
        var result = await UseCase(new FakeCycleSettings()).ExecuteAsync("PaymentService", "SETTLEMENT_CYCLE",
            new DateTime(2026, 10, 26, 17, 0, 0, DateTimeKind.Utc));

        Assert.Equal("SETTLEMENT_CYCLE", result.Key);
        Assert.Equal("WEEKLY", result.Value);
        Assert.True(result.IsDefault);
        Assert.Null(result.EffectiveFromUtc);
    }

    [Fact]
    public async Task ChangeAlreadyEffective_ReturnsChangedValueWithEffectiveFrom()
    {
        var settings = new FakeCycleSettings("WEEKLY", new SystemConfigChangePoint(5, "MONTHLY", MonthlyFrom, null));

        var result = await UseCase(settings).ExecuteAsync("PaymentService", "SETTLEMENT_CYCLE", MonthlyFrom);

        Assert.Equal("MONTHLY", result.Value);
        Assert.False(result.IsDefault);
        Assert.Equal(MonthlyFrom, result.EffectiveFromUtc);
    }

    [Fact]
    public async Task ChangeNotYetEffectiveAtInstant_ReturnsOldValue()
    {
        var settings = new FakeCycleSettings("WEEKLY", new SystemConfigChangePoint(5, "MONTHLY", MonthlyFrom, null));

        var result = await UseCase(settings).ExecuteAsync("PaymentService", "SETTLEMENT_CYCLE", MonthlyFrom.AddTicks(-1));

        Assert.Equal("WEEKLY", result.Value);
        Assert.True(result.IsDefault);
    }

    [Fact]
    public async Task CancelledChange_IsIgnored()
    {
        var settings = new FakeCycleSettings("WEEKLY",
            new SystemConfigChangePoint(5, "BIWEEKLY", MonthlyFrom, CancelledAtUtc: MonthlyFrom.AddDays(-1)));

        var result = await UseCase(settings).ExecuteAsync("PaymentService", "SETTLEMENT_CYCLE", Now);

        Assert.Equal("WEEKLY", result.Value);
    }

    [Fact]
    public async Task KeyIsCaseInsensitive_AndQueriedWithChangesUpToInstant()
    {
        var settings = new FakeCycleSettings();
        var at = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

        var result = await UseCase(settings).ExecuteAsync("PaymentService", " settlement_cycle ", at);

        Assert.Equal("SETTLEMENT_CYCLE", result.Key);
        Assert.Equal(("SETTLEMENT_CYCLE", at), Assert.Single(settings.Calls));
    }

    [Fact]
    public async Task NoInstant_UsesNow()
    {
        var result = await UseCase(new FakeCycleSettings()).ExecuteAsync("PaymentService", "SETTLEMENT_CYCLE", null);
        Assert.Equal(Now, result.AtUtc);
    }

    [Fact]
    public async Task UnspecifiedKind_IsTreatedAsUtc()
    {
        var result = await UseCase(new FakeCycleSettings()).ExecuteAsync("PaymentService", "SETTLEMENT_CYCLE",
            new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Unspecified));
        Assert.Equal(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc), result.AtUtc);
        Assert.Equal(DateTimeKind.Utc, result.AtUtc.Kind);
    }

    [Fact]
    public async Task WithinClockSkew_IsAccepted_BeyondIsRejected()
    {
        var useCase = UseCase(new FakeCycleSettings());
        await useCase.ExecuteAsync("PaymentService", "SETTLEMENT_CYCLE", Now.AddMinutes(5));
        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync("PaymentService", "SETTLEMENT_CYCLE", Now.AddMinutes(5).AddSeconds(1)));
    }

    [Theory]
    [InlineData("PaymentService", "DEFAULT_COMMISSION_RATE")]   // tham số ngoài danh sách được đọc
    [InlineData("SupportService", "SETTLEMENT_CYCLE")]          // service không được đọc tham số nào
    public async Task NotAllowed_ThrowsForbidden(string caller, string key)
    {
        var settings = new FakeCycleSettings();
        await Assert.ThrowsAsync<ForbiddenException>(() => UseCase(settings).ExecuteAsync(caller, key, Now));
        Assert.Empty(settings.Calls);                                // từ chối trước khi đọc database
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task EmptyKey_ThrowsValidation(string? key)
        => await Assert.ThrowsAsync<ValidationException>(() => UseCase(new FakeCycleSettings()).ExecuteAsync("PaymentService", key, Now));

    [Fact]
    public async Task MissingConfig_ThrowsDependencyUnavailable()
        => await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            UseCase(new FakeCycleSettings(defaultValue: null)).ExecuteAsync("PaymentService", "SETTLEMENT_CYCLE", Now));
}

// US-080: endpoint nội bộ chạy trên TestServer với đúng pipeline của AdminService (khóa nội bộ, xử lý lỗi chuẩn); database thay bằng fake.
public sealed class InternalSystemConfigEndpointTests : IAsyncLifetime
{
    private const string Url = "/internal/v1/system-configs/SETTLEMENT_CYCLE/resolve";
    private const string PaymentKey = "payment-service-internal-key-0123456789abcdef";
    private const string SupportKey = "support-service-internal-key-0123456789abcdef";

    private readonly RSA signingKey = RSA.Create(2048);
    private readonly string publicKeyPath = Path.Combine(Path.GetTempPath(), $"admin-cycle-{Guid.NewGuid():N}.pem");
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

    private async Task StartAsync(Dictionary<string, string?> configuration, ISettingsQueries? settings = null)
    {
        if (app is not null)
        {
            client.Dispose();
            await app.DisposeAsync();
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ApplicationName = typeof(InternalSystemConfigsController).Assembly.GetName().Name,
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Logging.ClearProviders();

        builder.AddServiceDefaults("AdminService");
        builder.Services.AddJwtAuth(publicKeyPath);
        builder.Services.AddInternalEventReceiver();
        builder.Services.AddSingleton<ISettingsQueries>(settings ?? new FakeCycleSettings("WEEKLY",
            new SystemConfigChangePoint(5, "MONTHLY", new DateTime(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc), null)));
        builder.Services.AddScoped<IResolveInternalSystemConfigUseCase, ResolveInternalSystemConfigUseCase>();

        app = builder.Build();
        app.UseServiceDefaults();
        await app.StartAsync();
        client = app.GetTestClient();
    }

    private Task<HttpResponseMessage> Get(string? bearer, string query = "?atUtc=2026-11-02T00:00:00Z", string url = Url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url + query);
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return client.SendAsync(request);
    }

    private string UserToken(string role)
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

    [Fact]
    public async Task PaymentServiceKey_Returns200WithEffectiveValue()
    {
        var response = await Get(PaymentKey);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SETTLEMENT_CYCLE", body.GetProperty("key").GetString());
        Assert.Equal("MONTHLY", body.GetProperty("value").GetString());
        Assert.False(body.GetProperty("isDefault").GetBoolean());
        Assert.Equal(new DateTime(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc), body.GetProperty("effectiveFromUtc").GetDateTime().ToUniversalTime());
        Assert.Equal(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc), body.GetProperty("atUtc").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task BeforeChange_Returns200WithDefault()
    {
        var body = await (await Get(PaymentKey, "?atUtc=2026-10-26T17:00:00Z")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("WEEKLY", body.GetProperty("value").GetString());
        Assert.True(body.GetProperty("isDefault").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("effectiveFromUtc").ValueKind);
    }

    [Fact]
    public async Task OffsetInstant_IsConvertedToUtc()
    {
        // 00:00 ICT 02/11 = 17:00Z 01/11 – đúng mốc thay đổi nên đã là MONTHLY.
        var body = await (await Get(PaymentKey, "?atUtc=2026-11-02T00:00:00%2B07:00")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MONTHLY", body.GetProperty("value").GetString());
    }

    [Fact]
    public async Task NoInstant_Returns200ForNow()
        => Assert.Equal(HttpStatusCode.OK, (await Get(PaymentKey, query: "")).StatusCode);

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key-wrong-key-wrong-key-wrong-key-0000")]
    public async Task MissingOrWrongKey_Returns401(string? key)
        => Assert.Equal(HttpStatusCode.Unauthorized, (await Get(key)).StatusCode);

    [Fact]
    public async Task UserJwtInsteadOfInternalKey_Returns401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await Get(UserToken("Admin"))).StatusCode);

    [Fact]
    public async Task MalformedInstantWithoutKey_Returns401NotBadRequest()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await Get(null, "?atUtc=not-a-date")).StatusCode);

    [Fact]
    public async Task KeyOfAnotherService_Returns403()
        => Assert.Equal(HttpStatusCode.Forbidden, (await Get(SupportKey)).StatusCode);

    [Fact]
    public async Task KeyOutsideAllowList_Returns403()
        => Assert.Equal(HttpStatusCode.Forbidden,
            (await Get(PaymentKey, url: "/internal/v1/system-configs/OTP_MAX_ATTEMPTS/resolve")).StatusCode);

    [Theory]
    [InlineData("?atUtc=not-a-date")]
    [InlineData("?atUtc=2099-01-01T00:00:00Z")]
    public async Task InvalidInstant_Returns400(string query)
        => Assert.Equal(HttpStatusCode.BadRequest, (await Get(PaymentKey, query)).StatusCode);

    [Fact]
    public async Task NoKeysConfigured_Returns503()
    {
        await StartAsync([]);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Get(PaymentKey)).StatusCode);
    }

    [Fact]
    public async Task ConfigMissing_Returns503()
    {
        await StartAsync(new() { ["InternalEvents:ApiKeys:PaymentService"] = PaymentKey }, new FakeCycleSettings(defaultValue: null));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Get(PaymentKey)).StatusCode);
    }
}
