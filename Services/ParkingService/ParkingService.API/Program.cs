using ParkingService.Application;
using ParkingService.Infrastructure;
using ParkingService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ===== Composition Root: lắp ráp các layer của ParkingService =====
builder.AddServiceDefaults("ParkingService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
builder.Services.AddParkingApplication();                                  // Application: Use Case
builder.Services.AddParkingInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository

var app = builder.Build();

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<ParkingDbContext>();

app.UseServiceDefaults();
app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;