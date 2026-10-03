using VehicleService.Application;
using VehicleService.Infrastructure;
using VehicleService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ===== Composition Root: lắp ráp các layer của VehicleService =====
builder.AddServiceDefaults("VehicleService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
builder.Services.AddVehicleApplication();                                  // Application: Use Case
builder.Services.AddVehicleInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository

var app = builder.Build();

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<VehicleDbContext>();

app.UseServiceDefaults();
app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;