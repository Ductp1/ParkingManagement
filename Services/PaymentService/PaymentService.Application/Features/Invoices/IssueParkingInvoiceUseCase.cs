using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Invoices;

/// <summary>
/// Ca sử dụng phát hành hóa đơn điện tử phí đỗ xe cho tài xế (US-036 AC1, AC2).
/// </summary>
public sealed class IssueParkingInvoiceUseCase : IIssueParkingInvoiceUseCase
{
    private readonly IInvoiceRepository repo;
    private readonly TimeProvider timeProvider;

    public IssueParkingInvoiceUseCase(IInvoiceRepository repo, TimeProvider? timeProvider = null)
    {
        this.repo = repo;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<InvoiceDto> ExecuteAsync(IssueParkingInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tính hợp lệ cơ bản của tham số đầu vào
        if (command.PaymentId <= 0)
            throw new ValidationException("PaymentId phải là số nguyên dương.");

        if (string.IsNullOrWhiteSpace(command.BuyerName))
            throw new ValidationException("Tên người mua / đơn vị nhận hóa đơn không được để trống.");

        if (command.VatRate < 0m || command.VatRate > 0.30m)
            throw new ValidationException("Thuế suất VAT không hợp lệ (phải nằm trong khoảng từ 0% đến 30%).");

        // 2. Tra cứu thông tin đơn thanh toán
        var payment = await repo.GetPaymentByIdAsync(command.PaymentId, cancellationToken)
            ?? throw new NotFoundException("Giao dịch thanh toán", command.PaymentId);

        // 3. Kiểm tra trạng thái giao dịch: bắt buộc phải thành công mới được xuất hóa đơn
        if (payment.Status != PaymentStatus.Succeeded)
            throw new ValidationException($"Chỉ có thể xuất hóa đơn cho giao dịch đã thanh toán thành công (Trạng thái hiện tại: {payment.Status}).");

        // 4. Kiểm tra chống xuất hóa đơn trùng lặp 2 lần cho cùng một giao dịch
        var alreadyIssued = await repo.ExistsForPaymentAsync(command.PaymentId, cancellationToken);
        if (alreadyIssued)
            throw new ValidationException($"Giao dịch thanh toán #{command.PaymentId} đã được xuất hóa đơn điện tử trước đó.");

        // 5. Bóc tách tài chính và tính thuế VAT (US-036 AC2)
        // Giá trị thanh toán thực tế của tài xế là tổng tiền đã bao gồm thuế VAT
        var total = payment.Amount;
        var subTotal = Math.Round(total / (1m + command.VatRate), 2);
        var vatAmount = total - subTotal;

        // 6. Sinh mã số hóa đơn duy nhất theo định dạng kế toán chuẩn: INV-PK-yyyyMMdd-XXXXXX
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var invoiceNumber = $"INV-PK-{now:yyyyMMdd}-{payment.Id:D6}";
        var pdfUrl = $"/invoices/{invoiceNumber}.pdf";

        // 7. Khởi tạo thực thể hóa đơn và lưu xuống cơ sở dữ liệu
        var invoice = new Invoice
        {
            Type = InvoiceType.ParkingFee,
            PaymentId = payment.Id,
            SettlementId = null,
            InvoiceNumber = invoiceNumber,
            BuyerName = command.BuyerName.Trim(),
            BuyerTaxCode = command.BuyerTaxCode?.Trim(),
            BuyerEmail = command.BuyerEmail?.Trim(),
            SubTotal = subTotal,
            VatAmount = vatAmount,
            Total = total,
            IssuedAtUtc = now,
            PdfUrl = pdfUrl
        };

        await repo.AddAsync(invoice, cancellationToken);

        return MapToDto(invoice, payment.BookingId);
    }

    public static InvoiceDto MapToDto(Invoice invoice, int? bookingId = null)
    {
        return new InvoiceDto(
            invoice.Id,
            invoice.Type,
            invoice.InvoiceNumber,
            invoice.PaymentId,
            bookingId ?? invoice.Payment?.BookingId,
            invoice.SettlementId,
            invoice.BuyerName,
            invoice.BuyerTaxCode,
            invoice.BuyerEmail,
            invoice.SubTotal,
            invoice.VatAmount,
            invoice.Total,
            invoice.IssuedAtUtc,
            invoice.PdfUrl,
            "Issued");
    }
}

