using BookingService.Application.Features.Bookings;
using BookingService.Infrastructure.Persistence;
using BookingService.Infrastructure.Persistence.Queries;
using BookingService.Infrastructure.Persistence.Seeding;
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
        services.AddScoped<IDataSeeder<BookingDbContext>, BookingDataSeeder>();
        return services;
    }
}
