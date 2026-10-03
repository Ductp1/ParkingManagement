using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Features;

namespace PaymentService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentApplication(this IServiceCollection services)
    {
        services.AddScoped<IQuotePriceUseCase, QuotePriceUseCase>();
        services.AddScoped<IGetPaymentsByBookingUseCase, GetPaymentsByBookingUseCase>();
        return services;
    }
}
