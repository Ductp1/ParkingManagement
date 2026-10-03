using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Domain.Entities;

/// <summary>
/// [GateService] Nhật ký sự kiện tại cổng: xe tới, OCR lỗi, sửa biển số tay, mở barie khẩn cấp...
/// Dùng làm bằng chứng khi tranh chấp và để phát hiện gian lận / leakage.
/// </summary>
public class GateEvent : BaseEntity
{
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    public int? ParkingSessionId { get; set; }
    public int? ShiftId { get; set; }
    public GateEventType EventType { get; set; }
    public string? PlateNumber { get; set; }
    public string? ImagePath { get; set; }
    public string? OcrRaw { get; set; }
    public double? OcrConfidence { get; set; }
    /// <summary>Staff thao tác; null = hệ thống tự động.</summary>
    public int? PerformedByUserId { get; set; }
    public string? Note { get; set; }

    public ParkingSession? ParkingSession { get; set; }
    public Shift? Shift { get; set; }
}
