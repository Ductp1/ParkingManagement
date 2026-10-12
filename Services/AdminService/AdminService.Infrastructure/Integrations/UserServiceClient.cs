using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdminService.Application.Features.Users;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Integrations;

/// <summary>
/// Cài đặt port IUserServiceClient bằng HttpClient. BaseAddress = Gateway (cấu hình "Services:GatewayBaseUrl"),
/// đường dẫn là API công khai của UserService – AdminService không tham chiếu project hay database của UserService.
/// </summary>
public sealed class UserServiceClient(HttpClient http, ILogger<UserServiceClient> logger) : IUserServiceClient
{
    // Thông điệp trả cho client giữ cố định; chi tiết kỹ thuật (mã HTTP, đường dẫn, exception gốc) chỉ ghi log.
    private const string UnavailableMessage = "UserService hiện không phản hồi, vui lòng thử lại sau.";
    private const string UnauthorizedMessage = "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.";
    private const string ForbiddenMessage = "Tài khoản không có quyền thực hiện thao tác này.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PagedResult<AdminUserSummaryDto>> ListUsersAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken)
    {
        var path = $"api/v1/users?page={page}&pageSize={pageSize}";
        if (role is { } r) path += $"&role={r}";

        return await GetAsync<PagedResult<AdminUserSummaryDto>>(path, cancellationToken)
            ?? throw Unavailable($"UserService trả HTTP 404 khi gọi GET {path}.");
    }

    public Task<AdminUserDto?> FindUserByIdAsync(int userId, CancellationToken cancellationToken)
        => SendAsync<AdminUserDto>(HttpMethod.Get, $"api/v1/users/{userId}", body: null, NotFoundMeans.ResourceMissing, cancellationToken);

    public Task<OwnerAccountDto?> LockOwnerAsync(int ownerProfileId, string reason, DateTime? lockedUntilUtc, int performedByUserId, CancellationToken cancellationToken)
        => SendAsync<OwnerAccountDto>(HttpMethod.Post, $"{OwnerPath(ownerProfileId)}/lock",
            new { reason, lockedUntilUtc, performedByUserId }, NotFoundMeans.ResourceMissingOnlyWithProblemBody, cancellationToken);

    public Task<OwnerAccountDto?> UnlockOwnerAsync(int ownerProfileId, string reason, int performedByUserId, CancellationToken cancellationToken)
        => SendAsync<OwnerAccountDto>(HttpMethod.Post, $"{OwnerPath(ownerProfileId)}/unlock",
            new { reason, performedByUserId }, NotFoundMeans.ResourceMissingOnlyWithProblemBody, cancellationToken);

    private static string OwnerPath(int ownerProfileId) => $"api/v1/users/owner-profiles/{ownerProfileId}";

    private Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken) where T : class
        => SendAsync<T>(HttpMethod.Get, path, body: null, NotFoundMeans.ResourceMissing, cancellationToken);

    /// <summary>
    /// Gửi request và đọc JSON. 404 "không có tài nguyên" → null; UserService từ chối token → 401 / 403;
    /// mọi lỗi khác (mất kết nối, timeout, mã ngoài dự kiến, nội dung sai) → 503.
    /// </summary>
    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, NotFoundMeans notFoundMeans,
        CancellationToken cancellationToken) where T : class
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);

            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound
                && (notFoundMeans == NotFoundMeans.ResourceMissing || HasJsonBody(response)))
                return null;
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new AuthenticationException(UnauthorizedMessage);
            if (response.StatusCode == HttpStatusCode.Forbidden) throw new ForbiddenException(ForbiddenMessage);
            if (!response.IsSuccessStatusCode)
                throw Unavailable($"UserService trả HTTP {(int)response.StatusCode} khi gọi {method} {path}.");

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                ?? throw Unavailable($"UserService trả nội dung rỗng khi gọi {method} {path}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            // HttpClient hết thời gian chờ cũng ném OperationCanceledException; chỉ để lọt khi chính request bị hủy.
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw Unavailable($"Không gọi được UserService ({method} {path}).", ex);
        }
    }

    private DependencyUnavailableException Unavailable(string detail, Exception? cause = null)
    {
        logger.LogWarning(cause, "{Detail}", detail);
        return new DependencyUnavailableException(UnavailableMessage);
    }

    /// <summary>ProblemDetails do middleware chung của các service trả về luôn là JSON; 404 do thiếu route thì không có body.</summary>
    private static bool HasJsonBody(HttpResponseMessage response)
        => response.Content.Headers.ContentType?.MediaType is { } mediaType
            && mediaType.EndsWith("json", StringComparison.OrdinalIgnoreCase);

    private enum NotFoundMeans
    {
        /// <summary>Endpoint đã có sẵn ở UserService: 404 nghĩa là không có bản ghi.</summary>
        ResourceMissing,
        /// <summary>
        /// Endpoint chủ bãi do UserService bổ sung sau: 404 kèm ProblemDetails mới là "không có bản ghi";
        /// 404 trống nghĩa là UserService chưa có endpoint → coi như chưa gọi được.
        /// </summary>
        ResourceMissingOnlyWithProblemBody
    }
}
