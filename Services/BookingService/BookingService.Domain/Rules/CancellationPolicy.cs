using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Domain.Rules;

/// <summary>Kết quả đánh giá hủy booking.</summary>
public sealed record CancellationDecision(bool CanCancel, BookingStatus ResultStatus, int RefundPercent, string Reason);

/// <summary>
/// CancellationWindowEvaluator (Kiến trúc v3, Final Business Decisions):
/// hủy trước giờ bắt đầu ≥ 60 phút → Cancelled, hoàn 100%; &lt; 60 phút → CancelledNoRefund, hoàn 0%.
/// Chỉ booking Confirmed / PendingOwnerApproval / PendingPayment mới được hủy.
/// </summary>
public static class CancellationPolicy
{
    public const int DefaultWindowMinutes = 60;

    public static CancellationDecision Evaluate(BookingStatus status, DateTime startAtUtc, DateTime nowUtc, int windowMinutes = DefaultWindowMinutes)
    {
        if (status is BookingStatus.PendingPayment or BookingStatus.Created)
            return new(true, BookingStatus.Cancelled, 0, "Chưa thanh toán – hủy không phát sinh hoàn tiền.");

        if (status is not (BookingStatus.Confirmed or BookingStatus.PendingOwnerApproval))
            return new(false, status, 0, $"Booking ở trạng thái {status} không thể hủy.");

        var minutesBefore = (startAtUtc - nowUtc).TotalMinutes;
        return minutesBefore >= windowMinutes
            ? new(true, BookingStatus.Cancelled, 100, $"Hủy trước {minutesBefore:0} phút (≥ {windowMinutes}) – hoàn 100%.")
            : new(true, BookingStatus.CancelledNoRefund, 0, $"Hủy trước {Math.Max(0, minutesBefore):0} phút (< {windowMinutes}) – không hoàn tiền.");
    }
}
