namespace PaymentService.Application.Features.Invoices;

/// <summary>
/// DTO gửi lên để yêu cầu phát hành hóa đơn hoa hồng sàn cho chủ bãi xe theo kỳ quyết toán (US-036).
/// </summary>
public sealed record IssueCommissionInvoiceCommand(
    int SettlementId,
    string BuyerName,
    string? BuyerTaxCode = null,
    string? BuyerEmail = null,
    decimal VatRate = 0.10m);

