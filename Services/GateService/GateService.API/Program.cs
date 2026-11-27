using GateService.Application;
using GateService.API.Middleware;
using GateService.Infrastructure;
using GateService.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

// Ghi đè cấu hình máy local (RabbitMQ password, khóa nội bộ...) — file bị gitignore, xem UserService.API cùng pattern.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();

// ===== Composition Root: lắp ráp các layer của GateService =====
builder.AddServiceDefaults("GateService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
// US-066: JWT do UserService phat hanh (RS256) - chi Admin duoc sua cau hinh suc chua.
builder.Services.AddJwtAuth(Path.GetFullPath(
    builder.Configuration["Jwt:PublicKeyPath"] ?? "Keys/public.key", builder.Environment.ContentRootPath));
// Gỡ phân công / khóa tài khoản có hiệu lực ngay: hỏi UserService trạng thái phiên (Jwt:SessionValidationUrl, cache 10s).
builder.Services.AddRemoteSessionValidation();
// parking-sessions nhận thêm khóa nội bộ của SupportService.
builder.Services.AddSingleton<InternalApiKeyAuthenticator>();
builder.Services.AddGateApplication();                                  // Application: Use Case
builder.Services.AddGateInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository

builder.Services.AddHealthChecks().AddCheck("event-bus", () => builder.Configuration.GetValue("EventBus:Enabled", true)
    ? HealthCheckResult.Healthy()
    : HealthCheckResult.Degraded("EventBus tắt: ca không tự đóng khi bị gỡ / khóa (vẫn chặn được nhờ kiểm phiên) và không có cảnh báo."));

var app = builder.Build();

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<GateDbContext>();

app.UseServiceDefaults();
// US-066: CapacityValidationException -> 400 kem errors theo field; version cu -> 409 CONFIG_VERSION_CONFLICT.
app.UseMiddleware<LotCapacityErrorHandlingMiddleware>();
app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;