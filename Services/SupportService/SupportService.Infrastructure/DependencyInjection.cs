using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;
using SupportService.Application.Features;
using SupportService.Application.Interfaces;
using SupportService.Application.Services;
using SupportService.Infrastructure.Http;
using SupportService.Infrastructure.Persistence;
using SupportService.Infrastructure.Persistence.Repositories;
using SupportService.Infrastructure.Persistence.Queries;
using SupportService.Infrastructure.Persistence.Seeding;

namespace SupportService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSupportInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDbContext<SupportDbContext>(configuration);
        services.AddScoped<ISupportQueries, SupportQueries>();
        services.AddScoped<IFaqQueries, FaqQueries>();
        services.AddSingleton(configuration.GetSection("HelpCenter").Get<HelpCenterSettings>() ?? new HelpCenterSettings());
        services.AddScoped<IComplaintRepository, ComplaintRepository>();
        services.AddHttpClient<IBookingLookup, BookingServiceClient>(c =>
        {
            c.BaseAddress = new Uri(configuration["Services:BookingService"] ?? "http://localhost:5104/");
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IDataSeeder<SupportDbContext>, SupportDataSeeder>();
        return services;
    }
}
