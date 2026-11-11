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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.API.Controllers;
using PaymentService.Application.Features.Settlements;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Persistence.Queries;
using static PaymentService.Test.OwnerRevenueTestData;

namespace PaymentService.Test;

// US-080: API duyệt / chi / điều chỉnh (Admin) và xem kỳ đã chốt (chủ bãi) qua HTTP thật (Kestrel 127.0.0.1, cổng ngẫu nhiên),
// pipeline thật (JWT RS256, RequireAdmin / RequireOwner, xử lý lỗi chuẩn); use case thật trên dữ liệu trong bộ nhớ.
public sealed class SettlementReviewEndpointTests : IAsyncLifetime
{
    private const int AdminA = 7;
    private const int AdminB = 9;
    private static readonly DateOnly Week45 = new(2026, 11, 2);

    private readonly RSA _signingKey = RSA.Create(2048);
    private readonly string _publicKeyPath = Path.Combine(Path.GetTempPath(), $"payment-review-{Guid.NewGuid():N}.pem");
    private readonly InMemorySettlementReview _review = new();
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private Settlement _draft = null!;
    private Settlement _blocked = null!;
    private Settlement _paidA = null!;
    private Settlement _approvedB = null!;

    public async Task InitializeAsync()
    {
        _draft = _review.Add(OwnerA, Week45, 90_000);
        _blocked = _review.Add(OwnerB, Week45, 50_000, blocked: SettlementBlockReasons.HoldsNotLoaded);
        _paidA = _review.Add(OwnerA, Week45.AddDays(-7), 70_000, SettlementStatus.Paid, null,
            new SettlementLine { LineType = SettlementLineType.Booking, Amount = 80_000, CommissionAmount = 8_000, ReferenceCode = "BK-0001",
                ParkingLotId = LotA1, PaymentId = 1, OccurredAtUtc = Utc(2026, 10, 28) });
        _approvedB = _review.Add(OwnerB, Week45.AddDays(-7), 40_000, SettlementStatus.Approved);

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
        builder.Services.AddSingleton<ISettlementQueries>(_review);
        builder.Services.AddSingleton<ISettlementReviewRepository>(_review);
        builder.Services.AddSingleton(new SettlementReviewOptions(RequireDifferentApprover: true));
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Utc(2026, 11, 10, 3)));
        builder.Services.AddScoped<IListSettlementsUseCase, ListSettlementsUseCase>();
        builder.Services.AddScoped<IGetSettlementUseCase, GetSettlementUseCase>();
        builder.Services.AddScoped<IExportSettlementsUseCase, ExportSettlementsUseCase>();
        builder.Services.AddScoped<ISubmitSettlementUseCase, SubmitSettlementUseCase>();
        builder.Services.AddScoped<IApproveSettlementUseCase, ApproveSettlementUseCase>();
        builder.Services.AddScoped<IMarkSettlementPaidUseCase, MarkSettlementPaidUseCase>();
        builder.Services.AddScoped<ICreateAdjustmentUseCase, CreateAdjustmentUseCase>();
        builder.Services.AddScoped<IListOwnerSettlementsUseCase, ListOwnerSettlementsUseCase>();
        builder.Services.AddScoped<IGetOwnerSettlementUseCase, GetOwnerSettlementUseCase>();
        builder.Services.AddScoped<IExportOwnerSettlementLinesUseCase, ExportOwnerSettlementLinesUseCase>();
        builder.Services.AddSingleton<IGenerateSettlementsUseCase, UnusedGenerate>();         // controller cần qua constructor
        builder.Services.AddSingleton<IRegenerateSettlementUseCase, UnusedRegenerate>();
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

    private string Token(string role, int sub, int? ownerProfileId = null)
    {
        var claims = new List<Claim> { new("sub", sub.ToString()), new("role", role) };
        if (ownerProfileId is { } owner) claims.Add(new Claim("OwnerProfileId", owner.ToString()));
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims), IssuedAt = now, NotBefore = now, Expires = now.AddMinutes(30),
            Issuer = "ParkingManagement", Audience = "ParkingManagement.Clients",
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_signingKey), SecurityAlgorithms.RsaSha256),
        });
    }

    private sealed class UnusedGenerate : IGenerateSettlementsUseCase
    {
        public Task<SettlementGenerationResultDto> ExecuteAsync(GenerateSettlementsCommand command, int? adminUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class UnusedRegenerate : IRegenerateSettlementUseCase
    {
        public Task<OwnerSettlementOutcomeDto> ExecuteAsync(int settlementId, string? rowVersion, int? adminUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return _client.SendAsync(request);
    }

    private string Admin(int sub = AdminA) => Token("Admin", sub);
    private string Owner(int owner) => Token("LotOwner", 100 + owner, owner);
    private static string Rv(Settlement s) => Convert.ToBase64String(s.RowVersion);

    // ===================== Quyền =====================

    [Theory]
    [InlineData("/api/v1/payments/admin/settlements")]
    [InlineData("/api/v1/payments/owner/settlements")]
    public async Task WithoutToken_401(string url)
        => Assert.Equal(HttpStatusCode.Unauthorized, (await Send(HttpMethod.Get, url, null)).StatusCode);

    [Fact]
    public async Task RoleMismatch_403()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Get, "/api/v1/payments/admin/settlements", Owner(OwnerA))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Get, "/api/v1/payments/owner/settlements", Admin())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, "/api/v1/payments/admin/adjustments", Token("Staff", 3),
            new { ownerProfileId = OwnerA, amount = 1000, reason = "x" })).StatusCode);
    }

    // ===================== Admin =====================

    [Fact]
    public async Task AdminList_And_Detail()
    {
        var list = await (await Send(HttpMethod.Get, "/api/v1/payments/admin/settlements?status=Draft&pageSize=10", Admin()))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, list.GetProperty("totalCount").GetInt32());

        var detail = await Send(HttpMethod.Get, $"/api/v1/payments/admin/settlements/{_draft.Id}", Admin());
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["Regenerate", "Submit"], body.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Get, "/api/v1/payments/admin/settlements/999999", Admin())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Send(HttpMethod.Get, "/api/v1/payments/admin/settlements?status=Closed", Admin())).StatusCode);
    }

    [Fact]
    public async Task SubmitBlocked_409_SubmitDraft_200_SelfApprove_403_OtherAdmin_200()
    {
        Assert.Equal(HttpStatusCode.Conflict, (await Send(HttpMethod.Post, $"/api/v1/payments/admin/settlements/{_blocked.Id}/submit", Admin(),
            new { rowVersion = Rv(_blocked) })).StatusCode);

        var submitted = await Send(HttpMethod.Post, $"/api/v1/payments/admin/settlements/{_draft.Id}/submit", Admin(AdminA),
            new { rowVersion = Rv(_draft) });
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var rowVersion = (await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rowVersion").GetString();

        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, $"/api/v1/payments/admin/settlements/{_draft.Id}/approve",
            Admin(AdminA), new { rowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Post, $"/api/v1/payments/admin/settlements/{_draft.Id}/approve",
            Admin(AdminB), new { rowVersion })).StatusCode);
        Assert.Equal(AdminB, _draft.ApprovedByUserId);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(HttpMethod.Post, $"/api/v1/payments/admin/settlements/{_draft.Id}/approve",
            Admin(AdminB), new { rowVersion })).StatusCode);                                                    // RowVersion cũ
    }

    [Fact]
    public async Task MarkPaid_MissingReference_400_NotApproved_409_Ok_200()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(HttpMethod.Post, $"/api/v1/payments/admin/settlements/{_approvedB.Id}/mark-paid", Admin(),
            new { rowVersion = Rv(_approvedB), payoutReference = "", paidAtUtc = "2026-11-10T02:00:00Z" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(HttpMethod.Post, $"/api/v1/payments/admin/settlements/{_draft.Id}/mark-paid", Admin(),
            new { rowVersion = Rv(_draft), payoutReference = "FT1", paidAtUtc = "2026-11-10T02:00:00Z" })).StatusCode);

        _approvedB.ApprovedAtUtc = Utc(2026, 11, 9);
        var paid = await Send(HttpMethod.Post, $"/api/v1/payments/admin/settlements/{_approvedB.Id}/mark-paid", Admin(),
            new { rowVersion = Rv(_approvedB), payoutReference = "FT26314XYZ", paidAtUtc = "2026-11-10T02:00:00Z" });
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal("Paid", (await paid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task AdminExport_IsCsvWithHeader()
    {
        var response = await Send(HttpMethod.Get, "/api/v1/payments/admin/settlements/export?periodStart=2026-11-02", Admin());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("settlements-20261102.csv", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        var text = Encoding.UTF8.GetString((await response.Content.ReadAsByteArrayAsync())[3..]).Split("\r\n");
        Assert.Equal(string.Join(",", SettlementCsvWriter.SettlementHeader), text[0]);
        Assert.Equal(3, text.Count(l => l.Length > 0));                                                  // header + 2 kỳ của tuần 45
    }

    [Fact]
    public async Task CreateAdjustment_201_ZeroAmount_400()
    {
        var created = await Send(HttpMethod.Post, "/api/v1/payments/admin/adjustments", Admin(),
            new { ownerProfileId = OwnerA, amount = -150000, reason = "Chargeback BK-0002" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingApproval", body.GetProperty("status").GetString());
        Assert.Equal(AdminA, body.GetProperty("createdByUserId").GetInt32());                              // người tạo lấy từ JWT

        Assert.Equal(HttpStatusCode.BadRequest, (await Send(HttpMethod.Post, "/api/v1/payments/admin/adjustments", Admin(),
            new { ownerProfileId = OwnerA, amount = 0, reason = "x" })).StatusCode);
    }

    // ===================== Chủ bãi =====================

    [Fact]
    public async Task Owner_SeesOwnFinalSettlementsOnly_OtherOwner404()
    {
        var list = await (await Send(HttpMethod.Get, "/api/v1/payments/owner/settlements", Owner(OwnerA))).Content.ReadFromJsonAsync<JsonElement>();
        var item = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(_paidA.Id, item.GetProperty("id").GetInt32());
        Assert.False(item.TryGetProperty("rowVersion", out _));
        Assert.False(item.TryGetProperty("ownerProfileId", out _));

        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Get, $"/api/v1/payments/owner/settlements/{_paidA.Id}", Owner(OwnerA))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Get, $"/api/v1/payments/owner/settlements/{_paidA.Id}", Owner(OwnerB))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Get, $"/api/v1/payments/owner/settlements/{_draft.Id}", Owner(OwnerA))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Get, $"/api/v1/payments/owner/settlements/{_approvedB.Id}/export", Owner(OwnerA))).StatusCode);

        var csv = await Send(HttpMethod.Get, $"/api/v1/payments/owner/settlements/{_paidA.Id}/export", Owner(OwnerA));
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var lines = Encoding.UTF8.GetString((await csv.Content.ReadAsByteArrayAsync())[3..]).Split("\r\n");
        Assert.Equal(string.Join(",", SettlementCsvWriter.LineHeader), lines[0]);
        Assert.StartsWith($"{_paidA.Code},Booking,BK-0001,{LotA1},1,", lines[1]);
    }

    [Fact]
    public async Task OwnerTokenWithoutOwnerProfile_403()
        => Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Get, "/api/v1/payments/owner/settlements", Token("LotOwner", 55))).StatusCode);
}

