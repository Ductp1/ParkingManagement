using SupportService.Application;
using SupportService.Infrastructure;
using SupportService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ===== Composition Root: lắp ráp các layer của SupportService =====
builder.AddServiceDefaults("SupportService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
// JWT do UserService phát hành (RS256): kiểm tra chữ ký bằng public key + policy RBAC dùng chung (T-102).
// API nào cần đăng nhập thì gắn [Authorize]; userId lấy từ claim "sub" qua ICurrentUser thay vì client tự gửi.
builder.Services.AddJwtAuth(Path.GetFullPath(
    builder.Configuration["Jwt:PublicKeyPath"] ?? "Keys/public.key", builder.Environment.ContentRootPath));
builder.Services.AddSupportApplication();                                  // Application: Use Case
builder.Services.AddSupportInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository

var app = builder.Build();

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<SupportDbContext>();

app.UseServiceDefaults();
app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;