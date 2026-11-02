using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Features;
using PaymentService.Application.Features.Payments;

namespace PaymentService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentApplication(this IServiceCollection services)
    {
        services.AddScoped<IQuotePriceUseCase, QuotePriceUseCase>();
        services.AddScoped<IGetPaymentsByBookingUseCase, GetPaymentsByBookingUseCase>();
        services.AddScoped<ICreateVnpayPaymentUseCase, CreateVnpayPaymentUseCase>();
        services.AddScoped<ICreateVietQrPaymentUseCase, CreateVietQrPaymentUseCase>();
        services.AddScoped<IProcessVnpayReturnUseCase, ProcessVnpayReturnUseCase>();
        services.AddScoped<IProcessVnpayIpnUseCase, ProcessVnpayIpnUseCase>();
        return services;
    }
}
