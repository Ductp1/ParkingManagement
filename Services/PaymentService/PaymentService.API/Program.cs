using PaymentService.Application;
using PaymentService.Infrastructure;
using PaymentService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Ghi đè cấu hình máy local (RabbitMQ password...) — file bị gitignore, xem UserService.API cùng pattern.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();

// ===== Composition Root: lắp ráp các layer của PaymentService =====
builder.AddServiceDefaults("PaymentService");                                   // Controller, OpenAPI, /health, CORS, xử lý lỗi
// JWT do UserService phát hành (RS256): kiểm tra chữ ký bằng public key + policy RBAC dùng chung (T-102).
// Chỉ controller báo cáo doanh thu chủ bãi (US-078) gắn [Authorize]; endpoint thanh toán/báo giá hiện có
// (VNPAY return/IPN, gate-settlement, pricing) giữ nguyên, không yêu cầu token.
builder.Services.AddJwtAuth(Path.GetFullPath(
    builder.Configuration["Jwt:PublicKeyPath"] ?? "Keys/public.key", builder.Environment.ContentRootPath));
builder.Services.AddPaymentApplication();                                  // Application: Use Case
builder.Services.AddPaymentInfrastructure(builder.Configuration);          // Infrastructure: DbContext riêng + Repository
builder.Services.AddPaymentCommission(builder.Configuration);              // US-079: hoa hồng nền tảng (job + báo cáo Admin)
builder.Services.AddPaymentSettlements(builder.Configuration);             // US-080: quyết toán định kỳ (job sinh kỳ + Admin)

var app = builder.Build();

// Tạo/cập nhật database riêng của service + nạp dữ liệu demo (bật/tắt trong appsettings → "Database").
await app.InitializeDatabaseAsync<PaymentDbContext>();

app.UseServiceDefaults();
app.Run();

/// <summary>Dùng cho WebApplicationFactory trong integration test.</summary>
public partial class Program;
