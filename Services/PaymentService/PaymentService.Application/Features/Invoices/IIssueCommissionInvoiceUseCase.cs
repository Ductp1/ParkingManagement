namespace PaymentService.Application.Features.Invoices;

/// <summary>
/// Cổng ca sử dụng phát hành hóa đơn hoa hồng sàn cho chủ bãi xe (US-036).
/// </summary>
public interface IIssueCommissionInvoiceUseCase
{
    /// <summary>Phát hành hóa đơn hoa hồng nền tảng dựa trên kỳ quyết toán doanh thu của chủ bãi.</summary>
    Task<InvoiceDto> ExecuteAsync(IssueCommissionInvoiceCommand command, CancellationToken cancellationToken = default);
}

