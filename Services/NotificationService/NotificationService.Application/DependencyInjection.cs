using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Features;

namespace NotificationService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetInboxUseCase, GetInboxUseCase>();
        return services;
    }
}
