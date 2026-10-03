using BookingService.Application.Features.Bookings;
using Microsoft.Extensions.DependencyInjection;

namespace BookingService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBookingApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetBookingByCodeUseCase, GetBookingByCodeUseCase>();
        services.AddScoped<IListMyBookingsUseCase, ListMyBookingsUseCase>();
        services.AddScoped<IPreviewCancellationUseCase, PreviewCancellationUseCase>();
        return services;
    }
}
