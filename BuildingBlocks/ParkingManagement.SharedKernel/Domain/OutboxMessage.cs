namespace ParkingManagement.SharedKernel.Domain;

/// <summary>
/// Transactional Outbox: mỗi service ghi sự kiện tích hợp (BookingConfirmed, PaymentSucceeded...)
/// vào bảng này CÙNG transaction với dữ liệu nghiệp vụ, sau đó một worker đọc và phát cho service khác.
/// Nhờ vậy các database tách rời vẫn đồng bộ được mà không cần transaction phân tán.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Tên sự kiện, VD "BookingConfirmed".</summary>
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
}
