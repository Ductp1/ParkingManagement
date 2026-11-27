using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Domain.Entities;

/// <summary>
/// [GateService] Sự kiện PMS (UC-51 E1, US-052) không khớp booking/dữ liệu nền tảng – đưa vào hàng chờ
/// để Staff xử lý tay. Lưu đủ payload gốc để Staff tra cứu và đối soát với PMS.
/// </summary>
public class PmsUnmatchedEvent : BaseEntity
{
    /// <summary>EventId do PMS sinh (idempotency) – trùng EventId đang Pending thì cập nhật thay vì thêm mới.</summary>
    public Guid EventId { get; set; }
    public int ParkingLotId { get; set; }
    public PmsEventType EventType { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string? BookingCode { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    /// <summary>Payload JSON gốc của sự kiện (gateId, imageUrl...) để đối soát.</summary>
    public string PayloadJson { get; set; } = "{}";
    /// <summary>Lý do sự kiện không khớp (không tìm thấy booking, booking sai bãi/trạng thái...).</summary>
    public string Reason { get; set; } = string.Empty;
    public PmsQuarantineStatus Status { get; set; } = PmsQuarantineStatus.Pending;
    public int? HandledByUserId { get; set; }
    public DateTime? HandledAtUtc { get; set; }
    public string? Note { get; set; }
}