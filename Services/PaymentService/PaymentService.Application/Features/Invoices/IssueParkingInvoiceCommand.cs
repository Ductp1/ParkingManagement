namespace PaymentService.Application.Features.Invoices;

/// <summary>
/// DTO gửi lên để yêu cầu phát hành hóa đơn điện tử cho giao dịch phí đỗ xe (US-036 AC1, AC2).
/// </summary>
public sealed record IssueParkingInvoiceCommand(
    int PaymentId,
    string BuyerName,
    string? BuyerTaxCode = null,
    string? BuyerEmail = null,
    decimal VatRate = 0.10m);

