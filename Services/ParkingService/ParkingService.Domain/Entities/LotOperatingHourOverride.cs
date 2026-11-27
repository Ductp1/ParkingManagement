using ParkingManagement.SharedKernel.Domain;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Lịch hoạt động ngoại lệ theo tuần và năm cụ thể của bãi xe (US-062).
/// Ghi đè lên lịch định kỳ chuẩn cho tuần được chỉ định (Year + WeekOfYear).
/// </summary>
public class LotOperatingHourOverride : BaseEntity
{
    public int ParkingLotId { get; set; }
    public int Year { get; set; }
    public int WeekOfYear { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public bool IsClosed { get; set; }
    public string? Reason { get; set; }

    public ParkingLot ParkingLot { get; set; } = null!;
}
