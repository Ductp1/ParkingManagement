using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Features.Users;

namespace UserService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddUserApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetUserByIdUseCase, GetUserByIdUseCase>();
        services.AddScoped<IListUsersUseCase, ListUsersUseCase>();
        services.AddScoped<ProfileUseCases>();
        services.AddScoped<AvatarUseCases>();
        services.AddScoped<ILockOwnerProfileUseCase, LockOwnerProfileUseCase>();
        services.AddScoped<IUnlockOwnerProfileUseCase, UnlockOwnerProfileUseCase>();

        // US-057: Chủ bãi xin rời nền tảng
        services.AddScoped<IRequestOwnerExitUseCase, RequestOwnerExitUseCase>();
        services.AddScoped<ICancelOwnerExitUseCase, CancelOwnerExitUseCase>();
        services.AddScoped<ITerminateOwnerExitUseCase, TerminateOwnerExitUseCase>();
        services.AddScoped<IGetOwnerExitStatusUseCase, GetOwnerExitStatusUseCase>();

        services.AddScoped<UserService.Application.Features.Auth.ILoginUseCase, UserService.Application.Features.Auth.LoginUseCase>();
        services.AddScoped<UserService.Application.Features.Auth.IRefreshSessionUseCase, UserService.Application.Features.Auth.RefreshSessionUseCase>();
        services.AddScoped<UserService.Application.Features.Auth.ILogoutUseCase, UserService.Application.Features.Auth.LogoutUseCase>();
        services.AddScoped<UserService.Application.Features.Identity.AccountUseCases>();
        services.AddScoped<UserService.Application.Features.Staff.StaffUseCases>();
        services.AddScoped<UserService.Application.Features.Identity.OwnerAccountUseCases>();   // US-116 U2–U5
        services.AddScoped<UserService.Application.Features.Privacy.DataRequestUseCases>();
        services.AddScoped<UserService.Application.Features.Kyc.KycUseCases>();
        services.AddScoped<UserService.Application.Features.History.ParkingHistoryUseCases>();
        // US-053: Admin duyệt hồ sơ đối tác → thêm role LotOwner cho chính tài khoản người nộp
        services.AddScoped<UserService.Application.Features.OwnerOnboarding.IGrantLotOwnerRoleUseCase,
            UserService.Application.Features.OwnerOnboarding.GrantLotOwnerRoleUseCase>();
        return services;
    }
}
