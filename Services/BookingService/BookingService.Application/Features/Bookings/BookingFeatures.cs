using BookingService.Domain.Rules;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace BookingService.Application.Features.Bookings;

// ===== DTO =====
public sealed record BookingSummaryDto(int Id, string Code, string Status, string ParkingLotName, string? SlotCode,
    string PlateNumber, DateTime StartAtUtc, DateTime EndAtUtc, decimal TotalAmount);

public sealed record BookingDetailDto(int Id, string Code, string Status, int UserId, int VehicleId, string PlateNumber, string VehicleType,
    int ParkingLotId, string ParkingLotName, int? SlotId, string? SlotCode, DateTime StartAtUtc, DateTime EndAtUtc,
    DateTime? HoldExpiresAtUtc, decimal TotalAmount, decimal DiscountAmount, decimal PaidAmount, string? PromotionCode,
    PriceSnapshotDto? PriceSnapshot, IReadOnlyList<StatusLogDto> History);

public sealed record PriceSnapshotDto(int RateCardId, decimal BaseAmount, decimal SurchargeAmount, decimal DiscountAmount,
    decimal FinalAmount, int BillingGracePeriodMinutes, string RateCardJson);

public sealed record StatusLogDto(string? FromStatus, string ToStatus, int? ChangedByUserId, string? Reason, DateTime AtUtc);

public sealed record CancellationPreviewDto(string Code, bool CanCancel, string ResultStatus, int RefundPercent, decimal RefundAmount, string Reason);

// ===== PORT =====
public interface IBookingQueries
{
    Task<BookingDetailDto?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyList<BookingSummaryDto>> ListByUserAsync(int userId, BookingStatus? status, CancellationToken cancellationToken);
}

// ===== USE CASE =====
public interface IGetBookingByCodeUseCase
{
    Task<BookingDetailDto> ExecuteAsync(string code, CancellationToken cancellationToken = default);
}

public sealed class GetBookingByCodeUseCase(IBookingQueries queries) : IGetBookingByCodeUseCase
{
    public async Task<BookingDetailDto> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ValidationException("Mã booking không được rỗng.");
        return await queries.GetByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }
}

public interface IListMyBookingsUseCase
{
    Task<IReadOnlyList<BookingSummaryDto>> ExecuteAsync(int userId, BookingStatus? status, CancellationToken cancellationToken = default);
}

/// <summary>UC-13: Booking của tôi (sắp tới / đang đỗ / lịch sử).</summary>
public sealed class ListMyBookingsUseCase(IBookingQueries queries) : IListMyBookingsUseCase
{
    public Task<IReadOnlyList<BookingSummaryDto>> ExecuteAsync(int userId, BookingStatus? status, CancellationToken cancellationToken = default)
    {
        if (userId <= 0) throw new ValidationException("userId phải là số nguyên dương.");
        return queries.ListByUserAsync(userId, status, cancellationToken);
    }
}

public interface IPreviewCancellationUseCase
{
    Task<CancellationPreviewDto> ExecuteAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>UC-16 (bước xem trước): cho tài xế biết hủy lúc này được hoàn bao nhiêu, theo Cancellation Window 60 phút.</summary>
public sealed class PreviewCancellationUseCase(IBookingQueries queries, TimeProvider timeProvider) : IPreviewCancellationUseCase
{
    public async Task<CancellationPreviewDto> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    {
        var booking = await queries.GetByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        var status = Enum.Parse<BookingStatus>(booking.Status);
        var decision = CancellationPolicy.Evaluate(status, booking.StartAtUtc, timeProvider.GetUtcNow().UtcDateTime);
        var refund = Math.Round(booking.PaidAmount * decision.RefundPercent / 100m, 0);

        return new CancellationPreviewDto(booking.Code, decision.CanCancel, decision.ResultStatus.ToString(),
            decision.RefundPercent, refund, decision.Reason);
    }
}
