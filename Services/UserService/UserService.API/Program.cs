using UserService.Application;
using UserService.Infrastructure;
using UserService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ===== Composition Root: lắp ráp các layer của UserService =====
builder.AddServiceDefaults("UserService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
builder.Services.AddUserApplication();                                  // Application: Use Case
builder.Services.AddUserInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository

var app = builder.Build();

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<UserDbContext>();

app.UseServiceDefaults();
app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;