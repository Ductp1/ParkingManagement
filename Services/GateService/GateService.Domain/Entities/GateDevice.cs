using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Domain.Entities;

/// <summary>
/// [GateService] Thiết bị tại cổng: camera ANPR / webcam, barie, kiosk, máy quét QR, cảm biến ô đỗ (Kiến trúc v3 §2.1 Mức 2, US-050, US-051).
/// Theo dõi thiết bị online/offline để chuyển sang Offline Fallback và để biết sự kiện nào đến từ thiết bị nào.
/// </summary>
public class GateDevice : BaseEntity
{
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public GateDeviceType DeviceType { get; set; }
    /// <summary>"Entry" / "Exit" / mã slot với cảm biến ô đỗ.</summary>
    public string? Position { get; set; }
    public GateDeviceStatus Status { get; set; } = GateDeviceStatus.Online;
    public DateTime? LastSeenAtUtc { get; set; }
    public string? FirmwareVersion { get; set; }
    /// <summary>Khóa công khai để xác thực QR ký số khi mất mạng (Offline Fallback, RSA-2048).</summary>
    public string? OfflinePublicKey { get; set; }
    public bool IsActive { get; set; } = true;
}
