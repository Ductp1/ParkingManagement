using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Dispatcher;

/// <summary>
/// Channel-specific sender interface. Mỗi implementation xử lý 1 loại kênh.
/// Module: TV6 (S1-T602).
/// </summary>
public interface INotificationChannelSender
{
    /// <summary>
    /// Kênh được handle bởi sender này.
    /// </summary>
    NotificationChannel SupportedChannel { get; }

    /// <summary>
    /// Thực hiện gửi thông báo qua kênh.
    /// Trả về (success, errorMessage). 
    /// Nếu success=false, dispatcher sẽ retry hoặc lưu lỗi.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> SendAsync(
        int userId,
        string title,
        string body,
        string? templateKey = null,
        string? dataJson = null,
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
/// Xử lý in-app notification (lưu DB + push qua SignalR). Module: TV6 (S1-T602).
/// </summary>
public interface IInAppNotificationSender : INotificationChannelSender
{
    // Marker interface
}
