using AdminService.Application.Features;
using AdminService.Infrastructure.Persistence;
using AdminService.Infrastructure.Persistence.Queries;
using AdminService.Infrastructure.Persistence.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;

namespace AdminService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDbContext<AdminDbContext>(configuration);
        services.AddScoped<ISettingsQueries, SettingsQueries>();
        services.AddScoped<IDataSeeder<AdminDbContext>, AdminDataSeeder>();
        return services;
    }
}
