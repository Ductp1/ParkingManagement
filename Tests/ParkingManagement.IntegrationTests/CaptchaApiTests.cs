using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UserService.Application.Interfaces;
using UserService.Infrastructure.Authentication;

namespace ParkingManagement.IntegrationTests;

public class CaptchaApiTests
{
    [Theory]
    [InlineData(null, 400, "CAPTCHA_INVALID")]
    [InlineData("bad", 400, "CAPTCHA_INVALID")]
    [InlineData("expired", 400, "CAPTCHA_INVALID")]
    [InlineData("wrong-host", 400, "CAPTCHA_INVALID")]
    [InlineData("wrong-action", 400, "CAPTCHA_INVALID")]
    [InlineData("unavailable", 503, "CAPTCHA_UNAVAILABLE")]
    public async Task Direct_login_request_cannot_bypass_captcha(string? token, int status, string code)
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { EmailOrPhone = "driver@example.com", Password = "Password123", CaptchaToken = token });
        Assert.Equal(status, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private sealed class Factory : WebApplicationFactory<UserService.API.Controllers.AuthController>
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "pm-captcha-" + Guid.NewGuid().ToString("N"));
        public Factory()
        {
            Directory.CreateDirectory(root);
            using var rsa = RSA.Create(2048);
            File.WriteAllText(Path.Combine(root, "public.key"), rsa.ExportSubjectPublicKeyInfoPem());
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["Database:SeedDemoData"] = "false",
                ["EventBus:Enabled"] = "false",
                ["Jwt:PublicKeyPath"] = Path.Combine(root, "public.key"),
                ["Captcha:Enabled"] = "true",
                ["Captcha:SecretKey"] = "unit-test-secret",
                ["Captcha:AllowedHostnames:0"] = "parking.test",
                ["Captcha:ExpectedAction"] = "login"
            }));
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService) &&
                    (d.ImplementationType == typeof(UserService.Infrastructure.Storage.IdentityRetentionWorker) ||
                     d.ImplementationType == typeof(ParkingManagement.ServiceDefaults.Messaging.OutboxDispatcher<UserService.Infrastructure.Persistence.UserDbContext>))).ToArray())
                    services.Remove(descriptor);
                services.AddScoped<UserService.Application.Features.Auth.IAuthRepository, TrapRepository>();
                services.AddHttpClient<ICaptchaVerifier, TurnstileCaptchaVerifier>()
                    .ConfigurePrimaryHttpMessageHandler(() => new Provider());
            });
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    private sealed class TrapRepository : UserService.Application.Features.Auth.IAuthRepository
    {
        public Task<UserService.Domain.Entities.User?> GetUserByEmailOrPhoneAsync(string identifier, CancellationToken ct)
            => throw new InvalidOperationException("CAPTCHA must reject before repository access.");
        public Task UpdateUserAsync(UserService.Domain.Entities.User user, CancellationToken ct)
            => throw new InvalidOperationException("CAPTCHA must reject before repository access.");
    }
    private sealed class Provider : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            var json = body.Contains("response=wrong-host")
                ? """{"success":true,"hostname":"evil.test","action":"login"}"""
                : body.Contains("response=wrong-action")
                ? """{"success":true,"hostname":"parking.test","action":"register"}"""
                : body.Contains("response=unavailable")
                ? """{"success":false,"error-codes":["internal-error"]}"""
                : """{"success":false,"error-codes":["timeout-or-duplicate"]}""";
            return new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
