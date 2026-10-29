using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AdminService.API.Controllers;
using AdminService.API.Security;
using AdminService.Application.Features.AuditLogs;
using AdminService.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;

namespace AdminService.Test;

// T-802: endpoint nội bộ POST /internal/v1/events chạy trên TestServer với đúng pipeline của AdminService
// (AddServiceDefaults + AddJwtAuth + UseServiceDefaults như Program.cs); chỉ thay database bằng fake inbox.
public sealed class InternalEventsEndpointTests : IAsyncLifetime
{
    private const string Url = "/internal/v1/events";
    private const string ParkingKey = "parking-service-internal-key-0123456789abcdef";
    private const string SupportKey = "support-service-internal-key-0123456789abcdef";

    private static readonly Lazy<string> PublicKeyPath = new(() =>
    {
        using var rsa = RSA.Create(2048);
        var path = Path.Combine(Path.GetTempPath(), $"admin-internal-events-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, rsa.ExportSubjectPublicKeyInfoPem());
        return path;
    });

    private readonly FakeInboxRepository repo = new();
    private readonly CapturingLoggerProvider logs = new();
    private WebApplication? app;
    private HttpClient client = null!;

    public Task InitializeAsync() => StartAsync(new()
    {
        ["InternalEvents:ApiKeys:ParkingService"] = ParkingKey,
        ["InternalEvents:ApiKeys:SupportService"] = SupportKey,
    });

    public async Task DisposeAsync()
    {
        client.Dispose();
        if (app is not null) await app.DisposeAsync();
    }

    private async Task StartAsync(Dictionary<string, string?> configuration)
    {
        if (app is not null) await DisposeAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,                 // bật ValidateOnBuild như khi chạy dev
            ApplicationName = typeof(InternalEventsController).Assembly.GetName().Name,
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configuration);
        // appsettings.json của API (được copy sang thư mục test) lọc Microsoft.AspNetCore ở mức Warning: mở ra để thấy JwtBearer.
        builder.Configuration.AddInMemoryCollection([new("Logging:LogLevel:Microsoft.AspNetCore", "Debug")]);
        builder.Logging.ClearProviders().AddProvider(logs).SetMinimumLevel(LogLevel.Debug);

        builder.AddServiceDefaults("AdminService");
        builder.Services.AddJwtAuth(PublicKeyPath.Value);
        builder.Services.AddInternalEventReceiver();
        builder.Services.AddSingleton<IAuditEventInboxRepository>(repo);
        builder.Services.AddScoped<IRecordAuditEventUseCase, RecordAuditEventUseCase>();

        app = builder.Build();
        app.UseServiceDefaults();
        await app.StartAsync();
        client = app.GetTestClient();
    }

    private static AuditRecorded KybApproved(string source = "ParkingService") => new(source, 1, "KybApplication.Approved",
        "KybApplication", "7", "{\"status\":\"UnderReview\"}", "{\"status\":\"Approved\"}", "HD-SP-2026-052")
    { OccurredAtUtc = OccurredUtc };

    // Host dùng TimeProvider.System: mốc phát sinh phải ở quá khứ thật, nếu không use case lấy thời điểm nhận.
    private static readonly DateTime OccurredUtc = DateTime.UtcNow.AddMinutes(-5);

    /// <summary>
    /// Sao chép đúng IdentityOutboxTransport.DeliverAsync (UserService, nhánh EventBus): outbox lưu PayloadJson mặc định,
    /// gửi POST JsonContent.Create(new { EventId = message.Id, message.EventType, message.OccurredAtUtc, Payload }),
    /// header Idempotency-Key = message.Id và Authorization: Bearer {EventBus:ApiKey}.
    /// </summary>
    private static HttpRequestMessage DispatcherRequest(IntegrationEvent ev, string? apiKey = ParkingKey)
    {
        var message = new OutboxMessage
        {
            Id = ev.EventId, EventType = ev.GetType().Name, OccurredAtUtc = ev.OccurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(ev, ev.GetType()),
        };
        using var document = JsonDocument.Parse(message.PayloadJson);
        object payload = new { EventId = message.Id, message.EventType, message.OccurredAtUtc, Payload = document.RootElement.Clone() };
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, Url) { Content = JsonContent.Create(payload) };
        requestMessage.Headers.Add("Idempotency-Key", message.Id.ToString());
        if (!string.IsNullOrWhiteSpace(apiKey)) requestMessage.Headers.Authorization = new("Bearer", apiKey);
        return requestMessage;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();

    private static async Task<string> ProblemDetailAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail!;

    // ==================== Hai đầu khớp nhau ====================

