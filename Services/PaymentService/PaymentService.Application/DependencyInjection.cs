using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Features;
using PaymentService.Application.Features.Holidays;
using PaymentService.Application.Features.Invoices;
using PaymentService.Application.Features.Payments;
using PaymentService.Application.Features.Pricing;
using PaymentService.Application.Features.Promotions;
using PaymentService.Application.Features.RateCards;
using PaymentService.Application.Features.Refunds;
using PaymentService.Application.Features.Reports;

namespace PaymentService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IQuotePriceUseCase, QuotePriceUseCase>();
        services.AddScoped<IGetPaymentsByBookingUseCase, GetPaymentsByBookingUseCase>();
        // UC-22: chốt phí lượt gửi tại cổng ra + ghi thanh toán tiền mặt do staff xác nhận.
        services.AddScoped<IComputeSessionFeeUseCase, ComputeSessionFeeUseCase>();
        services.AddScoped<IRecordGateSettlementUseCase, RecordGateSettlementUseCase>();
        // US-047: Tính phí phụ thu quá giờ độc lập
        services.AddScoped<ICalculateOverstayPenaltyUseCase, CalculateOverstayPenaltyUseCase>();
        // US-034: Hoàn tiền tự động khi đã trừ tiền nhưng booking thất bại
        services.AddScoped<IProcessAutoRefundUseCase, ProcessAutoRefundUseCase>();
        // US-041: Chủ bãi chủ động hoàn tiền khi lỗi tại bãi
        services.AddScoped<IProcessOwnerRefundUseCase, ProcessOwnerRefundUseCase>();
        // US-070 & US-071: Quản lý biểu giá và phụ thu
        services.AddScoped<ICreateRateCardUseCase, CreateRateCardUseCase>();
        services.AddScoped<IGetRateCardsByLotUseCase, GetRateCardsByLotUseCase>();
        services.AddScoped<IUpdateRateCardUseCase, UpdateRateCardUseCase>();
        // US-073: Quản lý và áp dụng mã khuyến mãi
        services.AddScoped<ICreatePromotionUseCase, CreatePromotionUseCase>();
        services.AddScoped<IApplyPromotionUseCase, ApplyPromotionUseCase>();
        services.AddScoped<IGetPromotionsUseCase, GetPromotionsUseCase>();
        services.AddScoped<IUpdatePromotionUseCase, UpdatePromotionUseCase>();
        // US-036: Hóa đơn điện tử
        services.AddScoped<IIssueParkingInvoiceUseCase, IssueParkingInvoiceUseCase>();
        services.AddScoped<IIssueCommissionInvoiceUseCase, IssueCommissionInvoiceUseCase>();
        services.AddScoped<IGetInvoiceUseCase, GetInvoiceUseCase>();
        // Quản lý ngày lễ phục vụ phụ thu
        services.AddScoped<ICreateHolidayUseCase, CreateHolidayUseCase>();
        services.AddScoped<IManageHolidaysUseCase, ManageHolidaysUseCase>();
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
