using AdminService.Application.Features;
using AdminService.Application.Features.Owners;
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
        // US-096: lịch sử vi phạm của chủ bãi
        services.AddScoped<IGetOwnerSanctionsUseCase, GetOwnerSanctionsUseCase>();
        // US-096: khóa chủ bãi
        services.AddScoped<ILockOwnerUseCase, LockOwnerUseCase>();
        return services;
    }
}
