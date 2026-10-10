using AdminService.Application.Features;
using AdminService.Application.Features.Owners;
using AdminService.Application.Features.Settings;
using AdminService.Application.Features.Users;
using Microsoft.Extensions.DependencyInjection;

namespace AdminService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetPlatformSettingsUseCase, GetPlatformSettingsUseCase>();
        // US-098: đọc tham số theo khóa (giá trị hiệu lực theo thời điểm)
        services.AddScoped<IGetSystemConfigByKeyUseCase, GetSystemConfigByKeyUseCase>();
        // US-098: đổi giá trị tham số có Effective Date
        services.AddScoped<IUpdateSystemConfigUseCase, UpdateSystemConfigUseCase>();
        // US-098: lịch sử thay đổi + hủy thay đổi chưa hiệu lực
        services.AddScoped<IGetSystemConfigHistoryUseCase, GetSystemConfigHistoryUseCase>();
        services.AddScoped<ICancelSystemConfigChangeUseCase, CancelSystemConfigChangeUseCase>();
        // US-096: tra cứu người dùng (đọc qua UserService)
        services.AddScoped<IListAdminUsersUseCase, ListAdminUsersUseCase>();
        services.AddScoped<IGetAdminUserByIdUseCase, GetAdminUserByIdUseCase>();
        // US-096: lịch sử vi phạm của chủ bãi
        services.AddScoped<IGetOwnerSanctionsUseCase, GetOwnerSanctionsUseCase>();
        // US-096: khóa / mở khóa chủ bãi
        services.AddScoped<ILockOwnerUseCase, LockOwnerUseCase>();
        services.AddScoped<IUnlockOwnerUseCase, UnlockOwnerUseCase>();
        return services;
    }
}
