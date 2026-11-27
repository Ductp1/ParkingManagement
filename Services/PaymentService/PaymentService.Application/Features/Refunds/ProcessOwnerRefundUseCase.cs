using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Refunds;

/// <summary>
/// Lệnh yêu cầu chủ bãi chủ động hoàn tiền bồi thường cho khách hàng do sự cố tại bãi (US-041).
/// </summary>
/// <param name="PaymentId">Mã định danh duy nhất của giao dịch thanh toán ban đầu.</param>
/// <param name="OwnerProfileId">Mã hồ sơ chủ bãi xe thực hiện yêu cầu hoàn tiền.</param>
/// <param name="Amount">Số tiền bồi thường muốn hoàn trả cho khách (VNĐ).</param>
/// <param name="ReasonDescription">Lý do chi tiết sự cố tại bãi (cúp điện, hỏng barrier, phục vụ kém...).</param>
/// <param name="ComplaintId">Mã khiếu nại liên kết nếu có.</param>
public sealed record ProcessOwnerRefundCommand(
    int PaymentId,
    int OwnerProfileId,
    decimal Amount,
    string ReasonDescription,
    int? ComplaintId = null);

/// <summary>
/// Kết quả phản hồi sau khi chủ bãi duyệt hoàn tiền thành công (US-041).
/// </summary>
/// <param name="PaymentId">Mã giao dịch thanh toán.</param>
/// <param name="RefundId">Mã bản ghi hoàn tiền vừa tạo.</param>
/// <param name="RefundAmount">Số tiền đã hoàn bồi thường (VNĐ).</param>
/// <param name="Status">Trạng thái của bản ghi hoàn tiền (Completed).</param>
/// <param name="Reason">Lý do hoàn tiền (LotFault - Lỗi tại bãi).</param>
/// <param name="Message">Thông điệp phản hồi chi tiết.</param>
public sealed record OwnerRefundResultDto(
    int PaymentId,
    int RefundId,
    decimal RefundAmount,
    string Status,
    string Reason,
    string Message);

/// <summary>
/// Ca sử dụng xử lý hoàn tiền chủ động do lỗi bãi xe (US-041, UC-36).
/// </summary>
public interface IProcessOwnerRefundUseCase
{
    /// <summary>
    /// Thực thi lệnh hoàn tiền bồi thường của chủ bãi xe.
    /// </summary>
    Task<OwnerRefundResultDto> ExecuteAsync(ProcessOwnerRefundCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lớp triển khai ca sử dụng chủ bãi chủ động hoàn tiền khi lỗi tại bãi (US-041).
/// Tuân thủ nguyên tắc Single Responsibility: Chỉ đảm nhiệm duy nhất nghiệp vụ hoàn tiền thủ công của Owner.
/// </summary>
public sealed class ProcessOwnerRefundUseCase(
    IRefundRepository refundRepository,
    TimeProvider timeProvider) : IProcessOwnerRefundUseCase
{
    public async Task<OwnerRefundResultDto> ExecuteAsync(ProcessOwnerRefundCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Kiểm tra tham số biên dữ liệu
        if (command.PaymentId <= 0)
            throw new ValidationException("PaymentId phải là số nguyên dương.");

        if (command.OwnerProfileId <= 0)
            throw new ValidationException("OwnerProfileId không hợp lệ.");

        if (command.Amount <= 0)
            throw new ValidationException("Số tiền hoàn bồi thường phải lớn hơn 0 VNĐ.");

        if (string.IsNullOrWhiteSpace(command.ReasonDescription))
            throw new ValidationException("Lý do hoàn tiền không được để trống.");

        // 2. Tìm kiếm đơn thanh toán gốc trong hệ thống
        var payment = await refundRepository.GetPaymentByIdAsync(command.PaymentId, cancellationToken)
            ?? throw new NotFoundException("Giao dịch thanh toán", command.PaymentId);

        // 3. Kiểm tra tính toàn vẹn quyền sở hữu: Chủ bãi chỉ có quyền hoàn tiền cho bãi xe của mình
        if (payment.OwnerProfileId != command.OwnerProfileId)
        {
            throw new ValidationException("Chủ bãi không có quyền xử lý hoàn tiền cho giao dịch của bãi xe khác.");
        }

        // 4. Kiểm tra trạng thái đơn: Chỉ hoàn tiền cho đơn đã thanh toán thành công (Succeeded hoặc PartiallyRefunded)
        if (payment.Status != PaymentStatus.Succeeded && payment.Status != PaymentStatus.PartiallyRefunded)
        {
            throw new ValidationException($"Không thể hoàn tiền cho giao dịch có trạng thái '{payment.Status}'. Đơn phải ở trạng thái Succeeded hoặc PartiallyRefunded.");
        }

        // 5. Kiểm tra số tiền hoàn hợp lệ (Không được hoàn vượt quá số tiền còn lại của đơn hàng)
        var remainingAmount = payment.Amount - payment.RefundedAmount;
        if (remainingAmount <= 0)
        {
            throw new ValidationException("Giao dịch này đã được hoàn đủ 100% trước đó, không thể hoàn thêm.");
        }

        if (command.Amount > remainingAmount)
        {
            throw new ValidationException($"Số tiền yêu cầu hoàn ({command.Amount:N0} VNĐ) vượt quá số tiền còn lại có thể hoàn ({remainingAmount:N0} VNĐ).");
        }

        // 6. Khởi tạo bản ghi Refund với lý do Lỗi bãi xe (LotFault)
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var refund = new Refund
        {
            PaymentId = payment.Id,
            Amount = command.Amount,
            Reason = RefundReason.LotFault, // AC1: Ghi nhận lỗi thuộc về bãi xe để trừ vào quyết toán
            Note = command.ReasonDescription.Trim(),
            Status = RefundStatus.Completed,
            RequestedByUserId = command.OwnerProfileId, // AC3: Lưu vết mã chủ bãi thực hiện
            ComplaintId = command.ComplaintId,
            ProcessedAtUtc = now
        };

        // 7. Cập nhật số tiền đã hoàn tích lũy và trạng thái của đơn thanh toán
        payment.RefundedAmount += command.Amount;
        payment.Status = payment.RefundedAmount >= payment.Amount
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;

        // 8. Tạo sự kiện tích hợp Outbox để dịch vụ NotificationService gửi thông báo bồi thường cho tài xế
        var integrationEvent = new RefundIssued(
            refund.Id,
            payment.Id,
            payment.BookingId,
            refund.Amount);

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = now,
            EventType = nameof(RefundIssued),
            PayloadJson = JsonSerializer.Serialize(integrationEvent)
        };

        // 9. Lưu tất cả thay đổi trong một Transaction nguyên tử
        await refundRepository.SaveAutoRefundTransactionAsync(payment, refund, outboxMessage, cancellationToken);

        return new OwnerRefundResultDto(
            payment.Id,
            refund.Id,
            refund.Amount,
            refund.Status.ToString(),
            refund.Reason.ToString(),
            $"Chủ bãi đã duyệt hoàn tiền {refund.Amount:N0} VNĐ cho khách hàng thành công. Khoản tiền sẽ được đối soát trừ vào kỳ quyết toán của chủ bãi.");
    }
}
