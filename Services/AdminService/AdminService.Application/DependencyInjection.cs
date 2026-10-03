using AdminService.Application.Features;
using Microsoft.Extensions.DependencyInjection;

namespace AdminService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetPlatformSettingsUseCase, GetPlatformSettingsUseCase>();
        return services;
    }
}
