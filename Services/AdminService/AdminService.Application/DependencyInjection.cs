using AdminService.Application.Features;
using AdminService.Application.Features.Users;
using Microsoft.Extensions.DependencyInjection;

namespace AdminService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetPlatformSettingsUseCase, GetPlatformSettingsUseCase>();
        // US-096: tra cứu người dùng (đọc qua UserService)
        services.AddScoped<IListAdminUsersUseCase, ListAdminUsersUseCase>();
        services.AddScoped<IGetAdminUserByIdUseCase, GetAdminUserByIdUseCase>();
        return services;
    }
}
