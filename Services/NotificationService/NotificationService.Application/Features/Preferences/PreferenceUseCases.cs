using NotificationService.Application.Features.Preferences;

namespace NotificationService.Application.Features.Preferences;

/// <summary>
/// Use case: Lấy notification preferences của user. Module: TV6 (S4).
/// </summary>
public sealed class GetNotificationPreferencesUseCase : IGetNotificationPreferencesUseCase
{
    public async Task<GetPreferencesResponse> ExecuteAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("userId phải > 0", nameof(userId));

        // TODO: Load từ database khi entity NotificationPreference được implement
        // For now, return default preferences
        return new GetPreferencesResponse(
            UserId: userId,
            EmailEnabled: true,
            SmsEnabled: true,
            PushEnabled: true,
            TransactionalNotifications: true,
            MarketingNotifications: false,
            UrgentNotifications: true
        );
    }
}

/// <summary>
/// Use case: Cập nhật notification preferences của user. Module: TV6 (S4).
/// </summary>
public sealed class UpdateNotificationPreferencesUseCase : IUpdateNotificationPreferencesUseCase
{
    public async Task ExecuteAsync(int userId, UpdatePreferencesRequest request, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("userId phải > 0", nameof(userId));

        if (request == null)
            throw new ArgumentNullException(nameof(request));

        // TODO: Update database when NotificationPreference entity is implemented
        // For now, just validate and return
        await Task.CompletedTask;
    }
}
