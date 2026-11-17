using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AdminService.API.Security;

/// <summary>
/// US-053: chống spam form public đăng ký đối tác. Mỗi địa chỉ client tối đa 5 lần gửi / 10 phút, quá thì 429.
/// Request đi qua Gateway (YARP đặt X-Forwarded-For) nên ưu tiên IP đầu tiên trong header đó, không có thì dùng IP kết nối.
/// </summary>
public static class PartnerRegistrationRateLimit
{
    public const string PolicyName = "partner-registration";
    public const int PermitLimit = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public static IServiceCollection AddPartnerRegistrationRateLimit(this IServiceCollection services)
        => services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    title = "Gửi quá nhiều lần",
                    status = StatusCodes.Status429TooManyRequests,
                    detail = "Bạn đã gửi hồ sơ quá nhiều lần. Vui lòng thử lại sau 10 phút.",
                }, cancellationToken);
            };
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(ClientKey(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = PermitLimit, Window = Window, QueueLimit = 0 }));
        });

    private static string ClientKey(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        var first = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return first ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
