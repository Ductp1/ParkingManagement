using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.API.Controllers;
using PaymentService.Application;
using PaymentService.Application.Features;
using PaymentService.Application.Features.Reports;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using static PaymentService.Test.OwnerRevenueTestData;

namespace PaymentService.Test;

/// <summary>
/// US-078: gọi HTTP thật (Kestrel 127.0.0.1, cổng ngẫu nhiên) qua đúng pipeline của Program.cs:
/// AddServiceDefaults + AddJwtAuth + AddPaymentApplication + UseServiceDefaults; port Infrastructure thay bằng fake (không cần database).
/// Endpoint thanh toán cũ không token vẫn chạy; báo cáo doanh thu: 401 khi thiếu token, 403 khi sai role, chủ bãi lấy từ token.
/// </summary>
public class PaymentApiAuthorizationTests(PaymentApiAuthorizationTests.ApiHost host) : IClassFixture<PaymentApiAuthorizationTests.ApiHost>
{
    private const string SummaryUrl = "/api/v1/payments/reports/owner-revenue/summary?from=2026-10-01&to=2026-11-01";

    // ----- Endpoint thanh toán / báo giá hiện có: không yêu cầu token -----

    [Fact]
    public async Task PricingQuote_WithoutToken_Returns200()
    {
        var response = await host.Client.GetAsync(
            "/api/v1/pricing/quote?parkingLotId=1&vehicleType=Sedan&startAtUtc=2026-10-05T01:00:00Z&endAtUtc=2026-10-05T02:00:00Z");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PricingQuote_WithInvalidBearerToken_StillReturns200()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "/api/v1/pricing/quote?parkingLotId=1&vehicleType=Sedan&startAtUtc=2026-10-05T01:00:00Z&endAtUtc=2026-10-05T02:00:00Z");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");
        var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task VnpayIpn_WithoutToken_IsProcessed()
    {
        var response = await host.Client.GetAsync("/api/v1/payments/vnpay-ipn?vnp_TxnRef=PAY_1&vnp_ResponseCode=00");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("97", await response.Content.ReadAsStringAsync());   // fake gateway: sai chữ ký → mã 97 theo VNPAY
    }

