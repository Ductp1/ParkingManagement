using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Commission;
using PaymentService.Infrastructure;
using PaymentService.Infrastructure.Commission;

namespace PaymentService.Test;

// US-079: client gọi AdminService POST /internal/v1/commission-rates/resolve + đọc cấu hình hoa hồng.
public class AdminCommissionRateClientTests
{
    private const string Key = "payment-service-internal-key-0123456789abcdef";
    private static readonly AdminServiceConnection Connection = new(new Uri("http://admin.test:5108/"), Key, TimeSpan.FromMilliseconds(300));

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return await respond(request, cancellationToken);
        }
    }

    private static Handler Returning(HttpStatusCode status, string json)
        => new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") }));

    private static readonly (int, DateTime)[] Items = [(7, new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc))];

    [Fact]
    public async Task Resolve_PostsItemsWithInternalKey_AndParsesQuotes()
    {
        var handler = Returning(HttpStatusCode.OK, """
            { "items": [ { "parkingLotId": 7, "atUtc": "2026-11-05T03:00:00Z", "rate": 0.075, "source": "LotContract", "reason": null,
                           "contractNumber": "HD-SP-2026-0007", "contractVersion": 2, "effectiveFrom": "2026-10-01", "effectiveTo": "2027-09-30" } ] }
            """);

        var quotes = await new AdminCommissionRateClient(new HttpClient(handler), Connection).ResolveAsync(Items, CancellationToken.None);

        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://admin.test:5108/internal/v1/commission-rates/resolve", request.RequestUri!.ToString());
        Assert.Equal(("Bearer", Key), (request.Headers.Authorization!.Scheme, request.Headers.Authorization.Parameter));
        var item = JsonDocument.Parse(body).RootElement.GetProperty("items")[0];
        Assert.Equal((7, "2026-11-05T03:00:00Z"), (item.GetProperty("parkingLotId").GetInt32(), item.GetProperty("atUtc").GetString()));
        Assert.Equal(new CommissionRateQuote(7, Items[0].Item2, 0.075m, "LotContract", null, "HD-SP-2026-0007", 2), Assert.Single(quotes));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Resolve_HttpError_ThrowsUnavailable(HttpStatusCode status)
    {
        var client = new AdminCommissionRateClient(new HttpClient(Returning(status, "{}")), Connection);
        var ex = await Assert.ThrowsAsync<CommissionRateUnavailableException>(() => client.ResolveAsync(Items, CancellationToken.None));
        Assert.Contains(((int)status).ToString(), ex.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    public async Task Resolve_BadBody_ThrowsUnavailable(string json)
    {
        var client = new AdminCommissionRateClient(new HttpClient(Returning(HttpStatusCode.OK, json)), Connection);
        await Assert.ThrowsAsync<CommissionRateUnavailableException>(() => client.ResolveAsync(Items, CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_Timeout_ThrowsUnavailable()
    {
        var handler = new Handler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = new AdminCommissionRateClient(new HttpClient(handler), Connection);

        var ex = await Assert.ThrowsAsync<CommissionRateUnavailableException>(() => client.ResolveAsync(Items, CancellationToken.None));
        Assert.Contains("không phản hồi", ex.Message);
    }

    [Fact]
    public async Task Resolve_NetworkError_ThrowsUnavailable()
    {
        var handler = new Handler((_, _) => throw new HttpRequestException("connection refused"));
        var client = new AdminCommissionRateClient(new HttpClient(handler), Connection);
        await Assert.ThrowsAsync<CommissionRateUnavailableException>(() => client.ResolveAsync(Items, CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Resolve_WithoutApiKey_DoesNotCallAdminService(string? apiKey)
    {
        var handler = Returning(HttpStatusCode.OK, "{}");
        var client = new AdminCommissionRateClient(new HttpClient(handler), Connection with { ApiKey = apiKey });

        await Assert.ThrowsAsync<CommissionRateUnavailableException>(() => client.ResolveAsync(Items, CancellationToken.None));
        Assert.Empty(handler.Calls);
    }

    // ===== Cấu hình =====

    private static IConfiguration Config(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void Policy_DefaultsToAllPurposes_AndBatch200()
    {
        var policy = CommissionServiceCollectionExtensions.ReadPolicy(Config().GetSection("Commission"));
        Assert.Empty(policy.ExcludedPurposes);
        Assert.Equal(200, policy.BatchSize);
    }

    [Fact]
    public void Policy_ParsesExcludedPurposes_CaseInsensitive_AndClampsBatch()
    {
        var policy = CommissionServiceCollectionExtensions.ReadPolicy(Config(
            ("Commission:ExcludedPurposes:0", "overstay"), ("Commission:ExcludedPurposes:1", "MonthlyPass"),
            ("Commission:BatchSize", "1000")).GetSection("Commission"));

        Assert.Equal([PaymentPurpose.Overstay, PaymentPurpose.MonthlyPass], policy.ExcludedPurposes);
        Assert.Equal(200, policy.BatchSize);
    }

    [Fact]
    public void Policy_UnknownPurpose_FailsAtStartup()
        => Assert.Throws<InvalidOperationException>(() =>
            CommissionServiceCollectionExtensions.ReadPolicy(Config(("Commission:ExcludedPurposes:0", "Penalty")).GetSection("Commission")));

    [Fact]
    public void AdminServiceConnection_ReadsBaseUrlKeyAndTimeout()
    {
        var connection = CommissionServiceCollectionExtensions.ReadAdminService(Config(
            ("Services:AdminService:BaseUrl", "http://localhost:5108"), ("Services:AdminService:ApiKey", " k "),
            ("Commission:AdminTimeoutSeconds", "2")));

        Assert.Equal(new AdminServiceConnection(new Uri("http://localhost:5108/"), "k", TimeSpan.FromSeconds(2)), connection);
        Assert.True(connection.IsConfigured);
        Assert.False(CommissionServiceCollectionExtensions.ReadAdminService(Config(("Services:AdminService:BaseUrl", "http://x"))).IsConfigured);
        Assert.Throws<InvalidOperationException>(() =>
            CommissionServiceCollectionExtensions.ReadAdminService(Config(("Services:AdminService:BaseUrl", "ftp://x"))));
    }
}
