using GateService.Application.Features;
using GateService.Application.Features.GateConsole;
using GateService.Application.Features.GateDevices;
using GateService.Application.Features.PlateRecognition;
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
        // T-704: xác thực QR token do BookingService cấp (thuần crypto, không depends Infrastructure).
        services.AddSingleton<IBookingQrTokenValidator, BookingQrTokenValidator>();
        // T-703: GateDevices
        services.AddScoped<IGetGateDevicesUseCase, GetGateDevicesUseCase>();
        services.AddScoped<IGetGateDeviceByIdUseCase, GetGateDeviceByIdUseCase>();
        services.AddScoped<ICreateGateDeviceUseCase, CreateGateDeviceUseCase>();
        services.AddScoped<IUpdateGateDeviceUseCase, UpdateGateDeviceUseCase>();
        services.AddScoped<IDeleteGateDeviceUseCase, DeleteGateDeviceUseCase>();
        services.AddScoped<IRecordGateDeviceHeartbeatUseCase, RecordGateDeviceHeartbeatUseCase>();
        return services;
    }
}

