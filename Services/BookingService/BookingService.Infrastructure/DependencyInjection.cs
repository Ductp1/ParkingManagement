using BookingService.Application.Features.Bookings;
using BookingService.Domain.Rules;
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
using ParkingManagement.ServiceDefaults.Messaging;
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
        services.AddScoped<IBookingAbuseQueries, BookingAbuseQueries>();
        // US-052: đẩy booking mới về PMS của bãi qua ParkingService (đi qua Gateway).
        var gateway = configuration["Services:GatewayBaseUrl"];
        services.AddHttpClient<ILotIntegrationClient, ParkingIntegrationClient>(client =>
        {
            client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(gateway) ? "http://localhost:5000/" : gateway.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // T-302: Saga Booking ↔ Payment (RabbitMQ Inbox Event Handler)
        services.AddScoped<PaymentIntegrationEventHandler>();
        if (configuration.GetSection("EventBus:Subscription").Exists())
        {
            services.AddRabbitMqInboxConsumer<BookingDbContext, PaymentIntegrationEventHandler>(configuration);
        }

        // US-066 (luat 4): dang ky thue bao nguyen tu theo han muc + doc Q tu GateService.
        services.AddScoped<IMonthlyPassRegistrationStore, MonthlyPassRegistrationStore>();
        services.AddScoped<ILotSubscriberQuotaProvider, GateCapacityQuotaProvider>();
        services.AddHttpClient<GateCapacityQuotaProvider>((_, client) =>
            client.BaseAddress = new Uri(configuration["GateService:BaseUrl"] ?? "http://localhost:5107"));
        return services;
    }
}
