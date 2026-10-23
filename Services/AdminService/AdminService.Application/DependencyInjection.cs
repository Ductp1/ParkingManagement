using AdminService.Application.Features;
using AdminService.Application.Features.ContractTemplates;
using AdminService.Application.Features.LotContracts;
using AdminService.Application.Features.Owners;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Application.Features.Settings;
using AdminService.Application.Features.Users;
using Microsoft.Extensions.DependencyInjection;

namespace AdminService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetPlatformSettingsUseCase, GetPlatformSettingsUseCase>();
        // US-098: đọc tham số theo khóa (giá trị hiệu lực theo thời điểm)
        services.AddScoped<IGetSystemConfigByKeyUseCase, GetSystemConfigByKeyUseCase>();
        // US-098: đổi giá trị tham số có Effective Date
        services.AddScoped<IUpdateSystemConfigUseCase, UpdateSystemConfigUseCase>();
        // US-098: lịch sử thay đổi + hủy thay đổi chưa hiệu lực
        services.AddScoped<IGetSystemConfigHistoryUseCase, GetSystemConfigHistoryUseCase>();
        services.AddScoped<ICancelSystemConfigChangeUseCase, CancelSystemConfigChangeUseCase>();
        // US-096: tra cứu người dùng (đọc qua UserService)
        services.AddScoped<IListAdminUsersUseCase, ListAdminUsersUseCase>();
        services.AddScoped<IGetAdminUserByIdUseCase, GetAdminUserByIdUseCase>();
        // US-096: lịch sử vi phạm của chủ bãi
        services.AddScoped<IGetOwnerSanctionsUseCase, GetOwnerSanctionsUseCase>();
        // US-096: khóa / mở khóa chủ bãi
        services.AddScoped<ILockOwnerUseCase, LockOwnerUseCase>();
        services.AddScoped<IUnlockOwnerUseCase, UnlockOwnerUseCase>();
        // US-116: Partner Onboarding – Admin xem hồ sơ đối tác
        services.AddScoped<IListPartnerApplicationsUseCase, ListPartnerApplicationsUseCase>();
        services.AddScoped<IGetPartnerApplicationUseCase, GetPartnerApplicationUseCase>();
        // US-116: thẩm định – bắt đầu xem xét, yêu cầu bổ sung, từ chối, hủy
        services.AddScoped<IStartPartnerReviewUseCase, StartPartnerReviewUseCase>();
        services.AddScoped<IRequestPartnerInfoUseCase, RequestPartnerInfoUseCase>();
        services.AddScoped<IRejectPartnerApplicationUseCase, RejectPartnerApplicationUseCase>();
        services.AddScoped<ICancelPartnerApplicationUseCase, CancelPartnerApplicationUseCase>();
        // US-116: cấp tài khoản chủ bãi qua UserService (kèm tài khoản nhận tiền), gửi lại link kích hoạt
        services.AddScoped<IIssueOwnerAccountUseCase, IssueOwnerAccountUseCase>();
        services.AddScoped<IResendOwnerActivationUseCase, ResendOwnerActivationUseCase>();
        // US-116: hợp đồng theo từng bãi (sau KYB) – lập, xem trước, sửa, tải file, ký (kích hoạt bãi), hủy
        services.AddScoped<IListLotContractsUseCase, ListLotContractsUseCase>();
        services.AddScoped<IGetLotContractUseCase, GetLotContractUseCase>();
        services.AddScoped<IGetLotContractFileUseCase, GetLotContractFileUseCase>();
        services.AddScoped<IPreviewNewLotContractUseCase, PreviewNewLotContractUseCase>();
        services.AddScoped<IPreviewLotContractRevisionUseCase, PreviewLotContractRevisionUseCase>();
        services.AddScoped<ICreateLotContractUseCase, CreateLotContractUseCase>();
        services.AddScoped<IReviseLotContractUseCase, ReviseLotContractUseCase>();
        services.AddScoped<ISignLotContractUseCase, SignLotContractUseCase>();
        services.AddScoped<ICancelLotContractUseCase, CancelLotContractUseCase>();
        // US-116: mẫu hợp đồng bãi (loại hợp đồng) – tạo / nhập DOCX, form, xem trước, áp dụng, đổi tên, thôi áp dụng
        services.AddScoped<LotContractDocumentBuilder>();
        services.AddScoped<IListContractTemplatesUseCase, ListContractTemplatesUseCase>();
        services.AddScoped<IGetContractTemplateUseCase, GetContractTemplateUseCase>();
        services.AddScoped<IGetContractPlaceholdersUseCase, GetContractPlaceholdersUseCase>();
        services.AddScoped<IPreviewContractTemplateUseCase, PreviewContractTemplateUseCase>();
        services.AddScoped<ICreateContractTemplateUseCase, CreateContractTemplateUseCase>();
        services.AddScoped<IImportContractTemplateUseCase, ImportContractTemplateUseCase>();
        services.AddScoped<IRenameContractTemplateUseCase, RenameContractTemplateUseCase>();
        services.AddScoped<IRetireContractTemplateUseCase, RetireContractTemplateUseCase>();
        services.AddScoped<IConvertContractTemplateUseCase, ConvertContractTemplateUseCase>();
        services.AddScoped<IGetContractTemplateSourceFileUseCase, GetContractTemplateSourceFileUseCase>();
        services.AddScoped<IUpdateContractTemplateDraftUseCase, UpdateContractTemplateDraftUseCase>();
        services.AddScoped<IActivateContractTemplateUseCase, ActivateContractTemplateUseCase>();
        services.AddScoped<IDiscardContractTemplateDraftUseCase, DiscardContractTemplateDraftUseCase>();
        return services;
    }
}