    [Fact]
    public async Task VnpayReturn_WithoutToken_IsProcessed()
    {
        var response = await host.Client.GetAsync("/api/v1/payments/vnpay-return?vnp_TxnRef=PAY_1&vnp_ResponseCode=00");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GateSettlement_WithoutToken_Returns200()
    {
        var response = await host.Client.PostAsJsonAsync("/api/v1/payments/gate-settlement",
            new GateSettlementCommand("SS-0001", 1, LotA1, OwnerA, null, 20_000, "Cash", 5));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListPaymentsByBooking_WithoutToken_Returns200()
    {
        var response = await host.Client.GetAsync("/api/v1/payments?bookingId=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ----- Báo cáo doanh thu chủ bãi: chỉ LotOwner -----

    [Fact]
    public async Task OwnerRevenueSummary_WithoutToken_Returns401()
    {
        var response = await host.Client.GetAsync(SummaryUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("Driver")]
    [InlineData("Admin")]
    public async Task OwnerRevenueSummary_WithOtherRole_Returns403(string role)
    {
        var response = await host.GetAsync(SummaryUrl, host.MintToken(role, ownerProfileId: OwnerA));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OwnerRevenueSummary_LotOwnerWithoutOwnerProfileClaim_Returns403()
    {
        var response = await host.GetAsync(SummaryUrl, host.MintToken("LotOwner", ownerProfileId: null));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OwnerRevenueSummary_UsesOwnerFromToken_IgnoresOwnerProfileIdInQuery()
    {
        var response = await host.GetAsync($"{SummaryUrl}&ownerProfileId={OwnerB}", host.MintToken("LotOwner", OwnerA));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<OwnerRevenueSummaryDto>(host.Json);
        Assert.Equal(new OwnerRevenueTotalsDto(100_000, 0, 100_000, 1), summary!.Totals);
        Assert.Equal("OriginalPaymentPeriod", summary.RefundBasis);
    }

    [Fact]
    public async Task OwnerRevenueSummary_LotOfAnotherOwner_Returns200WithZeros()
    {
        var response = await host.GetAsync($"{SummaryUrl}&parkingLotId={LotB1}", host.MintToken("LotOwner", OwnerA));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<OwnerRevenueSummaryDto>(host.Json);
        Assert.Equal(new OwnerRevenueTotalsDto(0, 0, 0, 0), summary!.Totals);
        Assert.Empty(summary.ByLot);
    }

    [Fact]
    public async Task OwnerRevenueSummary_InvalidRange_Returns400()
    {
        var response = await host.GetAsync("/api/v1/payments/reports/owner-revenue/summary?from=2026-11-01&to=2026-10-01",
            host.MintToken("LotOwner", OwnerA));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("from phải nhỏ hơn to.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OwnerRevenueSeries_WithoutToken_Returns401()
    {
        var response = await host.Client.GetAsync("/api/v1/payments/reports/owner-revenue/series?from=2026-10-01&to=2026-11-01");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerRevenueSeries_OwnerToken_ReturnsWeeklyPointsOfOwnData()
    {
        var response = await host.GetAsync(
            $"/api/v1/payments/reports/owner-revenue/series?from=2026-10-01&to=2026-11-01&granularity=Week&ownerProfileId={OwnerB}",
            host.MintToken("LotOwner", OwnerA));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var series = await response.Content.ReadFromJsonAsync<OwnerRevenueSeriesDto>(host.Json);
        Assert.Equal("Week", series!.Granularity);
        Assert.Equal(5, series.Points.Count);   // 01–04, 05–11, 12–18, 19–25, 26–31/10
        Assert.Equal(100_000, series.Points.Sum(p => p.GrossRevenue));
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(7)), series.Points[0].PeriodStart);
    }

    [Fact]
    public async Task OwnerRevenueSeries_InvalidGranularity_Returns400()
    {
        var response = await host.GetAsync("/api/v1/payments/reports/owner-revenue/series?granularity=Year",
            host.MintToken("LotOwner", OwnerA));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("granularity phải là Hour, Day, Week hoặc Month.", await response.Content.ReadAsStringAsync());
    }

    /// <summary>Host dùng chung cho cả lớp test: cặp khóa RSA test (UserService giả lập) + Kestrel cổng ngẫu nhiên.</summary>
    public sealed class ApiHost : IAsyncLifetime
    {
        private readonly RSA _signingKey = RSA.Create(2048);
        private readonly string _keyDirectory = Path.Combine(Path.GetTempPath(), "pm-payment-keys-" + Guid.NewGuid().ToString("N")[..8]);
        private WebApplication? _app;

        public HttpClient Client { get; private set; } = null!;
        public System.Text.Json.JsonSerializerOptions Json { get; } = new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

        public async Task InitializeAsync()
        {
            Directory.CreateDirectory(_keyDirectory);
            var publicKeyPath = Path.Combine(_keyDirectory, "public.pem");
            await File.WriteAllTextAsync(publicKeyPath, _signingKey.ExportSubjectPublicKeyInfoPem());

            // ApplicationName = assembly API → MVC nạp đúng các controller của PaymentService.API.
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(PaymentsController).Assembly.GetName().Name,
                ContentRootPath = AppContext.BaseDirectory,
                EnvironmentName = "Testing",
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();

            builder.AddServiceDefaults("PaymentService");
            builder.Services.AddJwtAuth(publicKeyPath);
            builder.Services.AddPaymentApplication();
            builder.Services.AddSingleton<IRateCardRepository, FakeRateCards>();
            builder.Services.AddSingleton<IHolidayRepository, FakeHolidays>();
            builder.Services.AddSingleton<IPaymentQueries, FakePaymentQueries>();
            builder.Services.AddSingleton<IPaymentWriter, FakePaymentWriter>();
            builder.Services.AddSingleton<IPaymentRepository, FakePaymentRepository>();
            builder.Services.AddSingleton<IVnpayGateway, RejectingVnpayGateway>();
            builder.Services.AddSingleton<IOwnerRevenueQueries>(new InMemoryOwnerRevenueQueries(
            [
                Paid(OwnerA, LotA1, 100_000, Utc(2026, 10, 5, 2)),
                Paid(OwnerB, LotB1, 70_000, Utc(2026, 10, 5, 2)),
            ]));

            _app = builder.Build();
            _app.UseServiceDefaults();
            await _app.StartAsync();

            var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            Client = new HttpClient { BaseAddress = new Uri(address) };
        }

        /// <summary>JWT RS256 đúng hợp đồng UserService (issuer/audience của AuthExtensions, claim "role", "OwnerProfileId").</summary>
        public string MintToken(string role, int? ownerProfileId)
        {
            var claims = new List<Claim> { new("sub", "100"), new("role", role) };
            if (ownerProfileId is { } owner) claims.Add(new Claim("OwnerProfileId", owner.ToString()));
            var now = DateTime.UtcNow;
            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(30),
                Issuer = "ParkingManagement",
                Audience = "ParkingManagement.Clients",
                SigningCredentials = new SigningCredentials(new RsaSecurityKey(_signingKey), SecurityAlgorithms.RsaSha256),
            });
        }

        public Task<HttpResponseMessage> GetAsync(string url, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return Client.SendAsync(request);
        }

        public async Task DisposeAsync()
        {
            Client?.Dispose();
            if (_app is not null) await _app.DisposeAsync();
            _signingKey.Dispose();
            try { Directory.Delete(_keyDirectory, true); } catch (IOException) { }
        }
    }

    private sealed class FakeRateCards : IRateCardRepository
    {
        public Task<RateCard?> GetActiveAsync(int parkingLotId, DateTime atUtc, CancellationToken cancellationToken)
        {
            var card = new RateCard { ParkingLotId = parkingLotId };
            card.Rules.Add(new RateRule { VehicleType = VehicleType.Sedan, FromMinute = 0, ToMinute = 60, BlockMinutes = 60, PricePerBlock = 30_000 });
            return Task.FromResult<RateCard?>(card);
        }
    }

    private sealed class FakeHolidays : IHolidayRepository
    {
        public Task<IReadOnlyCollection<DateOnly>> GetHolidaysInRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DateOnly>>([]);
    }

    private sealed class FakePaymentQueries : IPaymentQueries
    {
        public Task<IReadOnlyList<PaymentDto>> ListByBookingAsync(int bookingId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PaymentDto>>([]);
    }

    private sealed class FakePaymentWriter : IPaymentWriter
    {
        public Task<Payment?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
            => Task.FromResult<Payment?>(null);
        public Task AddAsync(Payment payment, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakePaymentRepository : IPaymentRepository
    {
        public Task AddAsync(Payment payment, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Payment?> GetByCodeAsync(string paymentCode, CancellationToken cancellationToken) => Task.FromResult<Payment?>(null);
        public Task SaveCallbackResultAsync(Payment? payment, PaymentCallbackLog callbackLog, OutboxMessage? outboxMessage,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RejectingVnpayGateway : IVnpayGateway
    {
        public string GeneratePaymentUrl(string paymentCode, decimal amount, string orderInfo, string ipAddress, DateTime createdAtUtc,
            DateTime expiresAtUtc) => "https://sandbox.vnpayment.vn/test";
        public bool ValidateCallbackSignature(IReadOnlyDictionary<string, string> parameters) => false;
    }
}
