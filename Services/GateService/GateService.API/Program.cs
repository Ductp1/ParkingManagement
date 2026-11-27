using GateService.Application;
using GateService.API.Middleware;
using GateService.Infrastructure;
using GateService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ===== Composition Root: lắp ráp các layer của GateService =====
builder.AddServiceDefaults("GateService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
// US-066: JWT do UserService phat hanh (RS256) - chi Admin duoc sua cau hinh suc chua.
builder.Services.AddJwtAuth(Path.GetFullPath(
    builder.Configuration["Jwt:PublicKeyPath"] ?? "Keys/public.key", builder.Environment.ContentRootPath));
builder.Services.AddGateApplication();                                  // Application: Use Case
builder.Services.AddGateInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository

var app = builder.Build();

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<GateDbContext>();

app.UseServiceDefaults();
// US-066: CapacityValidationException -> 400 kem errors theo field; version cu -> 409 CONFIG_VERSION_CONFLICT.
app.UseMiddleware<LotCapacityErrorHandlingMiddleware>();
app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;