using ParkingManagement.SharedKernel.Domain;

namespace GateService.Domain.Entities;

/// <summary>
/// [GateService] Cấu hình sức chứa US-066 của 1 bãi (1-1 ParkingLot):
/// C = Capacity (tổng chỗ), B = TransientBuffer (số chỗ TỐI ĐA vãng lai chiếm cùng lúc),
/// Q = SubscriberQuota (hạn mức thuê bao hiệu lực tối đa). Phần C - B - Q là vùng linh hoạt dùng chung.
/// Version tăng mỗi lần admin lưu (optimistic concurrency cho API quản trị).
/// Runtime counters (T, V) nằm ở LotAdmissionCounter; A (thuê bao hiệu lực) thuộc BookingService.
/// </summary>
public class LotCapacityConfig : BaseEntity
{
    public int ParkingLotId { get; set; }
    public int Capacity { get; set; }
    public int TransientBuffer { get; set; }
    public int SubscriberQuota { get; set; }
    public long Version { get; set; } = 1;
    public int? LastChangedByUserId { get; set; }
}