// US-080: SQL của truy vấn đọc kỳ quyết toán (ToQueryString – không mở database).
public sealed class SettlementReadSqlTests : IDisposable
{
    private readonly PaymentDbContext _db = new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseNpgsql("Host=localhost;Database=pm_payment_sql_shape").Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Filter_SearchAndOwnerStatuses_InDatabase()
    {
        var sql = SettlementReadShapes.Filter(_db.Settlements, new SettlementListFilter(Search: "stl-2026", OwnerProfileId: 1,
            Statuses: [SettlementStatus.Approved, SettlementStatus.Paid])).ToQueryString();
        Assert.Contains("LIKE", sql);
        Assert.Contains("\"OwnerProfileId\" =", sql);
        Assert.Contains("'Approved'", sql);
        Assert.Contains("ORDER BY s.\"PeriodStart\" DESC", sql);
    }

    [Fact]
    public void Aggregates_GroupInDatabase()
    {
        Assert.Contains("GROUP BY", SettlementReadShapes.TotalsByStatus(_db.Settlements, new DateOnly(2026, 11, 2)).ToQueryString());
        Assert.Contains("GROUP BY", SettlementReadShapes.LineTotals(_db.SettlementLines, 1).ToQueryString());
        Assert.Contains("LEFT JOIN", SettlementReadShapes.Adjustments(_db.FinancialAdjustments, _db.Settlements,
            new AdjustmentListFilter(Attached: false, Search: "hoa hồng")).ToQueryString());
        Assert.Contains("count", SettlementReadShapes.Periods(_db.SettlementPeriods, _db.Settlements, 10).ToQueryString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"LineType\" =", SettlementReadShapes.Lines(_db.SettlementLines, 1, SettlementLineType.Refund).ToQueryString());
    }
}
