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
        // US-066 (luật 4): dang ky thue bao co kiem tra han muc A < Q.
        services.AddScoped<IRegisterMonthlyPassUseCase, RegisterMonthlyPassUseCase>();
        return services;
    }
}
