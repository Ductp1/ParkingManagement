using BookingService.Application.Features.Bookings;
using BookingService.Application.Features.MonthlyPasses;
using BookingService.Infrastructure.Integrations;
using BookingService.Infrastructure.Persistence;
using BookingService.Infrastructure.Persistence.Queries;
using BookingService.Infrastructure.Persistence.Repositories;
using BookingService.Infrastructure.Persistence.Seeding;
using BookingService.Infrastructure.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;

namespace BookingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBookingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDbContext<BookingDbContext>(configuration);
        services.AddScoped<IBookingQueries, BookingQueries>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IDataSeeder<BookingDbContext>, BookingDataSeeder>();
        services.AddHostedService<HoldExpiryWorker>();
        // US-066 (luat 4): dang ky thue bao nguyen tu theo han muc + doc Q tu GateService.
        services.AddScoped<IMonthlyPassRegistrationStore, MonthlyPassRegistrationStore>();
        services.AddScoped<ILotSubscriberQuotaProvider, GateCapacityQuotaProvider>();
        services.AddHttpClient<GateCapacityQuotaProvider>((_, client) =>
            client.BaseAddress = new Uri(configuration["GateService:BaseUrl"] ?? "http://localhost:5107"));
        return services;
    }
}
