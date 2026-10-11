using AdminService.Application;
using AdminService.Infrastructure;
using AdminService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ===== Composition Root: lắp ráp các layer của AdminService =====
builder.AddServiceDefaults("AdminService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
// JWT do UserService phát hành (RS256): kiểm tra chữ ký bằng public key + policy RBAC dùng chung (T-102).
builder.Services.AddJwtAuth(Path.GetFullPath(
    builder.Configuration["Jwt:PublicKeyPath"] ?? "Keys/public.key", builder.Environment.ContentRootPath));
builder.Services.AddAdminApplication();                                  // Application: Use Case
builder.Services.AddAdminInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository

var app = builder.Build();

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<AdminDbContext>();

app.UseServiceDefaults();
app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;