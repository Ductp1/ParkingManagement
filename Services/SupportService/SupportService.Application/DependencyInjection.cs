using Microsoft.Extensions.DependencyInjection;
using SupportService.Application.Features;
using SupportService.Application.Interfaces;
using SupportService.Application.Services;

namespace SupportService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSupportApplication(this IServiceCollection services)
    {
        services.AddScoped<IListOwnerComplaintsUseCase, ListOwnerComplaintsUseCase>();
        services.AddScoped<IGetLotReviewsUseCase, GetLotReviewsUseCase>();
        services.AddScoped<IFaqService, FaqAppService>();
        return services;
    }
}
