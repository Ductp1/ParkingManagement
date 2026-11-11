using BookingService.Application.Features.Bookings;
using BookingService.Domain.Rules;
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
        // US-040: worker tự động chốt phiên đỗ (Auto-Completed / No-show khi thiếu dữ liệu check-in/out).
        services.AddHostedService<SessionCompletionWorker>();
        services.AddSingleton(configuration.GetSection("SessionCompletion").Get<SessionCompletionOptions>() ?? SessionCompletionOptions.Default);
        // US-103: ngưỡng chống lạm dụng đặt/hủy – bind từ "BookingAbuse", mọi ngưỡng cấu hình được.
        services.AddSingleton(configuration.GetSection("BookingAbuse").Get<BookingAbuseOptions>() ?? BookingAbuseOptions.Default);
        services.AddSingleton<BookingAbuseGuard>(sp =>
        {
            var options = sp.GetRequiredService<BookingAbuseOptions>();
            return ActivatorUtilities.CreateInstance<BookingAbuseGuard>(sp, options);
        });
        services.AddScoped<IBookingAbuseQueries, BookingAbuseQueries>();
        // US-052: đẩy booking mới về PMS của bãi qua ParkingService (đi qua Gateway).
        var gateway = configuration["Services:GatewayBaseUrl"];
        services.AddHttpClient<ILotIntegrationClient, ParkingIntegrationClient>(client =>
        {
            client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(gateway) ? "http://localhost:5000/" : gateway.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        return services;
    }
}
