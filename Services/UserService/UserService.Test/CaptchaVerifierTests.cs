using System.Net;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UserService.Application.Interfaces;
using UserService.Infrastructure.Authentication;

namespace UserService.Test;

public class CaptchaVerifierTests
{
    private static CaptchaOptions Settings => new()
    { SecretKey = "server-only-secret", AllowedHostnames = ["parking.test"], ExpectedAction = "login" };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Missing_token_does_not_call_provider(string? token)
    {
        var handler = new Handler("""{"success":true,"hostname":"parking.test","action":"login"}""");
        await Assert.ThrowsAsync<CaptchaException>(() => Create(handler).VerifyAsync(token));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Oversized_token_does_not_call_provider()
    {
        var handler = new Handler("{}");
        await Assert.ThrowsAsync<CaptchaException>(() => Create(handler).VerifyAsync(new string('a', 2049)));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Valid_token_is_submitted_to_siteverify()
    {
        var handler = new Handler("""{"success":true,"hostname":"parking.test","action":"login"}""");
        await Create(handler).VerifyAsync("captcha-value");
        Assert.Equal("https://challenges.cloudflare.com/turnstile/v0/siteverify", handler.Url);
        Assert.Contains("response=captcha-value", handler.Body);
        Assert.Contains("secret=server-only-secret", handler.Body);
    }

    [Theory]
    [InlineData("""{"success":false,"error-codes":["invalid-input-response"]}""")]
    [InlineData("""{"success":false,"error-codes":["timeout-or-duplicate"]}""")]
    [InlineData("""{"success":true,"hostname":"evil.test","action":"login"}""")]
    [InlineData("""{"success":true,"hostname":"parking.test","action":"register"}""")]
    [InlineData("""{"success":true}""")]
    public async Task Invalid_or_replayed_token_is_rejected(string json)
    {
        var ex = await Assert.ThrowsAsync<CaptchaException>(() => Create(new Handler(json)).VerifyAsync("token"));
        Assert.False(ex.Unavailable);
        Assert.Equal("CAPTCHA_INVALID", ex.Code);
    }

    [Theory]
    [InlineData("broken json")]
    [InlineData("null")]
    [InlineData("""{"success":false,"error-codes":["internal-error"]}""")]
    [InlineData("""{"success":false,"error-codes":["invalid-input-secret"]}""")]
    public async Task Provider_failure_is_unavailable(string json)
    {
        var ex = await Assert.ThrowsAsync<CaptchaException>(() => Create(new Handler(json)).VerifyAsync("token"));
        Assert.True(ex.Unavailable);
        Assert.Equal("CAPTCHA_UNAVAILABLE", ex.Code);
    }

    [Fact]
    public async Task Http_failure_is_unavailable()
    {
        var ex = await Assert.ThrowsAsync<CaptchaException>(() =>
            Create(new Handler("{}") { Status = HttpStatusCode.BadGateway }).VerifyAsync("token"));
        Assert.True(ex.Unavailable);
    }

    [Fact]
    public async Task Timeout_is_unavailable()
    {
        var ex = await Assert.ThrowsAsync<CaptchaException>(() =>
            Create(new Handler("{}") { Error = new TaskCanceledException() }).VerifyAsync("token"));
        Assert.True(ex.Unavailable);
    }

    [Fact]
    public async Task Caller_cancellation_is_preserved()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Create(new Handler("{}")).VerifyAsync("token", cts.Token));
    }

    [Theory]
    [InlineData(false, "secret")]
    [InlineData(true, "1x0000000000000000000000000000000AA")]
    [InlineData(true, "2x0000000000000000000000000000000AA")]
    [InlineData(true, "3x0000000000000000000000000000000AA")]
    public void Production_rejects_bypass_and_test_keys(bool enabled, string secret)
    {
        var result = new CaptchaEnvironmentValidator(new EnvironmentStub()).Validate(null,
            new CaptchaOptions { Enabled = enabled, SecretKey = secret });
        Assert.True(result.Failed);
    }

    [Fact]
    public void Production_rejects_test_action_even_with_real_key()
    {
        var result = new CaptchaEnvironmentValidator(new EnvironmentStub()).Validate(null,
            new CaptchaOptions { SecretKey = "real-secret", ExpectedAction = "test" });
        Assert.True(result.Failed);
    }

    [Fact]
    public void Development_allows_explicit_dummy_key_configuration()
    {
        var result = new CaptchaEnvironmentValidator(new EnvironmentStub { EnvironmentName = Environments.Development })
            .Validate(null, new CaptchaOptions { SecretKey = "1x0000000000000000000000000000000AA", ExpectedAction = "test" });
        Assert.True(result.Succeeded);
    }

    private static TurnstileCaptchaVerifier Create(Handler handler) =>
        new(new HttpClient(handler), Options.Create(Settings));

    private sealed class Handler(string json) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Body { get; private set; }
        public string? Url { get; private set; }
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public Exception? Error { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (Error is not null) throw Error;
            Body = await request.Content!.ReadAsStringAsync(ct);
            Url = request.RequestUri!.ToString();
            return new HttpResponseMessage(Status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        }
    }
    private sealed class EnvironmentStub : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
