using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UserService.Infrastructure.Persistence;

namespace ParkingManagement.IntegrationTests;

// Chỉ bật với DB test riêng; không chạm pm_user đang dùng.
public sealed class Tv1PostgresFactAttribute : FactAttribute
{
    public Tv1PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TV1_TEST_DB")))
            Skip="Cần TV1_TEST_DB trỏ đến PostgreSQL test riêng.";
    }
}

[CollectionDefinition("TV1 HTTP",DisableParallelization=true)]
public sealed class Tv1Collection : ICollectionFixture<Tv1ApiFixture>;

[Collection("TV1 HTTP")]
public sealed class UserServiceApiTests(Tv1ApiFixture fixture)
{
    [Tv1PostgresFact]
    public async Task Registration_login_rotation_and_logout_revoke_access_session()
    {
        var session=await fixture.RegisterAsync();
        using var client=fixture.Client();client.DefaultRequestHeaders.Authorization=new("Bearer",session.AccessToken);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/v1/users/me")).StatusCode);
        var response=await client.PostAsJsonAsync("/api/v1/auth/refresh",new{session.RefreshToken});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var replacement=(await response.Content.ReadFromJsonAsync<Tokens>())!;
        Assert.NotEqual(session.RefreshToken,replacement.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/users/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization=new("Bearer",replacement.AccessToken);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/v1/auth/logout",new{replacement.RefreshToken})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/v1/auth/refresh",new{replacement.RefreshToken})).StatusCode);
    }

    [Tv1PostgresFact]
    public async Task Third_wrong_otp_persists_lock_and_security_events()
    {
        using var client=fixture.Client();var contact=fixture.Contact();
        Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsJsonAsync("/api/v1/auth/register",new{FullName="Test",Contact=contact,Password="Password123"})).StatusCode);
        var code=await fixture.CodeAsync(contact,"Register");var wrong=code=="000000"?"999999":"000000";
        for(var i=0;i<3;i++)Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/v1/auth/otp/verify",new{Contact=contact,Code=wrong})).StatusCode);
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<UserDbContext>();
        var user=await db.Users.SingleAsync(u=>u.Email==contact);
        Assert.True(user.LockedUntilUtc>DateTime.UtcNow.AddMinutes(14));
        Assert.Equal(3,await db.SecurityEvents.CountAsync(e=>e.UserId==user.Id&&e.EventType==ParkingManagement.SharedKernel.Enums.SecurityEventType.OtpFailed));
    }

    [Tv1PostgresFact]
    public async Task Concurrent_refresh_has_one_winner_and_replay_revokes_the_chain()
    {
        var session=await fixture.RegisterAsync();using var client=fixture.Client();
        var responses=await Task.WhenAll(client.PostAsJsonAsync("/api/v1/auth/refresh",new{session.RefreshToken}),
            client.PostAsJsonAsync("/api/v1/auth/refresh",new{session.RefreshToken}));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Unauthorized);
        var winner=(await responses.Single(r=>r.StatusCode==HttpStatusCode.OK).Content.ReadFromJsonAsync<Tokens>())!;
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/v1/auth/refresh",new{winner.RefreshToken})).StatusCode);
    }

    public sealed record Tokens(string AccessToken,string RefreshToken);

    [Tv1PostgresFact]
    public async Task Reset_password_revokes_old_access_and_refresh_and_rejects_replay()
    {
        var session = await fixture.RegisterAsync();
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new { session.Contact })).StatusCode);
        var code = await fixture.CodeAsync(session.Contact, "ResetPassword");
        var reset = new { session.Contact, Code = code, NewPassword = "NewPassword123" };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/password/reset", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/password/reset", reset)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { session.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { EmailOrPhone = session.Contact, Password = "Password123", CaptchaToken = "integration-valid" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { EmailOrPhone = session.Contact, Password = "NewPassword123", CaptchaToken = "integration-valid" })).StatusCode);
    }

    [Tv1PostgresFact]
    public async Task Update_me_ignores_privileged_fields()
    {
        var session = await fixture.RegisterAsync();
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/v1/users/me", new { FullName = "Anonymous" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/v1/users/me", new { FullName = "Updated", AvatarUrl = "https://example.com/a.png", Roles = new[] { "Admin" }, Email = "attacker@example.com", Status = "Banned" })).StatusCode);
        using var profile = JsonDocument.Parse(await client.GetStringAsync("/api/v1/users/me"));
        Assert.Equal("Updated", profile.RootElement.GetProperty("fullName").GetString());
        Assert.Equal(session.Contact, profile.RootElement.GetProperty("email").GetString());
        Assert.Equal("Driver", profile.RootElement.GetProperty("roles")[0].GetString());
        Assert.Equal(JsonValueKind.Null, profile.RootElement.GetProperty("avatarUrl").ValueKind);
    }

    [Tv1PostgresFact]
    public async Task Avatar_upload_serves_normalized_image_replaces_and_removes_owned_files()
    {
        var session = await fixture.RegisterAsync();
        using var client = fixture.Client();
        using var image = new SkiaSharp.SKBitmap(800, 400);
        using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var bytes = encoded.ToArray();
        MultipartFormDataContent Form(byte[] content)
        {
            var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(content), "file", "../../untrusted.png");
            return form;
        }
        using (var form = Form(bytes))
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/users/me/avatar", form)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        using (var invalid = Form("not an image"u8.ToArray()))
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/users/me/avatar", invalid)).StatusCode);
        string? first = null;
        string? second = null;
        for (var i = 0; i < 2; i++)
        {
            using var form = Form(bytes);
            var uploaded = await client.PostAsync("/api/v1/users/me/avatar", form);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
            using var result = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync());
            var url = result.RootElement.GetProperty("avatarUrl").GetString()!;
            if (i == 0) first = url; else second = url;
            using var anonymous = fixture.Client();
            var response = await anonymous.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
            using var normalized = SkiaSharp.SKBitmap.Decode(await response.Content.ReadAsByteArrayAsync());
            Assert.Equal(512, normalized.Width); Assert.Equal(256, normalized.Height);
        }
        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(first)).StatusCode);
        // Updating name must never clear or overwrite an uploaded avatar.
        await client.PutAsJsonAsync("/api/v1/users/me", new { FullName = "Avatar user", AvatarUrl = "https://attacker.test/a.svg" });
        using var me = JsonDocument.Parse(await client.GetStringAsync("/api/v1/users/me"));
        Assert.Equal(second, me.RootElement.GetProperty("avatarUrl").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/users/me/avatar")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(second)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/users/me/avatar")).StatusCode);
    }


}

