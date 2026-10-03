using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Features;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Persistence.Queries;
using NotificationService.Infrastructure.Persistence.Seeding;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;

namespace NotificationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDbContext<NotificationDbContext>(configuration);
        services.AddScoped<INotificationQueries, NotificationQueries>();
        services.AddScoped<IDataSeeder<NotificationDbContext>, NotificationDataSeeder>();
        return services;
    }
}
