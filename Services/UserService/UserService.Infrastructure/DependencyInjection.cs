using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;
using UserService.Application.Features.Users;
using UserService.Infrastructure.Persistence;
using UserService.Infrastructure.Persistence.Queries;
using UserService.Infrastructure.Persistence.Seeding;

namespace UserService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUserInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDbContext<UserDbContext>(configuration);
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<IDataSeeder<UserDbContext>, UserDataSeeder>();
        return services;
    }
}
