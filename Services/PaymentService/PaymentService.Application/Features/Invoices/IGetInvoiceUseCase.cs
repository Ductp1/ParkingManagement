namespace PaymentService.Application.Features.Invoices;

/// <summary>
/// Cổng ca sử dụng tra cứu hóa đơn điện tử theo nhiều tiêu chí (US-036 AC3).
/// </summary>
public interface IGetInvoiceUseCase
{
    /// <summary>Tra cứu hóa đơn theo ID khóa chính.</summary>
    Task<InvoiceDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Tra cứu hóa đơn theo giao dịch thanh toán (PaymentId).</summary>
    Task<InvoiceDto> GetByPaymentIdAsync(int paymentId, CancellationToken cancellationToken = default);

    /// <summary>Tra cứu hóa đơn theo mã đơn đặt chỗ (BookingId).</summary>
    Task<InvoiceDto> GetByBookingIdAsync(int bookingId, CancellationToken cancellationToken = default);

    /// <summary>Tra cứu hóa đơn theo số hóa đơn (InvoiceNumber).</summary>
    Task<InvoiceDto> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);

    /// <summary>Tra cứu hóa đơn hoa hồng theo kỳ quyết toán (SettlementId).</summary>
    Task<InvoiceDto> GetBySettlementIdAsync(int settlementId, CancellationToken cancellationToken = default);
}

