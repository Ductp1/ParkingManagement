using Microsoft.Extensions.DependencyInjection;
using ParkingService.Application.Features.Capacity;
using ParkingService.Application.Features.OperatingHours;
using ParkingService.Application.Features.Floors;
using ParkingService.Application.Features.Kyb;
using ParkingService.Application.Features.Layouts;
using ParkingService.Application.Features.KybMisdeclaration;
using ParkingService.Application.Features.KybReview;
using ParkingService.Application.Features.ParkingLots;
using ParkingService.Application.Features.LotDetails;
using ParkingService.Application.Features.Slots;
using ParkingService.Application.Features.Zones;

using ParkingService.Application.Features.LotOperations;

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
        // US-068: operations (heartbeat)
        // (IGetOperationsOverviewUseCase sẽ đăng ký ở Infrastructure khi thêm US-067 — cần bind StalenessOptions từ config.)
        services.AddScoped<IRecordHeartbeatUseCase, RecordHeartbeatUseCase>();

        return services;
    }
}
