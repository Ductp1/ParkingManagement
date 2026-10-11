namespace NotificationService.Application.Features.Preferences;

/// <summary>
/// Use case: Lấy notification preferences của user. Module: TV6 (S4).
/// </summary>
public interface IGetNotificationPreferencesUseCase
{
    Task<GetPreferencesResponse> ExecuteAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case: Cập nhật notification preferences của user. Module: TV6 (S4).
/// </summary>
public interface IUpdateNotificationPreferencesUseCase
{
    Task ExecuteAsync(int userId, UpdatePreferencesRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Response cho preferences. Module: TV6 (S4).
/// </summary>
public sealed record GetPreferencesResponse(
    int UserId,
    bool EmailEnabled = true,
    bool SmsEnabled = true,
    bool PushEnabled = true,
    bool TransactionalNotifications = true,
    bool MarketingNotifications = false,
    bool UrgentNotifications = true
);

/// <summary>
/// Request để update preferences. Module: TV6 (S4).
/// </summary>
public sealed record UpdatePreferencesRequest(
    bool? EmailEnabled = null,
    bool? SmsEnabled = null,
    bool? PushEnabled = null,
    bool? TransactionalNotifications = null,
    bool? MarketingNotifications = null,
    bool? UrgentNotifications = null
);
