using GateService.Application.Features;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Application.Features.GateConsole;

public interface IQuoteExitFeeUseCase
{
    /// <summary>UC-22 bước 1–3: tra xe đang đỗ theo biển số và tính tổng tiền + phụ thu để staff xác nhận trước khi mở barie.</summary>
    Task<ExitFeeQuoteDto> ExecuteAsync(int parkingLotId, string plate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Bộ tính báo giá chốt phí dùng chung cho exit-quote và check-out (ConfirmFee):
/// lượt đang đỗ + booking snapshot (nếu có) → PaymentService tính phí (US-045/046/047).
/// AdditionalDue = tiền cần thu thêm tại cổng = SessionFee − số đã trả trước của booking (không âm).
/// </summary>
public static class ExitFeeQuoter
{
    public static async Task<ExitFeeQuoteDto> QuoteAsync(
        ParkingSessionDto session,
        IGateBookingClient bookingClient,
        IExitFeeCalculator feeCalculator,
        GateConsoleOptions options,
        DateTime exitAtUtc,
        CancellationToken cancellationToken)
    {
        var booking = session.BookingCode is null
            ? null
            : await bookingClient.FindByCodeAsync(session.BookingCode, cancellationToken)
              ?? throw new ParkingManagement.SharedKernel.Exceptions.NotFoundException("Booking", session.BookingCode);

        var fee = await feeCalculator.CalculateAsync(new SessionFeeRequest(
            session.ParkingLotId,
            booking?.VehicleType ?? session.VehicleType, // Price Lock: theo loại xe đã khai báo trên booking
            session.EntryAtUtc,
            exitAtUtc,
            booking?.EndAtUtc,
            booking?.BillingGracePeriodMinutes ?? options.BillingGracePeriodMinutes,
            booking?.OverstayMultiplier), cancellationToken);

        var additionalDue = Math.Max(0, fee.FinalAmount - (booking?.PrepaidAmount ?? 0));

        return new ExitFeeQuoteDto(
            session.Code,
            session.PlateDisplay,
            booking?.Code,
            booking?.Status,
            session.EntryAtUtc,
            (int)Math.Ceiling((exitAtUtc - session.EntryAtUtc).TotalMinutes),
            fee.FinalAmount,
            booking?.PrepaidAmount ?? 0,
            additionalDue,
            fee.IsWithinBillingGrace,
            fee.OverstayMinutes,
            fee.OverstayAmount,
            fee.Lines);
    }
}

/// <summary>UC-22: staff quét QR/nhập biển số ở cổng ra → xem tổng tiền + phụ thu trước khi xác nhận cho xe ra.</summary>
public sealed class QuoteExitFeeUseCase(
    IParkingSessionQueries sessionQueries,
    IGateBookingClient bookingClient,
    IExitFeeCalculator feeCalculator,
    GateConsoleOptions options,
    TimeProvider timeProvider) : IQuoteExitFeeUseCase
{
    public async Task<ExitFeeQuoteDto> ExecuteAsync(int parkingLotId, string plate, CancellationToken cancellationToken = default)
    {
        if (parkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        var normalized = PlateNormalizer.TryCorrect(plate)
            ?? throw new ValidationException($"Không đọc được biển số hợp lệ từ '{plate}'. Nhân viên kiểm tra và nhập tay.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var session = await sessionQueries.FindActiveByPlateAsync(parkingLotId, normalized, now, cancellationToken)
            ?? throw new NotFoundException("Lượt gửi xe đang đỗ", PlateNormalizer.Format(normalized));

        return await ExitFeeQuoter.QuoteAsync(session, bookingClient, feeCalculator, options, now, cancellationToken);
    }
}