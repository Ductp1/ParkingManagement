using GateService.Application;
using GateService.Infrastructure;
using GateService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ===== Composition Root: lắp ráp các layer của GateService =====
builder.AddServiceDefaults("GateService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
builder.Services.AddGateApplication();                                  // Application: Use Case
builder.Services.AddGateInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository

var app = builder.Build();

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<GateDbContext>();

app.UseServiceDefaults();
app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;