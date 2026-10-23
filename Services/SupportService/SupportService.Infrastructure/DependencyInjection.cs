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
using SupportService.Infrastructure.Workers;

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
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddHostedService<ComplaintEscalationWorker>();   // US-089 AC2: quá 48h → Escalated
        services.AddHttpClient<IBookingLookup, BookingServiceClient>(c =>
        {
            c.BaseAddress = new Uri(configuration["Services:BookingService"] ?? "http://localhost:5104/");
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient<IOwnerLotLookup, ParkingServiceClient>(c =>   // US-094: xác minh bãi của chủ bãi
        {
            c.BaseAddress = new Uri(configuration["Services:ParkingService"] ?? "http://localhost:5103/");
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IDataSeeder<SupportDbContext>, SupportDataSeeder>();
        return services;
    }
}
