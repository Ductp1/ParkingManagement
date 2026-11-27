using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;

namespace PaymentService.Application.Features.Invoices;

/// <summary>
/// Ca sử dụng tra cứu hóa đơn điện tử theo nhiều tiêu chí (US-036 AC3).
/// </summary>
public sealed class GetInvoiceUseCase(IInvoiceRepository repo) : IGetInvoiceUseCase
{
    public async Task<InvoiceDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ValidationException("InvoiceId phải là số nguyên dương.");

        var found = await repo.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Hóa đơn điện tử", id);

        return IssueParkingInvoiceUseCase.MapToDto(found);
    }

    public async Task<InvoiceDto> GetByPaymentIdAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        if (paymentId <= 0)
            throw new ValidationException("PaymentId phải là số nguyên dương.");

        var found = await repo.GetByPaymentIdAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException("Hóa đơn điện tử của giao dịch thanh toán", paymentId);

        return IssueParkingInvoiceUseCase.MapToDto(found);
    }

    public async Task<InvoiceDto> GetByBookingIdAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        if (bookingId <= 0)
            throw new ValidationException("BookingId phải là số nguyên dương.");

        var found = await repo.GetByBookingIdAsync(bookingId, cancellationToken)
            ?? throw new NotFoundException("Hóa đơn điện tử của đơn đặt chỗ", bookingId);

        return IssueParkingInvoiceUseCase.MapToDto(found, bookingId);
    }

    public async Task<InvoiceDto> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ValidationException("Số hóa đơn không được để trống.");

        var found = await repo.GetByInvoiceNumberAsync(invoiceNumber, cancellationToken)
            ?? throw new NotFoundException("Hóa đơn điện tử", invoiceNumber.Trim().ToUpperInvariant());

        return IssueParkingInvoiceUseCase.MapToDto(found);
    }

    public async Task<InvoiceDto> GetBySettlementIdAsync(int settlementId, CancellationToken cancellationToken = default)
    {
        if (settlementId <= 0)
            throw new ValidationException("SettlementId phải là số nguyên dương.");

        var found = await repo.GetBySettlementIdAsync(settlementId, cancellationToken)
            ?? throw new NotFoundException("Hóa đơn hoa hồng của kỳ quyết toán", settlementId);

        return IssueParkingInvoiceUseCase.MapToDto(found);
    }
}

