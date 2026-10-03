using BookingService.Application;
using BookingService.Infrastructure;
using BookingService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ===== Composition Root: lắp ráp các layer của BookingService =====
builder.AddServiceDefaults("BookingService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
builder.Services.AddBookingApplication();                                  // Application: Use Case
builder.Services.AddBookingInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository

var app = builder.Build();

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<BookingDbContext>();

app.UseServiceDefaults();
app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;