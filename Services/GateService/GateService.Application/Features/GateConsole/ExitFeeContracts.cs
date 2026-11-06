namespace GateService.Application.Features.GateConsole;

// ===== Port tính phí (gọi PaymentService – TV4) =====

public sealed record FeeLineDto(string Description, int Minutes, decimal Amount);

/// <summary>Yêu cầu chốt phí 1 lượt gửi xe. Booking truyền kèm snapshot để giữ Price Lock; khách vãng lai bỏ trống.</summary>
public sealed record SessionFeeRequest(
    int ParkingLotId,
    string VehicleType,
    DateTime EntryAtUtc,
    DateTime ExitAtUtc,
    DateTime? BookedEndAtUtc,
    int? BillingGracePeriodMinutes,
    decimal? OverstayMultiplier);

public sealed record SessionFeeResult(
    int RateCardId,
    int DurationMinutes,
    bool IsWithinBillingGrace,
    decimal BaseAmount,
    decimal Multiplier,
    int OverstayMinutes,
    decimal OverstayAmount,
    decimal FinalAmount,
    IReadOnlyList<FeeLineDto> Lines);

/// <summary>UC-22 bước 2: GateService nhờ PaymentService tính tổng tiền (UC-11 include) — GateService không tự tính giá.</summary>
public interface IExitFeeCalculator
{
    Task<SessionFeeResult> CalculateAsync(SessionFeeRequest request, CancellationToken cancellationToken = default);
}

// ===== Port ghi thanh toán tiền mặt tại cổng (gọi PaymentService) =====

public sealed record GateSettlementRequest(
    string SessionCode,
    int ParkingSessionId,
    int ParkingLotId,
    int OwnerProfileId,
    string? BookingCode,
    decimal Amount,
    string Method,
    int? ConfirmedByStaffId);

public sealed record GateSettlementResult(string PaymentCode, string Status);

/// <summary>UC-22 A2 / UC-23: staff xác nhận đã thu tiền → PaymentService ghi Payment Succeeded (idempotent theo SessionCode).</summary>
public interface IGateSettlementRecorder
{
    Task<GateSettlementResult> RecordAsync(GateSettlementRequest request, CancellationToken cancellationToken = default);
}

// ===== Báo giá chốt phí hiển thị cho staff trước khi xác nhận =====

public sealed record ExitFeeQuoteDto(
    string SessionCode,
    string PlateDisplay,
    string? BookingCode,
    string? BookingStatus,
    DateTime EntryAtUtc,
    int MinutesParked,
    decimal SessionFee,
    decimal PrepaidAmount,
    decimal AdditionalDue,
    bool IsWithinBillingGrace,
    int OverstayMinutes,
    decimal OverstayAmount,
    IReadOnlyList<FeeLineDto> Lines);