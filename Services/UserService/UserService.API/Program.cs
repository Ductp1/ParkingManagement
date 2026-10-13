using UserService.Application;
using UserService.Infrastructure;
using UserService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();

// ===== Composition Root: lắp ráp các layer của UserService =====
builder.AddServiceDefaults("UserService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
builder.Services.AddJwtAuth(Path.GetFullPath(
    builder.Configuration["Jwt:PublicKeyPath"] ?? "Keys/public.key", builder.Environment.ContentRootPath));
builder.Services.AddUserApplication();                                  // Application: Use Case
builder.Services.AddUserInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Clamp(builder.Configuration.GetValue("Security:AuthRequestsPerMinute",20),1,1000),
            Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
app.UseForwardedHeaders(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{ ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto });

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<UserDbContext>();

app.UseRouting();
app.UseRateLimiter();
app.UseServiceDefaults();
app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;