public sealed class Tv1ApiFixture : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"pm-tv1-api-"+Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string,string?> original=[];
    private readonly DeliveryHandler delivery=new();
    private readonly ApiFactory factory;
    public IServiceProvider Services=>factory.Services;
    public Tv1ApiFixture()
    {
        Directory.CreateDirectory(root);using var rsa=RSA.Create(2048);
        var privatePath=Path.Combine(root,"private.key");var publicPath=Path.Combine(root,"public.key");
        File.WriteAllText(privatePath,rsa.ExportPkcs8PrivateKeyPem());File.WriteAllText(publicPath,rsa.ExportSubjectPublicKeyInfoPem());
        Set("Jwt__PublicKeyPath",publicPath);Set("Jwt__PrivateKeyPath",privatePath);
        Set("Security__EncryptionKey",Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        Set("Security__PrivateFilesPath",Path.Combine(root,"files"));Set("Database__SeedDemoData","false");
        Set("Security__AvatarPath", Path.Combine(root, "avatars"));
        Set("Security__AuthRequestsPerMinute","200");
        Set("Captcha__SecretKey","integration-test-secret");
        Set("Database__ApplyMigrationsOnStartup","true");Set("EventBus__Enabled","true");
        Set("Otp__Transport","Http");Set("Otp__DeliveryUrl","https://otp.test/delivery");
        if(Environment.GetEnvironmentVariable("TV1_TEST_DB") is {Length:>0} db)Set("ConnectionStrings__ServiceDb",db);
        factory=new ApiFactory(delivery);
    }
    private void Set(string name,string value){original[name]=Environment.GetEnvironmentVariable(name);Environment.SetEnvironmentVariable(name,value);}
    public HttpClient Client()=>factory.CreateClient();
    public string Contact()=>"tv1-"+Guid.NewGuid().ToString("N")+"@example.com";
    public async Task<(string Contact,string AccessToken,string RefreshToken)> RegisterAsync()
    {
        using var client=Client();var contact=Contact();
        Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsJsonAsync("/api/v1/auth/register",new{FullName="Test",Contact=contact,Password="Password123"})).StatusCode);
        var code=await CodeAsync(contact,"Register");
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/v1/auth/otp/verify",new{Contact=contact,Code=code})).StatusCode);
        var response=await client.PostAsJsonAsync("/api/v1/auth/login",new{EmailOrPhone=contact,Password="Password123",CaptchaToken="integration-valid"});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var tokens=(await response.Content.ReadFromJsonAsync<UserServiceApiTests.Tokens>())!;
        return(contact,tokens.AccessToken,tokens.RefreshToken);
    }
    public async Task<string> CodeAsync(string contact,string purpose)
    {
        for(var i=0;i<30;i++){if(delivery.Codes.TryGetValue(contact+":"+purpose,out var code))return code;await Task.Delay(1000);}
        throw new TimeoutException("Outbox chưa giao OTP cho test adapter.");
    }
    public void Dispose(){factory.Dispose();foreach(var item in original)Environment.SetEnvironmentVariable(item.Key,item.Value);if(Directory.Exists(root))Directory.Delete(root,true);}
    private sealed class ApiFactory(DeliveryHandler handler):WebApplicationFactory<UserService.API.Controllers.AuthController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)=>builder.ConfigureLogging(logging=>logging.ClearProviders().AddConsole()).ConfigureServices(services=>
        {
            services.AddScoped<UserService.Application.Interfaces.ICaptchaVerifier, TestCaptcha>();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddHttpClient("IdentityIntegrations").ConfigurePrimaryHttpMessageHandler(()=>handler);
            services.AddSingleton<ParkingManagement.ServiceDefaults.Messaging.IRabbitMqPublisher, TestEventPublisher>();
            MassTransit.DependencyInjectionRegistrationExtensions.RemoveMassTransitHostedService(services);
        });
    }
    private sealed class TestCaptcha : UserService.Application.Interfaces.ICaptchaVerifier
    {
        public Task VerifyAsync(string? token, CancellationToken ct = default)
        {
            if (token != "integration-valid") throw new UserService.Application.Interfaces.CaptchaException(false);
            return Task.CompletedTask;
        }
    }
    private sealed class TestEventPublisher : ParkingManagement.ServiceDefaults.Messaging.IRabbitMqPublisher
    {
        public Task PublishAsync(ParkingManagement.ServiceDefaults.Messaging.IntegrationEventEnvelope envelope, CancellationToken ct)
            => Task.CompletedTask;
    }
    private sealed class DeliveryHandler:HttpMessageHandler
    {
        public ConcurrentDictionary<string,string> Codes {get;}=new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            if(request.RequestUri!.Host=="otp.test")
            {
                using var data=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));var json=data.RootElement;
                Codes[json.GetProperty("destination").GetString()+":"+json.GetProperty("purpose").GetString()]=json.GetProperty("code").GetString()!;
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
