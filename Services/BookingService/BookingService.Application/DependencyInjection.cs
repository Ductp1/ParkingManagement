using BookingService.Application.Features.Bookings;
using BookingService.Application.Features.MonthlyPasses;
using Microsoft.Extensions.DependencyInjection;

namespace BookingService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBookingApplication(this IServiceCollection services)
    {
        services.AddSingleton<IQrTokenService, QrTokenService>();
        services.AddScoped<IGetBookingByCodeUseCase, GetBookingByCodeUseCase>();
        services.AddScoped<IListMyBookingsUseCase, ListMyBookingsUseCase>();
        services.AddScoped<IPreviewCancellationUseCase, PreviewCancellationUseCase>();
        services.AddScoped<ICreateBookingUseCase, CreateBookingUseCase>();
        services.AddScoped<ICancelBookingUseCase, CancelBookingUseCase>();
        services.AddScoped<IModifyBookingUseCase, ModifyBookingUseCase>();
        services.AddScoped<IExtendBookingUseCase, ExtendBookingUseCase>();
        services.AddScoped<IReviewBookingUseCase, ReviewBookingUseCase>();
        services.AddScoped<ILotCancelBookingUseCase, LotCancelBookingUseCase>();
        services.AddScoped<IGateBookingActionsUseCase, GateBookingActionsUseCase>();
        // US-039: danh sách booking quá 30 phút ân hạn check-in cho Staff hủy No-show.
        services.AddScoped<IListNoShowCandidatesUseCase, ListNoShowCandidatesUseCase>();
        // US-103: guard chặn lạm dụng đặt/hủy – ngưỡng bind từ "BookingAbuse" (Infrastructure đăng ký options).
        services.AddScoped(sp => new BookingAbuseGuard(
            sp.GetRequiredService<IBookingAbuseQueries>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetService<BookingService.Domain.Rules.BookingAbuseOptions>()));
        // UC-20: danh sách booking trong ca trực cho Staff App (US-076).
        services.AddScoped<IListGateBookingsUseCase, ListGateBookingsUseCase>();
        services.AddScoped<IProcessPaymentResultUseCase, ProcessPaymentResultUseCase>();
        // T-303: Tra cứu lịch sử duyệt đơn bãi Mức 0 (Owner / Admin Portal).
        services.AddScoped<IGetApprovalLogsUseCase, GetApprovalLogsUseCase>();
        // US-066 (luật 4): đăng ký thuê bao có kiểm tra hạn mức A < Q.
        services.AddScoped<IRegisterMonthlyPassUseCase, RegisterMonthlyPassUseCase>();
        return services;
    }
}
