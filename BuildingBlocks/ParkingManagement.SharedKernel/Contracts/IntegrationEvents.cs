namespace ParkingManagement.SharedKernel.Contracts;

/// <summary>
/// HỢP ĐỒNG SỰ KIỆN giữa các service. Service phát ghi sự kiện vào bảng OutboxMessages trong database của mình
/// (cùng transaction với dữ liệu nghiệp vụ), worker đọc Outbox và gửi cho service nghe.
/// Đổi tên / xóa trường ở đây là thay đổi phá vỡ → phải báo cả nhóm.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}

// ===== UserService =====
public sealed record UserRegistered(int UserId, string FullName, string? Email, string? PhoneNumber) : IntegrationEvent;
public sealed record UserLocked(int UserId, string Reason) : IntegrationEvent;
public sealed record OwnerProfileCreated(int OwnerProfileId, int UserId, string BusinessName) : IntegrationEvent;
public sealed record UserPersonalDataErased(int UserId) : IntegrationEvent;

// ===== VehicleService =====
public sealed record VehicleRegistered(int VehicleId, int UserId, string PlateNumber, string VehicleType, int HeightCm) : IntegrationEvent;

// ===== ParkingService =====
public sealed record ParkingLotApproved(int ParkingLotId, int OwnerProfileId, string Name) : IntegrationEvent;
public sealed record SlotStateChanged(int ParkingLotId, int SlotId, string SlotCode, string OldState, string NewState) : IntegrationEvent;
public sealed record LayoutPublished(int ParkingLotId, int FloorId, int VersionNo) : IntegrationEvent;

// ===== BookingService =====
public sealed record BookingCreated(int BookingId, string Code, int UserId, int ParkingLotId, int? SlotId, decimal TotalAmount, DateTime HoldExpiresAtUtc) : IntegrationEvent;
public sealed record BookingConfirmed(int BookingId, string Code, int UserId, int ParkingLotId, int? SlotId) : IntegrationEvent;
public sealed record BookingCancelled(int BookingId, string Code, int UserId, decimal RefundAmount, string Reason) : IntegrationEvent;
public sealed record BookingCompleted(int BookingId, string Code, int ParkingLotId, int OwnerProfileId, decimal TotalAmount) : IntegrationEvent;

// ===== GateService =====
public sealed record VehicleCheckedIn(int ParkingSessionId, int ParkingLotId, int? BookingId, int? SlotId, string PlateNumber) : IntegrationEvent;
public sealed record VehicleCheckedOut(int ParkingSessionId, int ParkingLotId, int? BookingId, int? SlotId, string PlateNumber, decimal Fee) : IntegrationEvent;

// ===== PaymentService =====
public sealed record PaymentSucceeded(int PaymentId, int? BookingId, int? ParkingSessionId, decimal Amount, string Method) : IntegrationEvent;
public sealed record PaymentFailed(int PaymentId, int? BookingId, string Reason) : IntegrationEvent;
public sealed record RefundIssued(int RefundId, int PaymentId, int? BookingId, decimal Amount) : IntegrationEvent;

// ===== SupportService =====
/// <summary>OwnerProfileId = null khi chưa xác định được chủ bãi → chỉ thông báo cho đội Support.</summary>
public sealed record ComplaintCreated(int ComplaintId, string Code, int ParkingLotId, int? OwnerProfileId) : IntegrationEvent;
/// <summary>US-089: chủ bãi đã phản hồi tranh chấp → báo cho tài xế (UserId).</summary>
public sealed record ComplaintOwnerResponded(int ComplaintId, string Code, int UserId, int ParkingLotId) : IntegrationEvent;
/// <summary>US-089 AC2: quá 48 giờ chủ bãi không phản hồi → chuyển nền tảng (Supervisor/Admin) xử lý.</summary>
public sealed record ComplaintEscalated(int ComplaintId, string Code, int UserId, int ParkingLotId, int? OwnerProfileId, string Reason) : IntegrationEvent;
public sealed record ReviewCreated(int ReviewId, int ParkingLotId, byte Rating) : IntegrationEvent;
/// <summary>US-094 AC1: chủ bãi phản hồi đánh giá → báo cho người viết đánh giá (UserId).</summary>
public sealed record ReviewOwnerReplied(int ReviewId, int ParkingLotId, int UserId) : IntegrationEvent;
/// <summary>US-094 AC2: Admin gỡ đánh giá → ParkingService trừ đánh giá này khỏi điểm xếp hạng của bãi.</summary>
public sealed record ReviewHidden(int ReviewId, int ParkingLotId, byte Rating) : IntegrationEvent;

// ===== Mọi service → AdminService =====
public sealed record AuditRecorded(string SourceService, int? UserId, string Action, string EntityName, string? EntityId,
    string? OldValuesJson, string? NewValuesJson, string? Reason) : IntegrationEvent;
