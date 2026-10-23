using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.AddScoped<IComplaintService, ComplaintAppService>();
        services.AddScoped<IReviewService, ReviewAppService>();
        // ComplaintAppService cần giờ hệ thống. AddServiceDefaults() đã đăng ký sẵn; TryAdd chỉ thêm khi chưa có (không trùng).
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
