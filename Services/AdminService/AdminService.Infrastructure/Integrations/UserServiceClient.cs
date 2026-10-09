using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdminService.Application.Features.Users;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Infrastructure.Integrations;

/// <summary>
/// Cài đặt port IUserServiceClient bằng HttpClient. BaseAddress = Gateway (cấu hình "Services:GatewayBaseUrl"),
/// đường dẫn là API công khai của UserService – AdminService không tham chiếu project hay database của UserService.
/// </summary>
public sealed class UserServiceClient(HttpClient http) : IUserServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PagedResult<AdminUserSummaryDto>> ListUsersAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken)
    {
        var path = $"api/v1/users?page={page}&pageSize={pageSize}";
        if (role is { } r) path += $"&role={r}";

        return await GetAsync<PagedResult<AdminUserSummaryDto>>(path, cancellationToken)
            ?? throw new UserServiceUnavailableException($"UserService trả HTTP 404 khi gọi GET {path}.");
    }

    public Task<AdminUserDto?> FindUserByIdAsync(int userId, CancellationToken cancellationToken)
        => GetAsync<AdminUserDto>($"api/v1/users/{userId}", cancellationToken);

    /// <summary>GET và đọc JSON; 404 → null, mọi lỗi khác → UserServiceUnavailableException.</summary>
    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken) where T : class
    {
        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                throw new UserServiceUnavailableException($"UserService trả HTTP {(int)response.StatusCode} khi gọi GET {path}.");

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                ?? throw new UserServiceUnavailableException($"UserService trả nội dung rỗng khi gọi GET {path}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            // HttpClient hết thời gian chờ cũng ném OperationCanceledException; chỉ để lọt khi chính request bị hủy.
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new UserServiceUnavailableException($"Không gọi được UserService (GET {path}).", ex);
        }
    }
}
