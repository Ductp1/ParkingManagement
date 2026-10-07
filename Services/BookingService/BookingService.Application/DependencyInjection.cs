using BookingService.Application.Features.Bookings;
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
        return services;
    }
}
