// API Gateway – cổng vào duy nhất cho Frontend: http://localhost:5000
// Định tuyến theo đường dẫn (YARP Reverse Proxy), cấu hình trong appsettings.json → "ReverseProxy".
//   /api/v1/users/**            → UserService         :5101
//   /api/v1/vehicles/**         → VehicleService      :5102
//   /api/v1/parking-lots/**     → ParkingService      :5103
//   /api/v1/bookings/**         → BookingService      :5104
//   /api/v1/pricing/**, /api/v1/payments/** → PaymentService :5105
//   /api/v1/notifications/**    → NotificationService :5106
//   /api/v1/parking-sessions/** → GateService         :5107
//   /api/v1/admin/**            → AdminService        :5108
//   /api/v1/complaints/**, /api/v1/reviews/** → SupportService :5109

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
var jwtEnabled = builder.Configuration["Jwt:PublicKeyPath"] is { Length: > 0 };
if (jwtEnabled)
{
    ParkingManagement.ServiceDefaults.AuthExtensions.AddJwtAuth(builder.Services,
        Path.GetFullPath(builder.Configuration["Jwt:PublicKeyPath"]!, builder.Environment.ContentRootPath));
    ParkingManagement.Gateway.Authentication.RemoteSessionValidationExtensions.AddRemoteSessionValidation(builder.Services);
}

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHttpClient();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

app.UseCors();
if (jwtEnabled) { app.UseAuthentication(); app.UseAuthorization(); }
app.MapReverseProxy();

app.MapGet("/", () => Results.Ok(new { service = "ParkingManagement.Gateway", status = "running", health = "/health/services" }));

// GET /health/services – gọi /health của từng service, cho biết service nào đang chạy.
app.MapGet("/health/services", async (IConfiguration config, IHttpClientFactory httpFactory) =>
{
    var http = httpFactory.CreateClient();
    http.Timeout = TimeSpan.FromSeconds(3);
    var clusters = config.GetSection("ReverseProxy:Clusters").GetChildren();

    var checks = await Task.WhenAll(clusters.Select(async c =>
    {
        var address = c["Destinations:primary:Address"]!;
        try
        {
            using var res = await http.GetAsync(new Uri(new Uri(address), "/health"));
            return new { service = c.Key, address, status = res.IsSuccessStatusCode ? "Healthy" : $"HTTP {(int)res.StatusCode}" };
        }
        catch (Exception)
        {
            return new { service = c.Key, address, status = "Down" };
        }
    }));

    return Results.Ok(checks);
});

app.Run();
