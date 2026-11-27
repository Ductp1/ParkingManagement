using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BookingService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace ParkingManagement.IntegrationTests;

/// <summary>
/// Dịch vụ hạ tầng chung cho integration test: tự cấp database PostgreSQL riêng (pm_it_*),
/// đọc cấu hình cùng nguồn với run-all.ps1 (ConnectionStrings__ServiceDb → .env/DB_* → mặc định appsettings)
/// và bảo đảm QRTOKEN_SECRET tồn tại cho hợp đồng Booking ↔ Gate (T-704).
/// </summary>
public static class TestDatabaseProvisioner
{
    public const string DefaultServiceDb = "Host=localhost;Port=5432;Database=pm_booking;Username=postgres;Password=postgres";
    private static bool _qrSecretEnsured;

    /// <summary>Connection gốc: CI đặt ConnectionStrings__ServiceDb; local dùng .env/DB_* hoặc mặc định như appsettings.</summary>
    public static string ResolveBaseConnectionString()
    {
        LoadDotEnvAtRepoRoot();
        EnsureQrTokenSecret();
        return Environment.GetEnvironmentVariable("ConnectionStrings__ServiceDb") is { Length: > 0 } fromEnv
            ? fromEnv
            : $"Host={GetEnv("DB_HOST", "localhost")};Port={GetEnv("DB_PORT", "5432")};Database=pm_booking;" +
              $"Username={GetEnv("DB_USER", "postgres")};Password={GetEnv("DB_PASSWORD", "postgres")}";
    }

    public static async Task CreateDatabaseAsync(string baseConnectionString, string databaseName)
        => await ExecuteAdminSqlAsync(baseConnectionString, $"CREATE DATABASE \"{databaseName}\"");

    public static async Task DropDatabaseAsync(string baseConnectionString, string databaseName)
        => await ExecuteAdminSqlAsync(baseConnectionString, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");

    /// <summary>Chỉ thay mệnh đề Database=... — giữ nguyên server/port/user/password.</summary>
    public static string ReplaceDatabase(string connectionString, string databaseName)
        => Regex.Replace(connectionString, @"(?i)(database\s*=)\s*[^;]*", $"$1{databaseName}");

    /// <summary>Chạy 1 lệnh DDL trên database maintenance 'postgres' (CREATE/DROP DATABASE không gọi được qua migration).</summary>
    private static async Task ExecuteAdminSqlAsync(string baseConnectionString, string sql)
    {
        var adminCs = ReplaceDatabase(baseConnectionString, "postgres");
        var options = new DbContextOptionsBuilder<BookingDbContext>().UseNpgsql(adminCs).Options;
        await using var db = new BookingDbContext(options); // chỉ mượn provider Npgsql của service, không đụng bảng nghiệp vụ nào
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    private static string GetEnv(string name, string fallback)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    /// <summary>Nạp .env ở gốc repo (cùng cách run-all.ps1 làm) nhưng KHÔNG ghi đè biến môi trường đã có.</summary>
    private static void LoadDotEnvAtRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ParkingManagement.slnx")))
            dir = dir.Parent;
        if (dir?.FullName is null) return;
        var envPath = Path.Combine(dir.FullName, ".env");
        if (!File.Exists(envPath)) return;

        foreach (var line in File.ReadAllLines(envPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>QrTokenService (Booking) và BookingQrTokenValidator (Gate) đều fail fast khi thiếu secret → cấp sẵn cho test.</summary>
    private static void EnsureQrTokenSecret()
    {
        if (_qrSecretEnsured) return;
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("QRTOKEN_SECRET")))
            Environment.SetEnvironmentVariable("QRTOKEN_SECRET", "it-qrtoken-secret-" + Guid.NewGuid().ToString("N")[..8]);
        _qrSecretEnsured = true;
    }
}

/// <summary>
/// Cặp khóa RSA 2048 cho JWT test (giống cơ chế UserService phát hành, AdminService xác thực).
/// Ghi PEM vào thư mục temp riêng của fixture, tự dọn khi Dispose.
/// </summary>
public sealed class RsaTestKeys : IDisposable
{
    private readonly string _directory;

    public string PublicKeyPath { get; }
    public string PrivateKeyPath { get; }

    private RsaTestKeys(string directory, string publicKeyPath, string privateKeyPath)
    {
        _directory = directory;
        PublicKeyPath = publicKeyPath;
        PrivateKeyPath = privateKeyPath;
    }

    public static RsaTestKeys Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pm-it-keys-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(directory);
        using var rsa = RSA.Create(2048);
        var publicKeyPath = Path.Combine(directory, "public.pem");
        var privateKeyPath = Path.Combine(directory, "private.pem");
        File.WriteAllText(publicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());
        File.WriteAllText(privateKeyPath, rsa.ExportPkcs8PrivateKeyPem());
        return new RsaTestKeys(directory, publicKeyPath, privateKeyPath);
    }

    /// <summary>
    /// Mint JWT RS256 đúng hợp đồng UserService: sub/email/jti + "sid" (hash phiên) + "role".
    /// Issuer/Audience khớp cứng với AuthExtensions: ParkingManagement / ParkingManagement.Clients.
    /// </summary>
    public string MintToken(int userId, string email, string? sid = null, params string[] roles)
        => MintToken(userId, email, sid, [], roles);

    /// <summary>Như trên, kèm claim bổ sung (VD "OwnerProfileId" cho chủ bãi – giống token UserService phát hành).</summary>
    public string MintToken(int userId, string email, string? sid, IEnumerable<Claim> extraClaims, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (sid is not null) claims.Add(new Claim("sid", sid));
        foreach (var role in roles) claims.Add(new Claim("role", role));
        claims.AddRange(extraClaims);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(PrivateKeyPath));
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddHours(24),
            Issuer = "ParkingManagement",
            Audience = "ParkingManagement.Clients",
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(rsa) { CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } },
                SecurityAlgorithms.RsaSha256),
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
        catch { // best-effort
        }
    }
}

