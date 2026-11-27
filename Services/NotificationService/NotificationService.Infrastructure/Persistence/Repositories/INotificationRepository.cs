using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository interface cho Notification entity. Module: TV6.
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// Lấy notification theo ID.
    /// </summary>
    Task<Notification?> GetByIdAsync(int notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách thông báo của user (phân trang).
    /// </summary>
    Task<(int Total, List<Notification> Items)> GetByUserIdAsync(
        int userId,
        bool unreadOnly = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Đếm thông báo chưa đọc của user.
    /// </summary>
    Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách notification Pending để retry.
    /// </summary>
    Task<List<Notification>> GetPendingForRetryAsync(
        int maxRetryAttempts,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Tạo notification mới.
    /// </summary>
    Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật notification (e.g., status, retryCount, sentAt).
    /// </summary>
    Task<Notification> UpdateAsync(Notification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu notification là đã đọc.
    /// </summary>
    Task MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa notification (soft delete hoặc hard delete).
    /// </summary>
    Task DeleteAsync(int notificationId, CancellationToken cancellationToken = default);
}
