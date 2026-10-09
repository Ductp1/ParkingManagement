using NotificationService.Application.Features.DeviceTokens;

namespace NotificationService.Application.Features.DeviceTokens;

/// <summary>
/// Use case: Đăng ký device token cho push notifications. Module: TV6 (S1-T602).
/// </summary>
public sealed class RegisterDeviceTokenUseCase : IRegisterDeviceTokenUseCase
{
    public async Task ExecuteAsync(RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.UserId <= 0)
            throw new ArgumentException("UserId phải > 0", nameof(request.UserId));

        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ArgumentException("Token không được trống", nameof(request.Token));

        // TODO: Save to DeviceToken table when entity is fully integrated
        // For now, just validate
        await Task.CompletedTask;
    }
}

/// <summary>
/// Use case: Hủy đăng ký device token. Module: TV6 (S1-T602).
/// </summary>
public sealed class UnregisterDeviceTokenUseCase : IUnregisterDeviceTokenUseCase
{
    public async Task ExecuteAsync(int userId, string deviceId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("userId phải > 0", nameof(userId));

        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("deviceId không được trống", nameof(deviceId));

        // TODO: Delete from DeviceToken table
        // For now, just validate
        await Task.CompletedTask;
    }
}
