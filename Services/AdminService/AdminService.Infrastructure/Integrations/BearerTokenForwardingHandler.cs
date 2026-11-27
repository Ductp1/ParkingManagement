using Microsoft.AspNetCore.Http;

namespace AdminService.Infrastructure.Integrations;

/// <summary>
/// Chuyển tiếp header Authorization (Bearer) của request đang xử lý sang lời gọi UserService,
/// để UserService kiểm tra quyền Admin trên chính token của người thao tác. Không có header thì bỏ qua.
/// </summary>
public sealed class BearerTokenForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    private const string BearerPrefix = "Bearer ";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (request.Headers.Authorization is null
            && authorization is not null
            && authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            && authorization.Length > BearerPrefix.Length)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
