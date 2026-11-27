using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Application.Features.Invoices;

/// <summary>
/// DTO chứa đầy đủ thông tin chi tiết của một hóa đơn điện tử (US-036).
/// </summary>
public sealed record InvoiceDto(
    int Id,
    InvoiceType Type,
    string InvoiceNumber,
    int? PaymentId,
    int? BookingId,
    int? SettlementId,
    string BuyerName,
    string? BuyerTaxCode,
    string? BuyerEmail,
    decimal SubTotal,
    decimal VatAmount,
    decimal Total,
    DateTime IssuedAtUtc,
    string? PdfUrl,
    string Status);

