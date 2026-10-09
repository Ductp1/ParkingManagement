using Microsoft.Extensions.DependencyInjection;
using ParkingService.Application.Features.Floors;
using ParkingService.Application.Features.ParkingLots;
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

        return services;
    }
}
