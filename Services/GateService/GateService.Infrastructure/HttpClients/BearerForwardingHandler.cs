using Microsoft.AspNetCore.Http;

namespace GateService.Infrastructure.HttpClients;

/// <summary>
/// Chuyển tiếp JWT của chủ bãi đang thao tác sang UserService. Request vào /internal/… mang khóa nội bộ của service gọi
/// nên không chuyển đi; job nền không có request thì bỏ qua.
/// </summary>
public sealed class BearerForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var incoming = httpContextAccessor.HttpContext?.Request;
        var authorization = incoming?.Headers.Authorization.ToString();
        if (request.Headers.Authorization is null
            && incoming?.Path.StartsWithSegments("/internal") == false
            && authorization?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
