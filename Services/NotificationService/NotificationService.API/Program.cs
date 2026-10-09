using NotificationService.API.Hubs;
using NotificationService.Application;
using NotificationService.Infrastructure;
using NotificationService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ===== Composition Root: lắp ráp các layer của NotificationService =====
builder.AddServiceDefaults("NotificationService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
builder.Services.AddNotificationApplication();                                  // Application: Use Case
builder.Services.AddNotificationInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository
builder.Services.AddSignalR();                                                  // SignalR for real-time notifications & parking lot updates

var app = builder.Build();

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<NotificationDbContext>();

app.UseServiceDefaults();

// Map SignalR hubs (S1-T601)
// /hubs/notify: In-app notification push
// /hubs/parking: Real-time slot state & capacity updates
app.MapHub<NotificationHub>("/hubs/notify");
app.MapHub<ParkingHub>("/hubs/parking");

app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;