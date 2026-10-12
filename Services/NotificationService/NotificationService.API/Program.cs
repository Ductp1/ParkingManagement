using Microsoft.Extensions.DependencyInjection;
using NotificationService.API.Hubs;
using NotificationService.API.Services;
using NotificationService.Application;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Infrastructure;
using NotificationService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ===== Composition Root: lắp ráp các layer của NotificationService =====
builder.AddServiceDefaults("NotificationService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
builder.Services.AddNotificationApplication();                                  // Application: Use Case
builder.Services.AddNotificationInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository
builder.Services.AddSignalR();                                                  // SignalR for real-time notifications & parking lot updates

// IInAppNotificationBroadcaster: implementation SignalR – BẮT BUỘC đăng ký,
// nếu không resolve IInAppNotificationSender (Injected vào Dispatcher) sẽ nổ runtime.
builder.Services.AddScoped<IInAppNotificationBroadcaster, SignalRInAppNotificationBroadcaster>();

var app = builder.Build();

// ===== Fail-fast wiring check (feedback PR): toàn bộ pipeline notification phải resolve được
// NGAY LÚC KHỞI ĐỘNG. Nếu thiếu implementation (VD broadcaster SignalR của kênh in-app – route
// chính), service phải chết sớm với lỗi rõ ràng thay vì nổ runtime giữa chừng khi nhận request.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    _ = sp.GetRequiredService<INotificationDispatcher>();       // Infrastructure: dispatcher + 4 channel senders
    _ = sp.GetRequiredService<IInAppNotificationSender>();      // Infrastructure: in-app sender
    _ = sp.GetRequiredService<IInAppNotificationBroadcaster>(); // API: SignalRInAppNotificationBroadcaster
}

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