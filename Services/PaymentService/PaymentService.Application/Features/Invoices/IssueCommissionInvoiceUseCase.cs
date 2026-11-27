using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Invoices;

/// <summary>
/// Ca sử dụng phát hành hóa đơn hoa hồng sàn cho chủ bãi xe theo kỳ quyết toán (US-036).
/// </summary>
public sealed class IssueCommissionInvoiceUseCase : IIssueCommissionInvoiceUseCase
{
    private readonly IInvoiceRepository repo;
    private readonly TimeProvider timeProvider;

    public IssueCommissionInvoiceUseCase(IInvoiceRepository repo, TimeProvider? timeProvider = null)
    {
        this.repo = repo;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<InvoiceDto> ExecuteAsync(IssueCommissionInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tính hợp lệ cơ bản
        if (command.SettlementId <= 0)
            throw new ValidationException("SettlementId phải là số nguyên dương.");

        if (string.IsNullOrWhiteSpace(command.BuyerName))
            throw new ValidationException("Tên đơn vị nhận hóa đơn (Chủ bãi xe) không được để trống.");

        if (command.VatRate < 0m || command.VatRate > 0.30m)
            throw new ValidationException("Thuế suất VAT không hợp lệ (phải từ 0% đến 30%).");

        // 2. Tra cứu thông tin kỳ quyết toán
        var settlement = await repo.GetSettlementByIdAsync(command.SettlementId, cancellationToken)
            ?? throw new NotFoundException("Kỳ quyết toán", command.SettlementId);

        // 3. Kiểm tra trạng thái kỳ quyết toán: không được ở trạng thái Bản nháp (Draft)
        if (settlement.Status == SettlementStatus.Draft)
            throw new ValidationException("Chỉ có thể xuất hóa đơn cho kỳ quyết toán đã được duyệt hoặc đã chi trả.");

        // 4. Kiểm tra số tiền hoa hồng
        if (settlement.CommissionAmount <= 0)
            throw new ValidationException("Kỳ quyết toán này không phát sinh phí hoa hồng sàn để xuất hóa đơn.");

        // 5. Kiểm tra chống xuất trùng lặp hóa đơn hoa hồng cho cùng một kỳ quyết toán
        var alreadyIssued = await repo.ExistsForSettlementAsync(command.SettlementId, cancellationToken);
        if (alreadyIssued)
            throw new ValidationException($"Kỳ quyết toán #{command.SettlementId} đã được xuất hóa đơn hoa hồng trước đó.");

        // 6. Tính toán tiền thuế VAT của phí hoa hồng dịch vụ phần mềm
        var subTotal = settlement.CommissionAmount;
        var vatAmount = Math.Round(subTotal * command.VatRate, 2);
        var total = subTotal + vatAmount;

        // 7. Sinh mã số hóa đơn hoa hồng duy nhất: INV-COM-yyyyMMdd-XXXXXX
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var invoiceNumber = $"INV-COM-{now:yyyyMMdd}-{settlement.Id:D6}";
        var pdfUrl = $"/invoices/{invoiceNumber}.pdf";

        // 8. Khởi tạo thực thể hóa đơn hoa hồng và lưu cơ sở dữ liệu
        var invoice = new Invoice
        {
            Type = InvoiceType.PlatformCommission,
            PaymentId = null,
            SettlementId = settlement.Id,
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

        return IssueParkingInvoiceUseCase.MapToDto(invoice);
    }
}

