using NotificationService.Application.Features.Preferences;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Preferences;

/// <summary>
/// Use case: Lấy notification preferences của user. Module: TV6 (S4).
/// Đọc từ bảng NotificationPreferences; dòng chưa tồn tại = giá trị mặc định
/// (bật mọi kênh hữu ích, TẮT marketing).
/// </summary>
public sealed class GetNotificationPreferencesUseCase(INotificationPreferenceRepository repository) : IGetNotificationPreferencesUseCase
{
    public async Task<GetPreferencesResponse> ExecuteAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("userId phải > 0", nameof(userId));

        var rows = await repository.GetByUserAsync(userId, cancellationToken);

        bool Value(string templateKey, NotificationChannel channel, bool fallback)
            => rows.FirstOrDefault(r => r.TemplateKey == templateKey && r.Channel == channel)?.IsEnabled ?? fallback;

        return new GetPreferencesResponse(
            UserId: userId,
            EmailEnabled: Value(NotificationPreferenceKeys.AllTemplates, NotificationChannel.Email, true),
            SmsEnabled: Value(NotificationPreferenceKeys.AllTemplates, NotificationChannel.Sms, true),
            PushEnabled: Value(NotificationPreferenceKeys.AllTemplates, NotificationChannel.Push, true),
            TransactionalNotifications: Value(NotificationPreferenceKeys.Transactional, NotificationChannel.InApp, true),
            MarketingNotifications: Value(NotificationPreferenceKeys.Marketing, NotificationChannel.InApp, false),
            UrgentNotifications: Value(NotificationPreferenceKeys.Urgent, NotificationChannel.InApp, true));
    }
}

/// <summary>
/// Use case: Cập nhật notification preferences của user. Module: TV6 (S4).
/// Chỉ persist các flag được gửi rõ ràng (null = giữ nguyên) – upsert theo unique index.
/// </summary>
public sealed class UpdateNotificationPreferencesUseCase(INotificationPreferenceRepository repository) : IUpdateNotificationPreferencesUseCase
{
    public async Task ExecuteAsync(int userId, UpdatePreferencesRequest request, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("userId phải > 0", nameof(userId));
        ArgumentNullException.ThrowIfNull(request);

        await UpsertAsync(userId, NotificationPreferenceKeys.AllTemplates, NotificationChannel.Email, request.EmailEnabled, cancellationToken);
        await UpsertAsync(userId, NotificationPreferenceKeys.AllTemplates, NotificationChannel.Sms, request.SmsEnabled, cancellationToken);
        await UpsertAsync(userId, NotificationPreferenceKeys.AllTemplates, NotificationChannel.Push, request.PushEnabled, cancellationToken);
        await UpsertAsync(userId, NotificationPreferenceKeys.Transactional, NotificationChannel.InApp, request.TransactionalNotifications, cancellationToken);
        await UpsertAsync(userId, NotificationPreferenceKeys.Marketing, NotificationChannel.InApp, request.MarketingNotifications, cancellationToken);
        await UpsertAsync(userId, NotificationPreferenceKeys.Urgent, NotificationChannel.InApp, request.UrgentNotifications, cancellationToken);
    }

    private async Task UpsertAsync(int userId, string templateKey, NotificationChannel channel,
        bool? isEnabled, CancellationToken cancellationToken)
    {
        if (isEnabled is null)
            return;

        await repository.UpsertAsync(new NotificationPreference
        {
            UserId = userId,
            TemplateKey = templateKey,
            Channel = channel,
            IsEnabled = isEnabled.Value
        }, cancellationToken);
    }
}
