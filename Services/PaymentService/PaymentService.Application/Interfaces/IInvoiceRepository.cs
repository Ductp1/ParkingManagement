using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// Cổng lưu trữ và truy vấn hóa đơn điện tử phí đỗ xe và hoa hồng sàn (US-036).
/// </summary>
public interface IInvoiceRepository
{
    /// <summary>Tìm hóa đơn theo mã định danh duy nhất (Id).</summary>
    Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Tìm hóa đơn phí đỗ xe gắn với giao dịch thanh toán (PaymentId).</summary>
    Task<Invoice?> GetByPaymentIdAsync(int paymentId, CancellationToken cancellationToken = default);

    /// <summary>Tìm hóa đơn phí đỗ xe gắn với mã đặt chỗ (BookingId).</summary>
    Task<Invoice?> GetByBookingIdAsync(int bookingId, CancellationToken cancellationToken = default);

    /// <summary>Tìm hóa đơn hoa hồng nền tảng gắn với kỳ quyết toán (SettlementId).</summary>
    Task<Invoice?> GetBySettlementIdAsync(int settlementId, CancellationToken cancellationToken = default);

    /// <summary>Tìm hóa đơn theo số hóa đơn duy nhất (InvoiceNumber).</summary>
    Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);

    /// <summary>Lấy thông tin giao dịch thanh toán theo ID để đối chiếu trước khi xuất hóa đơn.</summary>
    Task<Payment?> GetPaymentByIdAsync(int paymentId, CancellationToken cancellationToken = default);

    /// <summary>Lấy thông tin kỳ quyết toán theo ID để đối chiếu trước khi xuất hóa đơn hoa hồng.</summary>
    Task<Settlement?> GetSettlementByIdAsync(int settlementId, CancellationToken cancellationToken = default);

    /// <summary>Lưu mới một bản ghi hóa đơn điện tử vào cơ sở dữ liệu.</summary>
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra xem giao dịch thanh toán đã được xuất hóa đơn trước đó chưa.</summary>
    Task<bool> ExistsForPaymentAsync(int paymentId, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra xem kỳ quyết toán đã được xuất hóa đơn hoa hồng trước đó chưa.</summary>
    Task<bool> ExistsForSettlementAsync(int settlementId, CancellationToken cancellationToken = default);
}

