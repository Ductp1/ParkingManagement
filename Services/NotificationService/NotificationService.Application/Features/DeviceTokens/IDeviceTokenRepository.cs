using NotificationService.Domain.Entities;

namespace NotificationService.Application.Features.DeviceTokens;

/// <summary>
/// Port lưu trữ device token FCM. Module: TV6.
/// Token là định danh duy nhất của 1 cài đặt app (unique index trên Token).
/// </summary>
public interface IDeviceTokenRepository
{
    /// <summary>Lấy token của user (để upsert khi re-đăng ký).</summary>
    Task<DeviceToken?> GetByUserAndTokenAsync(int userId, string token, CancellationToken cancellationToken = default);

    /// <summary>Danh sách token còn hiệu lực (IsRevoked = false) của user – FCM sender dùng để push.</summary>
    Task<IReadOnlyList<DeviceToken>> GetActiveByUserAsync(int userId, CancellationToken cancellationToken = default);

    Task AddAsync(DeviceToken deviceToken, CancellationToken cancellationToken = default);

    Task UpdateAsync(DeviceToken deviceToken, CancellationToken cancellationToken = default);

    /// <summary>Thu hồi token (IsRevoked = true). Trả số token đã thu hồi (0 = không tồn tại).</summary>
    Task<int> RevokeByUserAndTokenAsync(int userId, string token, CancellationToken cancellationToken = default);
}
