using BookingService.Application.Features.Bookings;
using BookingService.Application.Features.MonthlyPasses;
using BookingService.Infrastructure.HttpClients;
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
        services.AddScoped<ILotBookingSummaryQueries, LotBookingQueries>(); // US-067/069: đọc-only cho ParkingService
        services.AddScoped<IDataSeeder<BookingDbContext>, BookingDataSeeder>();
        services.AddHostedService<HoldExpiryWorker>();
        // US-083: outbox publisher + consumer kết quả thanh toán (PaymentSucceeded → ConfirmPayment → BookingConfirmed).
        // AddRabbitMqInboxConsumer đã dựng luôn bus + IRabbitMqPublisher + IOutboxTransport (không cần AddRabbitMqEventBus nữa).
        // Merge develop: handler T-302 (PaymentIntegrationEventHandler) cũ đã được THAY THẾ bởi BookingIntegrationEventHandler —
        // cùng queue "booking.service" nhưng phạm vi rộng hơn (PaymentSucceeded/PaymentFailed → ConfirmPayment +
        // phát BookingConfirmed + BookingPaymentCompleted giàu dữ liệu cho NotificationService US-083).
        services.AddScoped<IBookingEventPublisher, BookingEventOutbox>();
        services.AddRabbitMqInboxConsumer<BookingDbContext, BookingIntegrationEventHandler>(configuration);
        services.AddHostedService<ParkingManagement.ServiceDefaults.Messaging.OutboxDispatcher<BookingDbContext>>();
        // US-066 (luat 4): dang ky thue bao nguyen tu theo han muc + doc Q tu GateService.
        services.AddScoped<IMonthlyPassRegistrationStore, MonthlyPassRegistrationStore>();
        services.AddScoped<ILotSubscriberQuotaProvider, GateCapacityQuotaProvider>();
        services.AddHttpClient<GateCapacityQuotaProvider>((_, client) =>
            client.BaseAddress = new Uri(configuration["GateService:BaseUrl"] ?? "http://localhost:5107"));
        // US-069: check trang thai van hanh bai truoc khi tao booking (fail-open khi ParkingService chet).
        services.AddHttpClient<ILotOperationalStatusProvider, ParkingServiceOperationalStatusClient>((_, client) =>
        {
            client.BaseAddress = new Uri(configuration["ParkingService:BaseUrl"] ?? "http://localhost:5103/");
            client.Timeout = TimeSpan.FromSeconds(3); // nhanh de khai bao dat cho khong bi treo khi ParkingService chet
        });
        return services;
    }
}
