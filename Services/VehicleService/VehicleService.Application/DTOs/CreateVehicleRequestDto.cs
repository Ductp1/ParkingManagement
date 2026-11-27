using ParkingManagement.SharedKernel.Enums;

namespace VehicleService.Application.DTOs;

/// <summary>
/// Request DTO nhận dữ liệu thêm xe mới từ Client (US-009, US-010).
/// </summary>
public sealed record CreateVehicleRequestDto(
    int UserId,
    string PlateNumber,
    VehicleType VehicleType = VehicleType.Sedan,
    FuelType FuelType = FuelType.Gasoline,
    string? Brand = null,
    string? Model = null,
    string? Color = null,
    int? HeightCm = null,
    int? LengthCm = null,
    int? WidthCm = null,
    bool IsDefault = false
);