    [Fact]
    public async Task Request_exactly_as_the_outbox_transport_sends_is_recorded()
    {
        var ev = KybApproved();

        using var response = await client.SendAsync(DispatcherRequest(ev));

        response.EnsureSuccessStatusCode();                              // điều kiện thành công của IdentityOutboxTransport
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("Recorded", body.GetProperty("status").GetString());
        Assert.Equal(1, body.GetProperty("auditLogId").GetInt32());

        var log = Assert.Single(repo.AuditLogs);
        Assert.Equal("ParkingService", log.SourceService);
        Assert.Equal(1, log.UserId);
        Assert.Equal("KybApplication.Approved", log.Action);
        Assert.Equal("KybApplication", log.EntityName);
        Assert.Equal("7", log.EntityId);
        Assert.Equal("{\"status\":\"UnderReview\"}", log.OldValuesJson);
        Assert.Equal("{\"status\":\"Approved\"}", log.NewValuesJson);
        Assert.Equal("HD-SP-2026-052", log.Reason);
        Assert.Equal(ev.OccurredAtUtc, log.CreatedAtUtc);
        Assert.Equal(ev.EventId, Assert.Single(repo.Receipts));
    }

    [Fact]
    public async Task Retried_delivery_returns_success_without_a_second_row()
    {
        var ev = KybApproved();

        using var first = await client.SendAsync(DispatcherRequest(ev));
        using var retry = await client.SendAsync(DispatcherRequest(ev));

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);               // 2xx → dispatcher đặt ProcessedAtUtc, hết retry
        Assert.Equal("Duplicate", (await JsonAsync(retry)).GetProperty("status").GetString());
        Assert.Single(repo.AuditLogs);
    }

    [Fact]
    public async Task Other_event_type_returns_202_ignored()
    {
        using var response = await client.SendAsync(DispatcherRequest(new UserLocked(5, "Sai OTP quá 3 lần")));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("Ignored", (await JsonAsync(response)).GetProperty("status").GetString());
        Assert.Empty(repo.AuditLogs);
        Assert.Empty(repo.Receipts);
    }

    [Fact]
    public async Task Bearer_api_key_is_not_rejected_or_logged_as_error_by_the_jwt_middleware()
    {
        // AdminService bật AddJwtAuth: header Bearer chứa khóa nội bộ (không phải JWT) đi qua UseAuthentication trước controller.
        using var response = await client.SendAsync(DispatcherRequest(KybApproved()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("WWW-Authenticate"));     // không có challenge của JwtBearer
        Assert.DoesNotContain(logs.Entries, e => e.Level >= LogLevel.Error);
        // TestServer không hỗ trợ IHttpMaxRequestBodySizeFeature nên RequestSizeLimit ghi warning riêng (Kestrel thì có);
        // phần xác thực (JwtBearer) và ExceptionHandlingMiddleware không được ghi gì từ mức Warning.
        Assert.DoesNotContain(logs.Entries, e => e.Level >= LogLevel.Warning
            && (e.Category.StartsWith("Microsoft.AspNetCore.Authentication", StringComparison.Ordinal)
                || e.Category.StartsWith("Microsoft.AspNetCore.Authorization", StringComparison.Ordinal)
                || e.Category.StartsWith("ParkingManagement", StringComparison.Ordinal)
                || e.Category.StartsWith("AdminService", StringComparison.Ordinal)));
        Assert.Contains(logs.Entries, e => e.Category.StartsWith("Microsoft.AspNetCore.Authentication", StringComparison.Ordinal));   // JwtBearer có chạy
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains(ParkingKey, StringComparison.Ordinal));   // không log khóa
    }

    [Fact]
    public async Task Route_is_internal_and_not_under_the_gateway_admin_prefix()
    {
        var route = typeof(InternalEventsController).GetCustomAttribute<RouteAttribute>()!.Template;

        Assert.StartsWith("internal/", route);                           // Gateway chỉ chuyển /api/v1/admin/** tới AdminService
        Assert.False(route.StartsWith("api/", StringComparison.OrdinalIgnoreCase));
        var request = DispatcherRequest(KybApproved());
        request.RequestUri = new Uri("/api/v1/admin/internal/v1/events", UriKind.Relative);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(repo.AuditLogs);
    }

    // ==================== Xác thực: 401 / 403 / 503 ====================

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Basic cGFya2luZzprZXk=")]
    [InlineData("Bearer sai-khoa-noi-bo-0123456789abcdef0123456789")]
    [InlineData("Bearer eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl")]   // JWT người dùng không thay được khóa nội bộ
    public async Task Missing_or_wrong_internal_key_returns_401_without_touching_the_inbox(string? authorization)
    {
        var request = DispatcherRequest(KybApproved(), apiKey: null);
        if (authorization is not null) request.Headers.TryAddWithoutValidation("Authorization", authorization);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Khóa nội bộ", await ProblemDetailAsync(response), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repo.HasReceivedCount);
        Assert.Empty(repo.AuditLogs);
    }

    [Fact]
    public async Task Key_of_one_service_cannot_record_audit_for_another_service_403()
    {
        using var response = await client.SendAsync(DispatcherRequest(KybApproved("ParkingService"), apiKey: SupportKey));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("SupportService", await ProblemDetailAsync(response));
        Assert.Empty(repo.AuditLogs);
    }

    [Fact]
    public async Task Each_service_key_records_its_own_audit()
    {
        using var response = await client.SendAsync(DispatcherRequest(KybApproved("SupportService"), apiKey: SupportKey));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("SupportService", Assert.Single(repo.AuditLogs).SourceService);
    }

    public static TheoryData<Dictionary<string, string?>> MisconfiguredKeys => new()
    {
        new Dictionary<string, string?>(),                                                   // appsettings.json để trống
        new Dictionary<string, string?> { ["InternalEvents:ApiKeys:ParkingService"] = "" },
        new Dictionary<string, string?> { ["InternalEvents:ApiKeys:ParkingService"] = "ngan-qua" },
        new Dictionary<string, string?>
        {
            ["InternalEvents:ApiKeys:ParkingService"] = ParkingKey, ["InternalEvents:ApiKeys:SupportService"] = ParkingKey,
        },
    };

    [Theory]
    [MemberData(nameof(MisconfiguredKeys))]
    public async Task Missing_or_invalid_key_configuration_fails_closed_with_503(Dictionary<string, string?> configuration)
    {
        await StartAsync(configuration);

        using var response = await client.SendAsync(DispatcherRequest(KybApproved()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);   // dispatcher retry, không mất sự kiện
        Assert.Empty(repo.AuditLogs);
    }

    // ==================== 400 ====================

    [Fact]
    public async Task Idempotency_key_different_from_event_id_returns_400()
    {
        var request = DispatcherRequest(KybApproved());
        request.Headers.Remove("Idempotency-Key");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Header Idempotency-Key phải trùng eventId trong body.", await ProblemDetailAsync(response));
        Assert.Empty(repo.AuditLogs);
    }

    [Fact]
    public async Task Missing_idempotency_key_returns_400()
    {
        var request = DispatcherRequest(KybApproved());
        request.Headers.Remove("Idempotency-Key");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Idempotency-Key", await ProblemDetailAsync(response));
    }

    [Theory]
    [InlineData("không phải JSON")]
    [InlineData("")]
    [InlineData("{\"eventId\":\"8f0d6c1e-3b7a-4f0e-9c55-1a2b3c4d5e6f\",\"eventType\":\"AuditRecorded\",\"occurredAtUtc\":\"2026-10-27T07:55:00Z\",\"payload\":{}}")]
    public async Task Malformed_body_returns_400_in_vietnamese(string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", "8f0d6c1e-3b7a-4f0e-9c55-1a2b3c4d5e6f");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ParkingKey);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Dữ liệu không hợp lệ", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
        Assert.Empty(repo.AuditLogs);
    }

    [Fact]
    public async Task Body_over_256_kb_returns_400()
    {
        var ev = KybApproved() with { NewValuesJson = $"{{\"note\":\"{new string('x', 60 * 1024)}\"}}" };
        var request = DispatcherRequest(ev);
        var json = await request.Content!.ReadAsStringAsync();
        request.Content = new StringContent(json.Insert(1, $"\"padding\":\"{new string(' ', 200 * 1024)}\","), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("256 KB", await ProblemDetailAsync(response));
        Assert.Empty(repo.AuditLogs);
    }

    // ==================== Fakes ====================

    private sealed class FakeInboxRepository : IAuditEventInboxRepository
    {
        private static readonly PropertyInfo IdProperty = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!;
        public List<Guid> Receipts { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];
        public int HasReceivedCount { get; private set; }

        public Task<bool> HasReceivedAsync(Guid eventId, CancellationToken cancellationToken)
        {
            HasReceivedCount++;
            return Task.FromResult(Receipts.Contains(eventId));
        }

        public Task<bool> TryRecordAsync(Guid eventId, AuditLog auditLog, DateTime receivedAtUtc, CancellationToken cancellationToken)
        {
            if (Receipts.Contains(eventId)) return Task.FromResult(false);
            Receipts.Add(eventId);
            IdProperty.SetValue(auditLog, AuditLogs.Count + 1);
            AuditLogs.Add(auditLog);
            return Task.FromResult(true);
        }
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);
        public void Dispose() { }

        private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue(new(category, logLevel, formatter(state, exception) + (exception is null ? "" : " " + exception)));
        }
    }
}
