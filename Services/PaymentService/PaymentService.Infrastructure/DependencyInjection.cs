using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;
using PaymentService.Application.Features;
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
        services.AddScoped<IHolidayRepository, HolidayRepository>();
        services.AddScoped<IPaymentQueries, PaymentQueries>();
        services.AddScoped<IDataSeeder<PaymentDbContext>, PaymentDataSeeder>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IVnpayGateway, VnpayGateway>();
        return services;
    }
}
