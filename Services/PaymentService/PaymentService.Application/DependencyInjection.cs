using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Features;

namespace PaymentService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentApplication(this IServiceCollection services)
    {
        services.AddScoped<IQuotePriceUseCase, QuotePriceUseCase>();
        services.AddScoped<IGetPaymentsByBookingUseCase, GetPaymentsByBookingUseCase>();
        // UC-22: chốt phí lượt gửi tại cổng ra + ghi thanh toán tiền mặt do staff xác nhận.
        services.AddScoped<IComputeSessionFeeUseCase, ComputeSessionFeeUseCase>();
        services.AddScoped<IRecordGateSettlementUseCase, RecordGateSettlementUseCase>();
        return services;
    }
}
