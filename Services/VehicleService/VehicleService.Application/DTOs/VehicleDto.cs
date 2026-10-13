using ParkingManagement.SharedKernel.Enums;

namespace VehicleService.Application.DTOs;

/// <summary>
/// Response DTO trả về cho Client hiển thị thông tin xe.
/// </summary>
public sealed record VehicleDto(
    int Id,
    int UserId,
    string PlateNumber,
    string PlateDisplay,
    VehicleType VehicleType,
    FuelType FuelType,
    string? Brand,
    string? Model,
    string? Color,
    int HeightCm,
    bool IsDefault,
    int? LengthCm = null,
    int? WidthCm = null,
    bool IsEmergencyVehicle = false
);
