using Microsoft.Extensions.DependencyInjection;
using SupportService.Application.Features;

namespace SupportService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSupportApplication(this IServiceCollection services)
    {
        services.AddScoped<IListOwnerComplaintsUseCase, ListOwnerComplaintsUseCase>();
        services.AddScoped<IGetLotReviewsUseCase, GetLotReviewsUseCase>();
        return services;
    }
}
