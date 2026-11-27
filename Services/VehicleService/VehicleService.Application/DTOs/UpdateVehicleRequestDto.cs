using ParkingManagement.SharedKernel.Enums;

namespace VehicleService.Application.DTOs;

/// <summary>
/// DTO cập nhật thông tin xe trong Garage (Task P0.4, US-011).
/// Lưu ý: Không cho phép sửa PlateNumber (biển số xe là bất biến).
/// </summary>
public sealed record UpdateVehicleRequestDto(
    int UserId,
    VehicleType VehicleType,
    FuelType FuelType,
    string? Brand = null,
    string? Model = null,
    string? Color = null,
    int? HeightCm = null,
    int? LengthCm = null,
    int? WidthCm = null
);
