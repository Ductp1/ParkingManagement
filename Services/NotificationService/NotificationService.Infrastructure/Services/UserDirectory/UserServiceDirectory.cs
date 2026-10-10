using System.Net.Http.Json;
using NotificationService.Application.Features.Dispatcher;

namespace NotificationService.Infrastructure.Services.UserDirectory;

/// <summary>
/// Tra cứu email/số điện thoại user qua UserService API (GET /api/v1/users/{id}).
/// Base URL cấu hình: appsettings → "UserService:BaseUrl" (mặc định http://localhost:5101).
/// Lỗi HTTP (404/503...) → trả null để sender quyết định PermanentFailure với message rõ ràng.
/// Module: TV6.
/// </summary>
public sealed class UserServiceDirectory(HttpClient httpClient) : IUserDirectory
{
    public async Task<string?> GetEmailAsync(int userId, CancellationToken cancellationToken = default)
        => (await GetUserAsync(userId, cancellationToken))?.Email;

    public async Task<string?> GetPhoneNumberAsync(int userId, CancellationToken cancellationToken = default)
        => (await GetUserAsync(userId, cancellationToken))?.PhoneNumber;

    private async Task<UserProfile?> GetUserAsync(int userId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/api/v1/users/{userId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        // Shape JSON của UserService.UserDto (camelCase) – chỉ map trường liên hệ cần dùng.
        return await response.Content.ReadFromJsonAsync<UserProfile>(cancellationToken: cancellationToken);
    }

    private sealed record UserProfile(int Id, string? Email, string? PhoneNumber);
}
