using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Features;
using PaymentService.Application.Features.Payments;
using PaymentService.Application.Features.Reports;

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
        services.AddScoped<ICreateVnpayPaymentUseCase, CreateVnpayPaymentUseCase>();
        services.AddScoped<ICreateVietQrPaymentUseCase, CreateVietQrPaymentUseCase>();
        services.AddScoped<IProcessVnpayReturnUseCase, ProcessVnpayReturnUseCase>();
        services.AddScoped<IProcessVnpayIpnUseCase, ProcessVnpayIpnUseCase>();
        // US-078: báo cáo doanh thu chủ bãi (chỉ đọc).
        services.AddScoped<IGetOwnerRevenueSummaryUseCase, GetOwnerRevenueSummaryUseCase>();
        services.AddScoped<IGetOwnerRevenueSeriesUseCase, GetOwnerRevenueSeriesUseCase>();
        services.AddScoped<IListOwnerRevenueTransactionsUseCase, ListOwnerRevenueTransactionsUseCase>();
        services.AddScoped<IExportOwnerRevenueTransactionsUseCase, ExportOwnerRevenueTransactionsUseCase>();
        return services;
    }
}
