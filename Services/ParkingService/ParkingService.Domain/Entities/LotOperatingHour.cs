using ParkingManagement.SharedKernel.Domain;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Lịch hoạt động theo từng thứ trong tuần (US-062). Không có dòng cho 1 thứ = dùng OpenTime/CloseTime của bãi.
/// OpenTime == CloseTime nghĩa là mở 24 giờ; OpenTime &gt; CloseTime nghĩa là mở qua đêm.
/// </summary>
public class LotOperatingHour : BaseEntity
{
    public int ParkingLotId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public bool IsClosed { get; set; }

    public ParkingLot ParkingLot { get; set; } = null!;
}