/// <summary>
/// Trỏ DbContext của host test sang database pm_it_*: gỡ đăng ký options gốc (từ Program.cs qua appsettings/env)
/// rồi đăng ký lại với connection string của test. Chạy trong ConfigureServices của factory nên luôn thắng,
/// KHÔNG đụng biến môi trường toàn cục (an toàn khi nhiều fixture chạy song song).
/// </summary>
public static class IsolatedDatabaseExtension
{
    public static IServiceCollection UseIsolatedDatabase<TContext>(this IServiceCollection services, string connectionString)
        where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<TContext>>();
        services.AddDbContext<TContext>(options => options.UseNpgsql(connectionString));
        return services;
    }
}

/// <summary>
/// Fixture chung cho 1 service: 1 host thật (WebApplicationFactory) + 1 database pm_it_* riêng.
/// Database được nối vào host bằng cách thay đăng ký DbContext (xem <see cref="IsolatedDatabaseExtension"/>),
/// nên không phụ thuộc thứ tự ưu tiên cấu hình và không đụng biến môi trường dùng chung.
/// </summary>
public class ServiceHostFixture<TEntryPoint, TContext> : IAsyncLifetime
    where TEntryPoint : class
    where TContext : DbContext
{
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];

    public string DbName { get; }
    public WebApplicationFactory<TEntryPoint> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => Factory.Services;

    /// <summary>Hook đăng ký thêm service vào host (VD: DataProtection ephemeral cho UserService).</summary>
    public Action<IServiceCollection>? ExtraServices { get; set; }

    public ServiceHostFixture()
    {
        var slug = typeof(TContext).Name.Replace("DbContext", "").ToLowerInvariant();
        DbName = $"pm_it_{slug}_{_suffix}";
    }

    public virtual async Task InitializeAsync()
    {
        var baseCs = TestDatabaseProvisioner.ResolveBaseConnectionString();
        var dbConnectionString = TestDatabaseProvisioner.ReplaceDatabase(baseCs, DbName);
        await TestDatabaseProvisioner.CreateDatabaseAsync(baseCs, DbName);

        Factory = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.UseIsolatedDatabase<TContext>(dbConnectionString);
                ExtraServices?.Invoke(services);
            });
        });
        Client = Factory.CreateClient();

        using var health = await Client.GetAsync("/health");
        Assert.True(health.IsSuccessStatusCode,
            $"{typeof(TEntryPoint).Assembly.GetName().Name} không start được trên database {DbName}.");
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        try { await TestDatabaseProvisioner.DropDatabaseAsync(TestDatabaseProvisioner.ResolveBaseConnectionString(), DbName); }
        catch { // dọn dẹp best-effort: PostgreSQL tắt sớm không được làm đỏ kết quả test
        }
    }
}

/// <summary>Làm tròn về đầu phút để so khớp thời gian xuyên suốt JSON → PostgreSQL (timestamptz Microsecond).</summary>
public static class TestTime
{
    public static DateTime RoundToMinute(DateTime utc)
        => new(utc.Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
}

/// <summary>
/// Assert chung cho response lỗi của ExceptionHandlingMiddleware (ServiceDefaults).
/// Middleware viết ProblemDetails bằng WriteAsJsonAsync nên content-type là "application/json" (body vẫn RFC 7807).
/// </summary>
public static class Asserts
{
    public static async Task BadRequestAsync(HttpResponseMessage response, string detailFragment)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(detailFragment, (await response.Content.ReadFromJsonAsync<ProblemJson>())!.Detail);
    }

    public static async Task ConflictAsync(HttpResponseMessage response, string detailFragment)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(detailFragment, (await response.Content.ReadFromJsonAsync<ProblemJson>())!.Detail);
    }

    public static async Task NotFoundAsync(HttpResponseMessage response)
        => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}