using ParkingService.Application;
using ParkingService.Infrastructure;
using ParkingService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ===== Composition Root: lắp ráp các layer của ParkingService =====
builder.AddServiceDefaults("ParkingService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
// JWT do UserService phát hành (RS256): kiểm tra chữ ký bằng public key + policy RBAC dùng chung (T-102).
// Chỉ controller admin (US-055) gắn [Authorize]; endpoint của US-053/US-054 giữ nguyên, không yêu cầu token.
builder.Services.AddJwtAuth(Path.GetFullPath(
    builder.Configuration["Jwt:PublicKeyPath"] ?? "Keys/public.key", builder.Environment.ContentRootPath));
builder.Services.AddParkingApplication();                                 // Application: Use Case
builder.Services.AddParkingInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository

var app = builder.Build();

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<ParkingDbContext>();

app.UseServiceDefaults();
app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;