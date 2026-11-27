using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Dispatcher;

/// <summary>
/// Channel-specific sender interface. Mỗi implementation xử lý 1 loại kênh.
/// Trả về <see cref="ChannelSendResult"/> để dispatcher phân biệt lỗi tạm thời (retry) và lỗi vĩnh viễn (Failed ngay).
/// Module: TV6 (S1-T602).
/// </summary>
public interface INotificationChannelSender
{
    /// <summary>
    /// Kênh được handle bởi sender này.
    /// </summary>
    NotificationChannel SupportedChannel { get; }

    /// <summary>
    /// Thực hiện gửi thông báo qua kênh. Receiver đã được dispatcher lưu DB – sender KHÔNG tạo row mới
    /// (tránh trùng lặp in-app khi retry), chỉ thực hiện hành động của kênh và trả kết quả phân loại lỗi.
    /// </summary>
    Task<ChannelSendResult> SendAsync(
        Notification notification,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Xử lý sending email qua SMTP. Module: TV6 (S1-T602).
/// </summary>
public interface IEmailNotificationSender : INotificationChannelSender
{
    // Marker interface, thêm custom methods nếu cần
}

/// <summary>
/// Xử lý sending SMS (mock hoặc thực). Module: TV6 (S1-T602).
/// </summary>
public interface ISmsNotificationSender : INotificationChannelSender
{
    // Marker interface
}

/// <summary>
/// Xử lý sending Firebase Cloud Messaging (FCM). Module: TV6 (S1-T602).
/// </summary>
public interface IFcmNotificationSender : INotificationChannelSender
{
    // Marker interface
}

/// <summary>
/// Xử lý in-app notification (push real-time qua SignalR). Module: TV6 (S1-T602).
/// </summary>
public interface IInAppNotificationSender : INotificationChannelSender
{
    // Marker interface
}

/// <summary>
/// Port broadcast notification qua SignalR. Implementation đăng ký ở API layer
/// (SignalRInAppNotificationBroadcaster) vì cần IHubContext – Infrastructure không tham chiếu API được.
/// Đăng ký DI là BẮT BUỘC, nếu thiếu resolve IInAppNotificationSender sẽ nổ runtime.
/// Module: TV6 (S1-T602).
/// </summary>
public interface IInAppNotificationBroadcaster
{
    Task BroadcastAsync(int userId, Notification notification, CancellationToken cancellationToken = default);
}

