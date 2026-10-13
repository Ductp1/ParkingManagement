namespace NotificationService.Application.Features.DeviceTokens;

/// <summary>
/// Request để đăng ký device token (FCM). Module: TV6 (S1-T602).
/// </summary>
public sealed record RegisterDeviceTokenRequest(
    int UserId,
    string Token,
    string DeviceId,
    string? DeviceType = null // "iOS", "Android", "Web", etc.
);

/// <summary>
/// Use case: Đăng ký device token cho push notifications. Module: TV6 (S1-T602).
/// </summary>
public interface IRegisterDeviceTokenUseCase
{
    Task ExecuteAsync(RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case: Xóa device token khi user logout. Module: TV6 (S1-T602).
/// </summary>
public interface IUnregisterDeviceTokenUseCase
{
    Task ExecuteAsync(int userId, string deviceId, CancellationToken cancellationToken = default);
}
