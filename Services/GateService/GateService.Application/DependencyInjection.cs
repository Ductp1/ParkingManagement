using GateService.Application.Features;
using GateService.Application.Features.GateConsole;
using GateService.Application.Features.GateDevices;
using GateService.Application.Features.GateExceptions;
using GateService.Application.Features.PlateRecognition;
using GateService.Application.Features.PmsIntegration;
using GateService.Application.Features.StaffCancellations;
using GateService.Application.Features.LotCapacity;
using Microsoft.Extensions.DependencyInjection;

namespace GateService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddGateApplication(this IServiceCollection services)
    {
        services.AddScoped<IListLotSessionsUseCase, ListLotSessionsUseCase>();
        services.AddScoped<ILookupVehicleAtGateUseCase, LookupVehicleAtGateUseCase>();
        // T-702: Gate Console
        services.AddScoped<ICheckInUseCase, CheckInUseCase>();
        services.AddScoped<ICheckOutUseCase, CheckOutUseCase>();
        // UC-20/UC-22: danh sách booking trong ca + chốt phí cổng ra
        services.AddScoped<IListShiftBookingsUseCase, ListShiftBookingsUseCase>();
        services.AddScoped<IQuoteExitFeeUseCase, QuoteExitFeeUseCase>();
        // T-704: xác thực QR token do BookingService cấp (thuần crypto, không depends Infrastructure).
        services.AddSingleton<IBookingQrTokenValidator, BookingQrTokenValidator>();
        // T-703: GateDevices
        services.AddScoped<IGetGateDevicesUseCase, GetGateDevicesUseCase>();
        services.AddScoped<IGetGateDeviceByIdUseCase, GetGateDeviceByIdUseCase>();
        services.AddScoped<ICreateGateDeviceUseCase, CreateGateDeviceUseCase>();
        services.AddScoped<IUpdateGateDeviceUseCase, UpdateGateDeviceUseCase>();
        services.AddScoped<IDeleteGateDeviceUseCase, DeleteGateDeviceUseCase>();
        services.AddScoped<IRecordGateDeviceHeartbeatUseCase, RecordGateDeviceHeartbeatUseCase>();
        // US-039: Staff xem ứng viên No-show + hủy No-show sau 30 phút (UC-24).
        services.AddScoped<IListNoShowCandidatesUseCase, ListNoShowCandidatesUseCase>();
        services.AddScoped<ICancelNoShowUseCase, CancelNoShowUseCase>();
        // US-048: xử lý ngoại lệ thủ công tại cổng (UC-23).
        services.AddScoped<IRecordGateExceptionUseCase, RecordGateExceptionUseCase>();
        services.AddScoped<IListGateExceptionsUseCase, ListGateExceptionsUseCase>();
        // US-049: Staff ghi đè trạng thái slot khi cảm biến hỏng (UC-25).
        services.AddScoped<IOverrideSlotStatusUseCase, OverrideSlotStatusUseCase>();
        // US-052: sự kiện PMS vào/ra + hàng chờ xử lý tay (UC-51).
        services.AddScoped<IProcessPmsEventUseCase, ProcessPmsEventUseCase>();
        services.AddScoped<IListQuarantinedPmsEventsUseCase, ListQuarantinedPmsEventsUseCase>();
        services.AddScoped<IResolveQuarantinedPmsEventUseCase, ResolveQuarantinedPmsEventUseCase>();
        // US-077: thống kê hủy No-show theo Staff.
        services.AddScoped<IGetStaffCancellationStatsUseCase, GetStaffCancellationStatsUseCase>();
        services.AddScoped<IStaffCancellationMonitor, StaffCancellationMonitor>();
        // US-066: cau hinh suc chua & vung dem vang lai.
        services.AddScoped<IGetLotCapacityConfigUseCase, GetLotCapacityConfigUseCase>();
        services.AddScoped<IUpdateLotCapacityConfigUseCase, UpdateLotCapacityConfigUseCase>();
        Features.AdminDashboard.AdminGateDashboardRegistration.AddAdminGateDashboard(services); // US-081: Admin dashboard (chỉ đọc)
        return services;
    }
}

