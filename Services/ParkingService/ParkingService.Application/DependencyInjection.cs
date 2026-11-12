using Microsoft.Extensions.DependencyInjection;
using ParkingService.Application.Features.Capacity;
using ParkingService.Application.Features.Closures;
using ParkingService.Application.Features.OperatingHours;
using ParkingService.Application.Features.Floors;
using ParkingService.Application.Features.Integrations;
using ParkingService.Application.Features.Kyb;
using ParkingService.Application.Features.Layouts;
using ParkingService.Application.Features.LotHeartbeat;
using ParkingService.Application.Features.KybMisdeclaration;
using ParkingService.Application.Features.KybReview;
using ParkingService.Application.Features.ParkingLots;
using ParkingService.Application.Features.LotDetails;
using ParkingService.Application.Features.SanctionStates;
using ParkingService.Application.Features.PhysicalChanges;
using ParkingService.Application.Features.Slots;
using ParkingService.Application.Features.Zones;

namespace ParkingService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddParkingApplication(this IServiceCollection services)
    {
        // Parking Lots
        services.AddScoped<IGetParkingLotByIdUseCase, GetParkingLotByIdUseCase>();
        services.AddScoped<ISearchParkingLotsUseCase, SearchParkingLotsUseCase>();
        services.AddScoped<ICreateParkingLotUseCase, CreateParkingLotUseCase>();
        services.AddScoped<IUpdateParkingLotUseCase, UpdateParkingLotUseCase>();
        services.AddScoped<IGetOwnerParkingLotsUseCase, GetOwnerParkingLotsUseCase>();
        services.AddScoped<IGetParkingLotHierarchyUseCase, GetParkingLotHierarchyUseCase>();

        // KYB Application (US-054)
        services.AddScoped<IGetKybApplicationUseCase, GetKybApplicationUseCase>();
        services.AddScoped<ISaveKybDraftUseCase, SaveKybDraftUseCase>();
        services.AddScoped<ISubmitKybApplicationUseCase, SubmitKybApplicationUseCase>();

        // KYB Review – Admin (US-055)
        services.AddScoped<IListAdminKybApplicationsUseCase, ListAdminKybApplicationsUseCase>();
        services.AddScoped<IGetAdminKybApplicationUseCase, GetAdminKybApplicationUseCase>();
        services.AddScoped<IStartKybReviewUseCase, StartKybReviewUseCase>();
        services.AddScoped<IScheduleKybFieldSurveyUseCase, ScheduleKybFieldSurveyUseCase>();
        services.AddScoped<IRecordKybFieldSurveyResultUseCase, RecordKybFieldSurveyResultUseCase>();
        services.AddScoped<IApproveKybApplicationUseCase, ApproveKybApplicationUseCase>();
        services.AddScoped<IRejectKybApplicationUseCase, RejectKybApplicationUseCase>();
        services.AddScoped<IRequestKybMoreInfoUseCase, RequestKybMoreInfoUseCase>();
        services.AddScoped<Features.AdminDashboard.IGetAdminLotsSummaryUseCase, Features.AdminDashboard.GetAdminLotsSummaryUseCase>(); // US-081

        // Khai báo sai – Admin (US-056)
        services.AddScoped<IGetLotMisdeclarationUseCase, GetLotMisdeclarationUseCase>();
        services.AddScoped<IOpenLotMisdeclarationCaseUseCase, OpenLotMisdeclarationCaseUseCase>();
        services.AddScoped<IEscalateLotMisdeclarationCaseUseCase, EscalateLotMisdeclarationCaseUseCase>();
        services.AddScoped<IResolveLotMisdeclarationCaseUseCase, ResolveLotMisdeclarationCaseUseCase>();
        services.AddScoped<IBanLotOwnerUseCase, BanLotOwnerUseCase>();

        // Chế tài SLA – hiệu lực đẩy từ AdminService (US-097)
        services.AddScoped<IUpsertSanctionStateUseCase, UpsertSanctionStateUseCase>();
        services.AddScoped<ILiftSanctionStateUseCase, LiftSanctionStateUseCase>();
        services.AddScoped<IGetLotSanctionStatusUseCase, GetLotSanctionStatusUseCase>();
        services.AddScoped<IGetLotSanctionStateUseCase, GetLotSanctionStateUseCase>();

        // Zones
        services.AddScoped<ICreateZoneUseCase, CreateZoneUseCase>();
        services.AddScoped<IUpdateZoneUseCase, UpdateZoneUseCase>();
        services.AddScoped<IDeleteZoneUseCase, DeleteZoneUseCase>();
        services.AddScoped<IGetZonesByLotUseCase, GetZonesByLotUseCase>();

        // Floors
        services.AddScoped<ICreateFloorUseCase, CreateFloorUseCase>();
        services.AddScoped<IUpdateFloorUseCase, UpdateFloorUseCase>();
        services.AddScoped<IDeleteFloorUseCase, DeleteFloorUseCase>();
        services.AddScoped<IGetFloorsByZoneUseCase, GetFloorsByZoneUseCase>();

        // Slots
        services.AddScoped<ICreateSlotUseCase, CreateSlotUseCase>();
        services.AddScoped<IBatchCreateSlotsUseCase, BatchCreateSlotsUseCase>();
        services.AddScoped<IUpdateSlotUseCase, UpdateSlotUseCase>();
        services.AddScoped<IDeleteSlotUseCase, DeleteSlotUseCase>();
        services.AddScoped<IGetSlotsByFloorUseCase, GetSlotsByFloorUseCase>();
        services.AddScoped<IOverrideSlotStateUseCase, OverrideSlotStateUseCase>(); // US-039/US-049

        // US-052: tích hợp PMS Mức 1
        services.AddScoped<IVerifyPmsApiKeyUseCase, VerifyPmsApiKeyUseCase>();
        services.AddScoped<IPushBookingToPmsUseCase, PushBookingToPmsUseCase>();

        // Layouts (T-502, US-059, US-060)
        services.AddScoped<IGetFloorLayoutUseCase, GetFloorLayoutUseCase>();
        services.AddScoped<IGetFloorLayoutDraftUseCase, GetFloorLayoutDraftUseCase>();
        services.AddScoped<ISaveFloorLayoutDraftUseCase, SaveFloorLayoutDraftUseCase>();
        services.AddScoped<IPublishFloorLayoutUseCase, PublishFloorLayoutUseCase>();
        services.AddScoped<IGetFloorLayoutVersionsUseCase, GetFloorLayoutVersionsUseCase>();
        services.AddScoped<IGetFloorLayoutByVersionUseCase, GetFloorLayoutByVersionUseCase>();
        services.AddScoped<IRollbackFloorLayoutUseCase, RollbackFloorLayoutUseCase>();
        services.AddScoped<IGetFloorLayoutDiffUseCase, GetFloorLayoutDiffUseCase>();
        services.AddScoped<IValidateFloorLayoutSafetyUseCase, ValidateFloorLayoutSafetyUseCase>();
        services.AddScoped<IDiscardFloorLayoutDraftUseCase, DiscardFloorLayoutDraftUseCase>();

        // Capacity & Walk-in Buffer (UI-26, EP10)
        services.AddScoped<IGetLotCapacityConfigUseCase, GetLotCapacityConfigUseCase>();
        services.AddScoped<IUpdateLotCapacityConfigUseCase, UpdateLotCapacityConfigUseCase>();

        // Closures & Maintenance (US-063, EP09)
        services.AddScoped<ICreateClosureScheduleUseCase, CreateClosureScheduleUseCase>();
        services.AddScoped<IValidateClosureImpactUseCase, ValidateClosureImpactUseCase>();
        services.AddScoped<ICancelClosureScheduleUseCase, CancelClosureScheduleUseCase>();
        services.AddScoped<IGetLotClosureSchedulesUseCase, GetLotClosureSchedulesUseCase>();
        services.AddScoped<IGetClosureScheduleByIdUseCase, GetClosureScheduleByIdUseCase>();

        // Lot Details (Amenities, Photos, Attributes) (US-064, EP09)
        services.AddScoped<IGetStandardAmenitiesCatalogUseCase, GetStandardAmenitiesCatalogUseCase>();
        services.AddScoped<IGetLotAmenitiesUseCase, GetLotAmenitiesUseCase>();
        services.AddScoped<IUpdateLotAmenitiesUseCase, UpdateLotAmenitiesUseCase>();
        services.AddScoped<IAddLotAmenityUseCase, AddLotAmenityUseCase>();
        services.AddScoped<IDeleteLotAmenityUseCase, DeleteLotAmenityUseCase>();

        services.AddScoped<IGetLotPhotosUseCase, GetLotPhotosUseCase>();
        services.AddScoped<IAddLotPhotoUseCase, AddLotPhotoUseCase>();
        services.AddScoped<IUpdateLotPhotoUseCase, UpdateLotPhotoUseCase>();
        services.AddScoped<ISetCoverPhotoUseCase, SetCoverPhotoUseCase>();
        services.AddScoped<IReorderLotPhotosUseCase, ReorderLotPhotosUseCase>();
        services.AddScoped<IDeleteLotPhotoUseCase, DeleteLotPhotoUseCase>();

        services.AddScoped<IGetLotAttributesUseCase, GetLotAttributesUseCase>();
        services.AddScoped<IUpdateLotAttributesUseCase, UpdateLotAttributesUseCase>();

        // Operating Hours (US-062, EP09)
        services.AddScoped<IGetLotOperatingHoursUseCase, GetLotOperatingHoursUseCase>();
        services.AddScoped<IUpdateLotOperatingHoursUseCase, UpdateLotOperatingHoursUseCase>();
        services.AddScoped<IDeleteLotOperatingHoursOverrideUseCase, DeleteLotOperatingHoursOverrideUseCase>();

        // Physical Changes (SLA 48h Protection) (US-061, EP09)
        services.AddScoped<IDeclarePhysicalChangeUseCase, DeclarePhysicalChangeUseCase>();
        services.AddScoped<IGetLotPhysicalChangesUseCase, GetLotPhysicalChangesUseCase>();
        services.AddScoped<IGetPhysicalChangeByIdUseCase, GetPhysicalChangeByIdUseCase>();
        services.AddScoped<ISendPhysicalChangeReminderUseCase, SendPhysicalChangeReminderUseCase>();
        services.AddScoped<ICompletePhysicalChangeUseCase, CompletePhysicalChangeUseCase>();
        services.AddScoped<ICheckPhysicalChangeSlaOverdueUseCase, CheckPhysicalChangeSlaOverdueUseCase>();
        services.AddScoped<IRecordPhysicalChangeBookingErrorUseCase, RecordPhysicalChangeBookingErrorUseCase>();
        services.AddScoped<ICancelPhysicalChangeUseCase, CancelPhysicalChangeUseCase>();
        services.AddScoped<IGetPhysicalChangeSlaSummaryUseCase, GetPhysicalChangeSlaSummaryUseCase>();
        // Owner Platform Exit (US-057, EP08)
        services.AddScoped<IGetOwnerExitImpactUseCase, GetOwnerExitImpactUseCase>();
        services.AddScoped<ISuspendOwnerLotsUseCase, SuspendOwnerLotsUseCase>();

        // Heartbeat & Stale Data (US-068) – settings singleton đăng ký ở Infrastructure DI (cần IConfiguration)
        services.AddScoped<IRecordLotHeartbeatUseCase, RecordLotHeartbeatUseCase>();
        services.AddScoped<IGetLotFreshnessUseCase, GetLotFreshnessUseCase>();

        return services;
    }
}
