namespace PaymentService.Application.Features.Invoices;

/// <summary>
/// Cổng ca sử dụng phát hành hóa đơn điện tử phí đỗ xe cho tài xế (US-036).
/// </summary>
public interface IIssueParkingInvoiceUseCase
{
    /// <summary>Phát hành hóa đơn điện tử phí gửi xe sau khi giao dịch thanh toán hoàn tất.</summary>
    Task<InvoiceDto> ExecuteAsync(IssueParkingInvoiceCommand command, CancellationToken cancellationToken = default);
}

