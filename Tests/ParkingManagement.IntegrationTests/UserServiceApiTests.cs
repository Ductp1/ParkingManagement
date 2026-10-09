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
        Set("Security__AuthRequestsPerMinute","200");
        Set("Database__ApplyMigrationsOnStartup","true");Set("EventBus__Enabled","true");
        Set("Otp__Transport","Http");Set("Otp__DeliveryUrl","https://otp.test/delivery");Set("EventBus__PublishUrl","https://events.test/publish");
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
        var response=await client.PostAsJsonAsync("/api/v1/auth/login",new{EmailOrPhone=contact,Password="Password123"});
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
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddHttpClient("IdentityIntegrations").ConfigurePrimaryHttpMessageHandler(()=>handler);
        });
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
