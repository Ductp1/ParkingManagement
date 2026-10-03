using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace VehicleService.Domain.Entities;

/// <summary>
/// [VehicleService] Xe trong Garage của tài xế – tối đa 10 xe/tài khoản (Đặc tả v3 §2.3). Module: TV2.
/// </summary>
public class Vehicle : BaseEntity, ISoftDelete
{
    public int UserId { get; set; }
    /// <summary>Biển số chuẩn hóa, không dấu: "51F12345".</summary>
    public string PlateNumber { get; set; } = string.Empty;
    /// <summary>Biển số hiển thị: "51F-123.45".</summary>
    public string PlateDisplay { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; } = VehicleType.Sedan;
    public FuelType FuelType { get; set; } = FuelType.Gasoline;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Color { get; set; }
    public int? LengthCm { get; set; }
    public int? WidthCm { get; set; }
    /// <summary>Chiều cao xe – so với trần bãi + 10 cm margin.</summary>
    public int HeightCm { get; set; }
    /// <summary>Xe ưu tiên khẩn cấp (cứu thương/cứu hỏa).</summary>
    public bool IsEmergencyVehicle { get; set; }
    public bool IsDefault { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

}
