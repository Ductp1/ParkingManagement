using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// [PaymentService] Hóa đơn điện tử (US-036, UC-19). 2 loại:
///  - ParkingFee: hóa đơn phí đỗ cho tài xế, gắn với 1 Payment.
///  - PlatformCommission: hóa đơn hoa hồng nền tảng xuất cho chủ bãi, gắn với 1 kỳ quyết toán (Settlement).
/// </summary>
public class Invoice : BaseEntity
{
    public InvoiceType Type { get; set; } = InvoiceType.ParkingFee;
    public int? PaymentId { get; set; }
    public int? SettlementId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string? BuyerTaxCode { get; set; }
    public string? BuyerEmail { get; set; }
    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public string? PdfUrl { get; set; }

    public Payment? Payment { get; set; }
    public Settlement? Settlement { get; set; }
}
