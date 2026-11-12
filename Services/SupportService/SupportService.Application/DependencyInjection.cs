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
        services.AddScoped<IOwnerDisputeService, OwnerDisputeService>();
        services.AddScoped<IComplaintEscalationService, ComplaintEscalationService>();
        services.AddScoped<IReviewModerationService, ReviewModerationService>();
        services.AddScoped<Features.AdminDashboard.IGetAdminComplaintSummaryUseCase, Features.AdminDashboard.GetAdminComplaintSummaryUseCase>(); // US-081
        services.AddScoped<ILotFullIncidentService, LotFullIncidentService>();   // US-091
        services.AddScoped<IDisputeResolutionService, DisputeResolutionService>();   // US-090
        services.AddScoped<ILotFineService, LotFineService>();                       // US-090: mức phạt đậu sai chỗ
        // ComplaintAppService cần giờ hệ thống. AddServiceDefaults() đã đăng ký sẵn; TryAdd chỉ thêm khi chưa có (không trùng).
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
