using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

namespace AdminService.API.Security;

/// <summary>
/// US-116 T1: form public "Đăng ký đối tác" không cần đăng nhập → giới hạn số hồ sơ mỗi IP trong 1 giờ
/// ("Security:PartnerApplicationsPerHour", mặc định 5; vượt → 429). Request đi qua Gateway (YARP chạy localhost) nên IP người gửi
/// lấy từ X-Forwarded-For (chỉ tin proxy loopback – mặc định của ForwardedHeadersOptions).
/// </summary>
public static class PartnerApplicationRateLimit
{
    public const string Policy = "partner-apply";
    public const string PerHourKey = "Security:PartnerApplicationsPerHour";
    public const int DefaultPerHour = 5;

    public static IServiceCollection AddPartnerApplicationRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var perHour = Math.Clamp(configuration.GetValue(PerHourKey, DefaultPerHour), 1, 1000);
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = perHour, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
        });
    }

    /// <summary>Gọi TRƯỚC UseServiceDefaults (MapControllers) để limiter đọc được metadata [EnableRateLimiting] của endpoint.</summary>
    public static WebApplication UsePartnerApplicationRateLimit(this WebApplication app)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        });
        app.UseRateLimiter();
        return app;
    }
}
