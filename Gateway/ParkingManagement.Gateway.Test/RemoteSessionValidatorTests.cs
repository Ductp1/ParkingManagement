using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ParkingManagement.Gateway.Authentication;

namespace ParkingManagement.Gateway.Test;

public class RemoteSessionValidatorTests
{
    [Fact]
    public async Task Repeated_requests_reuse_state_and_restore_roles_and_tenant()
    {
        using var setup = new Setup();
        Assert.True(await setup.Validate());
        var principal = Principal();
        Assert.True(await setup.Validator.ValidateAsync(principal, default));
        Assert.Equal(1, setup.Handler.Calls);
        Assert.True(principal.IsInRole("LotOwner"));
        Assert.False(principal.IsInRole("Admin"));
        Assert.Equal("2", principal.FindFirst("OwnerProfileId")?.Value);
        Assert.Equal("3", principal.FindFirst("ParkingLotId")?.Value);
    }

    [Fact]
    public async Task Cache_hits_do_not_extend_lifetime_and_expiry_rechecks_revocation()
    {
        using var setup = new Setup();
        Assert.True(await setup.Validate());
        setup.Clock.Now += TimeSpan.FromSeconds(9);
        Assert.True(await setup.Validate());
        setup.Handler.Status = HttpStatusCode.Unauthorized;
        setup.Clock.Now += TimeSpan.FromSeconds(1);
        Assert.False(await setup.Validate());
        Assert.Equal(2, setup.Handler.Calls);
    }

    [Fact]
    public async Task Different_tokens_do_not_share_cache()
    {
        using var setup = new Setup();
        Assert.True(await setup.Validate());
        setup.Accessor.HttpContext!.Request.Headers.Authorization = "Bearer second-token";
        setup.Handler.Status = HttpStatusCode.Unauthorized;
        Assert.False(await setup.Validate());
        Assert.Equal(2, setup.Handler.Calls);
    }

    [Fact]
    public async Task Concurrent_requests_for_same_token_make_one_remote_call()
    {
        using var setup = new Setup();
        setup.Handler.Delay = true;
        Assert.All(await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => setup.Validate())), Assert.True);
        Assert.Equal(1, setup.Handler.Calls);
    }

    [Fact]
    public async Task Failed_validation_is_not_cached()
    {
        using var setup = new Setup();
        setup.Handler.Status = HttpStatusCode.Unauthorized;
        Assert.False(await setup.Validate());
        setup.Handler.Status = HttpStatusCode.OK;
        Assert.True(await setup.Validate());
        Assert.Equal(2, setup.Handler.Calls);
    }

    [Fact]
    public async Task Cached_state_cannot_be_used_for_another_user()
    {
        using var setup = new Setup();
        Assert.True(await setup.Validate());
        Assert.False(await setup.Validator.ValidateAsync(Principal("99"), default));
    }

    private static ClaimsPrincipal Principal(string id = "1") => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, "Admin")], "Bearer"));

    private sealed class Setup : IDisposable
    {
        public Handler Handler { get; } = new();
        public Clock Clock { get; } = new();
        public HttpContextAccessor Accessor { get; } = new() { HttpContext = new DefaultHttpContext() };
        public RemoteSessionValidator Validator { get; }
        private readonly HttpClient client;
        public Setup()
        {
            Accessor.HttpContext!.Request.Headers.Authorization = "Bearer first-token";
            client = new HttpClient(Handler);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
                { ["Jwt:SessionValidationUrl"] = "https://user.test/api/v1/auth/session" }).Build();
            Validator = new RemoteSessionValidator(new Factory(client), config, Accessor, Clock);
        }
        public Task<bool> Validate() => Validator.ValidateAsync(Principal(), default);
        public void Dispose() { Validator.Dispose(); client.Dispose(); }
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Factory(HttpClient client) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => client; }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public bool Delay;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            if (Delay) await Task.Delay(30, ct);
            return new HttpResponseMessage(Status) { Content = JsonContent.Create(new
                { UserId = 1, Roles = new[] { "LotOwner" }, OwnerProfileId = 2, ParkingLotIds = new[] { 3 } }) };
        }
    }
}
