using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Features;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Application.Features.DeviceTokens;
using NotificationService.Application.Features.Notifications;
using NotificationService.Application.Features.Preferences;

namespace NotificationService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        // ===== S1-T602: Dispatcher Use Cases =====
        services.AddScoped<ISendNotificationUseCase, SendNotificationUseCase>();
        services.AddScoped<IRetryNotificationUseCase, RetryNotificationUseCase>();

        // ===== S1+S2: Notification Management Use Cases =====
        services.AddScoped<IGetInboxUseCase, GetInboxUseCase>();
        services.AddScoped<IGetNotificationsUseCase, GetNotificationsUseCase>();
        services.AddScoped<IMarkNotificationAsReadUseCase, MarkNotificationAsReadUseCase>();

        // ===== S1-T602: Device Token Use Cases =====
        services.AddScoped<IRegisterDeviceTokenUseCase, RegisterDeviceTokenUseCase>();
        services.AddScoped<IUnregisterDeviceTokenUseCase, UnregisterDeviceTokenUseCase>();

        // ===== S4: Notification Preferences Use Cases =====
        services.AddScoped<IGetNotificationPreferencesUseCase, GetNotificationPreferencesUseCase>();
        services.AddScoped<IUpdateNotificationPreferencesUseCase, UpdateNotificationPreferencesUseCase>();

        return services;
    }
}
