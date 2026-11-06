namespace NotificationService.Application.Features.Dispatcher;

/// <summary>
/// Port tra cứu thông tin liên hệ của user (email/số điện thoại). Module: TV6.
/// NotificationService KHÔNG sở hữu bảng Users – port được Infrastructure cài bằng
/// HTTP call sang UserService API (GET /api/v1/users/{id}).
/// </summary>
public interface IUserDirectory
{
    /// <summary>Email người nhận. Null khi user không tồn tại / chưa khai báo email.</summary>
    Task<string?> GetEmailAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Số điện thoại người nhận (SMS). Null khi không có.</summary>
    Task<string?> GetPhoneNumberAsync(int userId, CancellationToken cancellationToken = default);
}
