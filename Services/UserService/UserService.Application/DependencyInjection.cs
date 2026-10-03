using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Features.Users;

namespace UserService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddUserApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetUserByIdUseCase, GetUserByIdUseCase>();
        services.AddScoped<IListUsersUseCase, ListUsersUseCase>();
        return services;
    }
}
