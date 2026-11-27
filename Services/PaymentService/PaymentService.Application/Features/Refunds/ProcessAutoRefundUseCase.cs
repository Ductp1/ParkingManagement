using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Refunds;

/// <summary>
/// DTO chứa tham số yêu cầu xử lý hoàn tiền tự động khi lệch thanh toán và booking (US-034).
/// </summary>
/// <param name="PaymentId">Mã định danh giao dịch thanh toán đã trừ tiền.</param>
/// <param name="IsLotFull">Đánh dấu bãi xe đã hết chỗ hoàn toàn hay chưa (nếu còn chỗ thì ưu tiên tạo lại booking).</param>
/// <param name="FailureReason">Lý do chi tiết gây ra lỗi kích hoạt booking.</param>
public sealed record ProcessAutoRefundCommand(
    int PaymentId,
    bool IsLotFull,
    string? FailureReason = null);

/// <summary>
/// DTO trả về kết quả xử lý hoàn tiền tự động (US-034 AC1, AC2).
/// </summary>
/// <param name="PaymentId">Mã giao dịch thanh toán.</param>
/// <param name="RefundId">Mã bản ghi hoàn tiền được tạo (null nếu bãi còn chỗ và chọn thử lại booking).</param>
/// <param name="RefundAmount">Số tiền thực tế được hoàn (VNĐ).</param>
/// <param name="ActionTaken">Hành động hệ thống đã thực hiện (Refunded100Percent hoặc RetryNewBooking).</param>
/// <param name="Status">Trạng thái hoàn tiền hiện tại.</param>
/// <param name="Message">Thông điệp chi tiết giải thích kết quả xử lý.</param>
public sealed record AutoRefundResultDto(
    int PaymentId,
    int? RefundId,
    decimal RefundAmount,
    string ActionTaken,
    string Status,
    string Message);

/// <summary>
/// Giao diện (Port) định nghĩa ca sử dụng hoàn tiền tự động khi booking thất bại (US-034).
/// </summary>
public interface IProcessAutoRefundUseCase
{
    /// <summary>
    /// Thực thi kiểm tra và xử lý hoàn tiền tự động 100% hoặc kích hoạt tạo booking mới khi bãi còn chỗ.
    /// </summary>
    Task<AutoRefundResultDto> ExecuteAsync(ProcessAutoRefundCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cài đặt ca sử dụng xử lý hoàn tiền tự động độc lập theo nguyên lý Single Responsibility (US-034).
/// </summary>
public sealed class ProcessAutoRefundUseCase(
    IRefundRepository refundRepository,
    TimeProvider timeProvider) : IProcessAutoRefundUseCase
{
    public async Task<AutoRefundResultDto> ExecuteAsync(ProcessAutoRefundCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tham số đầu vào hợp lệ
        if (command.PaymentId <= 0)
            throw new ValidationException("PaymentId phải là số nguyên dương.");

        // 2. Tìm kiếm đơn thanh toán gốc trong hệ thống
        var payment = await refundRepository.GetPaymentByIdAsync(command.PaymentId, cancellationToken)
            ?? throw new NotFoundException("Giao dịch thanh toán", command.PaymentId);

        // 3. Kiểm tra tính toàn vẹn: Chỉ hoàn tiền cho đơn đã thanh toán thành công (Succeeded hoặc PartiallyRefunded)
        if (payment.Status != PaymentStatus.Succeeded && payment.Status != PaymentStatus.PartiallyRefunded)
        {
            throw new ValidationException(
                $"Không thể hoàn tiền cho giao dịch có trạng thái '{payment.Status}'. Giao dịch phải ở trạng thái 'Succeeded'.");
        }

        // 4. Tính toán số tiền còn lại có thể hoàn (chống hoàn vượt quá số tiền khách đã trả)
        var remainingAmount = payment.Amount - payment.RefundedAmount;
        if (remainingAmount <= 0)
        {
            throw new ValidationException("Giao dịch này đã được hoàn đủ 100% từ trước, không thể hoàn thêm.");
        }

        // 5. Xử lý tiêu chí AC1: Nếu bãi vẫn còn chỗ thì ưu tiên đề xuất tạo booking mới mà không bắt trả tiền lại
        if (!command.IsLotFull)
        {
            return new AutoRefundResultDto(
                payment.Id,
                null,
                0m,
                "RetryNewBooking",
                "Retrying",
                "Bãi xe vẫn còn vị trí đỗ trống tương đương. Hệ thống ưu tiên giữ tiền và kích hoạt đặt lại booking mới cho khách.");
        }

        // 6. Xử lý tiêu chí AC1 & AC2: Bãi đã hết chỗ hoàn toàn -> Tiến hành hoàn tiền 100% tự động
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var refund = new Refund
        {
            PaymentId = payment.Id,
            Amount = remainingAmount,
            Reason = RefundReason.PaymentBookingMismatch, // AC2: Lý do lệch thanh toán và booking
            Note = string.IsNullOrWhiteSpace(command.FailureReason)
                ? "Hoàn tiền tự động 100% do bãi hết chỗ và booking không thể kích hoạt."
                : $"Hoàn tiền tự động do booking thất bại: {command.FailureReason}",
            Status = RefundStatus.Completed,
            RequestedByUserId = null, // AC2: null biểu thị SYSTEM tự động thực hiện
            ProcessedAtUtc = now
        };

        // Cập nhật số tiền đã hoàn và trạng thái của đơn thanh toán
        payment.RefundedAmount += remainingAmount;
        payment.Status = payment.RefundedAmount >= payment.Amount 
            ? PaymentStatus.Refunded 
            : PaymentStatus.PartiallyRefunded;

        // 7. Tạo sự kiện tích hợp Outbox để gửi thông báo cho tài xế (AC3)
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

        // 8. Lưu nguyên khối trong cùng một Transaction
        await refundRepository.SaveAutoRefundTransactionAsync(payment, refund, outboxMessage, cancellationToken);

        return new AutoRefundResultDto(
            payment.Id,
            refund.Id,
            refund.Amount,
            "Refunded100Percent",
            refund.Status.ToString(),
            $"Đã tự động hoàn trả 100% số tiền {refund.Amount:N0} VNĐ cho khách hàng thành công do bãi hết chỗ.");
    }
}
