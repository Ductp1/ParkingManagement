using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.ServiceDefaults.Persistence;
using PaymentService.Application.Features;
using PaymentService.Application.Features.Reports;
using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Persistence.Queries;
using PaymentService.Infrastructure.Persistence.Repositories;
using PaymentService.Infrastructure.Persistence.Seeding;
using PaymentService.Infrastructure.Vnpay;

namespace PaymentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<VnpayOptions>(configuration.GetSection(VnpayOptions.SectionName));
        services.AddServiceDbContext<PaymentDbContext>(configuration);
        services.AddScoped<IRateCardRepository, RateCardRepository>();
        services.AddScoped<IRateCardManagementRepository, RateCardManagementRepository>();
        services.AddScoped<IRateCardAuditLogRepository, RateCardAuditLogRepository>();
        services.AddScoped<IHolidayRepository, HolidayRepository>();
        services.AddScoped<IHolidayManagementRepository, HolidayManagementRepository>();
        services.AddScoped<IPaymentQueries, PaymentQueries>();
        // UC-22: ghi thanh toán tiền mặt staff xác nhận tại cổng ra.
        services.AddScoped<IPaymentWriter, PaymentWriter>();
        // US-078: báo cáo doanh thu chủ bãi – GROUP BY / SUM ở database.
        services.AddScoped<IOwnerRevenueQueries, OwnerRevenueQueries>();
        services.AddScoped<IDataSeeder<PaymentDbContext>, PaymentDataSeeder>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IVnpayGateway, VnpayGateway>();
        // US-083: PaymentSucceeded/PaymentFailed đã ghi OutboxMessages từ IPN use case —
        // chỉ cần bật event bus + dispatcher để event chảy tới BookingService/NotificationService.
        services.AddRabbitMqEventBus(configuration);
        services.AddHostedService<ParkingManagement.ServiceDefaults.Messaging.OutboxDispatcher<PaymentDbContext>>();
        return services;
    }
}
