using NotificationService.Application.Features.DeviceTokens;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Exceptions;

namespace NotificationService.Application.Features.DeviceTokens;

/// <summary>
/// Use case: Đăng ký device token cho push notifications. Module: TV6 (S1-T602).
/// Upsert theo (UserId, Token): token mới → thêm; token đã có (kể cả đã revoke) →
/// cập nhật LastUsedAtUtc/Platform/DeviceName và bật lại – đăng ký là thao tác idempotent.
/// </summary>
public sealed class RegisterDeviceTokenUseCase(IDeviceTokenRepository repository, TimeProvider timeProvider) : IRegisterDeviceTokenUseCase
{
    public async Task ExecuteAsync(RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId <= 0)
            throw new ArgumentException("UserId phải > 0", nameof(request.UserId));

        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ArgumentException("Token không được trống", nameof(request.Token));

        var token = request.Token.Trim();
        var existing = await repository.GetByUserAndTokenAsync(request.UserId, token, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (existing is null)
        {
            await repository.AddAsync(new DeviceToken
            {
                UserId = request.UserId,
                Token = token,
                Platform = request.DeviceType ?? "Unknown",
                DeviceName = request.DeviceId,
                LastUsedAtUtc = now,
                IsRevoked = false
            }, cancellationToken);
        }
        else
        {
            existing.Platform = request.DeviceType ?? existing.Platform;
            existing.DeviceName = request.DeviceId ?? existing.DeviceName;
            existing.LastUsedAtUtc = now;
            existing.IsRevoked = false;   // re-login/re-install → bật lại token cũ
            await repository.UpdateAsync(existing, cancellationToken);
        }
    }
}

/// <summary>
/// Use case: Hủy đăng ký device token (user logout / uninstall). Module: TV6 (S1-T602).
/// deviceId ở API chính là FCM token. Không tìm thấy → NotFoundException (404).
/// </summary>
public sealed class UnregisterDeviceTokenUseCase(IDeviceTokenRepository repository) : IUnregisterDeviceTokenUseCase
{
    public async Task ExecuteAsync(int userId, string deviceId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("userId phải > 0", nameof(userId));

        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("deviceId không được trống", nameof(deviceId));

        var revoked = await repository.RevokeByUserAndTokenAsync(userId, deviceId.Trim(), cancellationToken);
        if (revoked == 0)
            throw new NotFoundException("Device token", deviceId);
    }
}
