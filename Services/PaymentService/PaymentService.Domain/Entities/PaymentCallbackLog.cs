using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// [PaymentService] Log TỪNG lần cổng thanh toán gọi callback/IPN (US-033, FR-PAY-010).
/// Callback có thể đến nhiều lần cho 1 giao dịch → giữ lại cả bản gốc để đối soát và chứng minh khi tranh chấp.
/// </summary>
public class PaymentCallbackLog : BaseEntity
{
    /// <summary>null khi không khớp được giao dịch nào (callback giả / sai mã).</summary>
    public int? PaymentId { get; set; }
    public PaymentMethod Provider { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string RawPayload { get; set; } = string.Empty;
    public bool SignatureValid { get; set; }
    /// <summary>Đã xử lý trước đó (trùng IdempotencyKey) → bỏ qua, không cộng tiền lần 2.</summary>
    public bool IsDuplicate { get; set; }
    public string? ResultCode { get; set; }
    public string? ProcessingError { get; set; }
    public string? SourceIp { get; set; }

    public Payment? Payment { get; set; }
}
