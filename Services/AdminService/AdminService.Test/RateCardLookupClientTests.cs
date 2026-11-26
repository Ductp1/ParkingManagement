using System.Net;
using System.Text;
using AdminService.Infrastructure.Integrations;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Exceptions;
using Xunit;

namespace AdminService.Test;

/// <summary>US-055 AC2: chỉ bảng giá đang hiệu lực mới cho duyệt bãi.</summary>
public class RateCardLookupClientTests
{
    private static readonly DateTime Now = new(2026, 11, 27, 3, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("[]", false)]
    [InlineData("""[{"isActive":true,"effectiveFromUtc":"2026-11-01T00:00:00Z","effectiveToUtc":null}]""", true)]
    [InlineData("""[{"isActive":false,"effectiveFromUtc":"2026-11-01T00:00:00Z","effectiveToUtc":null}]""", false)]
    [InlineData("""[{"isActive":true,"effectiveFromUtc":"2026-12-01T00:00:00Z","effectiveToUtc":null}]""", false)]
    [InlineData("""[{"isActive":true,"effectiveFromUtc":"2026-11-01T00:00:00Z","effectiveToUtc":"2026-11-27T03:00:00Z"}]""", false)]
    public async Task Only_a_card_in_effect_counts(string body, bool expected)
    {
        var handler = new Handler(HttpStatusCode.OK, body);

        Assert.Equal(expected, await Client(handler).HasActiveRateCardAsync(9, Now, CancellationToken.None));
        Assert.Equal("http://gateway.test/api/v1/rate-cards?parkingLotId=9", handler.Url);
    }

    [Fact]
    public async Task Payment_service_error_is_dependency_unavailable()
        => await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            Client(new Handler(HttpStatusCode.InternalServerError, "{}")).HasActiveRateCardAsync(9, Now, CancellationToken.None));

    private static RateCardLookupClient Client(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://gateway.test/") }, NullLogger<RateCardLookupClient>.Instance);

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? Url { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
