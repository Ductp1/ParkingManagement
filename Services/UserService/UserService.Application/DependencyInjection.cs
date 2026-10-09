using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Features.Users;

namespace UserService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddUserApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetUserByIdUseCase, GetUserByIdUseCase>();
        services.AddScoped<IListUsersUseCase, ListUsersUseCase>();
        services.AddScoped<UserService.Application.Features.Auth.ILoginUseCase, UserService.Application.Features.Auth.LoginUseCase>();
        services.AddScoped<UserService.Application.Features.Auth.IRefreshSessionUseCase, UserService.Application.Features.Auth.RefreshSessionUseCase>();
        services.AddScoped<UserService.Application.Features.Auth.ILogoutUseCase, UserService.Application.Features.Auth.LogoutUseCase>();
        services.AddScoped<UserService.Application.Features.Identity.AccountUseCases>();
        return services;
    }
}
