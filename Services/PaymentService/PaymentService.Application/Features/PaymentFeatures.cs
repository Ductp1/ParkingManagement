using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;

namespace PaymentService.Application.Features;

// ===== DTO =====
public sealed record PriceQuoteDto(int ParkingLotId, int RateCardId, string VehicleType, DateTime StartAtUtc, DateTime EndAtUtc,
    int DurationMinutes, decimal BaseAmount, decimal Multiplier, decimal FinalAmount, bool IsWithinGracePeriod, IReadOnlyList<PriceLine> Lines);

public sealed record PaymentDto(int Id, string Code, int? BookingId, string? BookingCode, string Method, string Status,
    decimal Amount, decimal RefundedAmount, string? ProviderTransactionId, DateTime? PaidAtUtc, string? InvoiceNumber);

// ===== PORT =====
public interface IRateCardRepository
{
    /// <summary>Rate card đang hiệu lực của bãi tại thời điểm <paramref name="atUtc"/> (kèm các RateRule).</summary>
    Task<RateCard?> GetActiveAsync(int parkingLotId, DateTime atUtc, CancellationToken cancellationToken);
}

public interface IPaymentQueries
{
    Task<IReadOnlyList<PaymentDto>> ListByBookingAsync(int bookingId, CancellationToken cancellationToken);
}

// ===== USE CASE =====
public sealed record QuotePriceQuery(int ParkingLotId, VehicleType VehicleType, DateTime StartAtUtc, DateTime EndAtUtc);

public interface IQuotePriceUseCase
{
    Task<PriceQuoteDto> ExecuteAsync(QuotePriceQuery query, CancellationToken cancellationToken = default);
}

/// <summary>UC-11: Báo giá trước khi đặt chỗ. BookingService gọi API này rồi lưu kết quả vào PriceSnapshot (Price Lock).</summary>
public sealed class QuotePriceUseCase(IRateCardRepository rateCards) : IQuotePriceUseCase
{
    public async Task<PriceQuoteDto> ExecuteAsync(QuotePriceQuery query, CancellationToken cancellationToken = default)
    {
        if (query.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (query.EndAtUtc <= query.StartAtUtc) throw new ValidationException("Giờ kết thúc phải sau giờ bắt đầu.");
        if ((query.EndAtUtc - query.StartAtUtc).TotalDays > 30) throw new ValidationException("Thời gian đỗ tối đa 30 ngày.");

        var card = await rateCards.GetActiveAsync(query.ParkingLotId, query.StartAtUtc, cancellationToken)
            ?? throw new NotFoundException("Bảng giá của bãi", query.ParkingLotId);

        var q = PriceCalculator.Calculate(card, query.VehicleType, query.StartAtUtc, query.EndAtUtc);
        return new PriceQuoteDto(query.ParkingLotId, card.Id, query.VehicleType.ToString(), query.StartAtUtc, query.EndAtUtc,
            q.DurationMinutes, q.BaseAmount, q.Multiplier, q.FinalAmount, q.IsWithinGracePeriod, q.Lines);
    }
}

public interface IGetPaymentsByBookingUseCase
{
    Task<IReadOnlyList<PaymentDto>> ExecuteAsync(int bookingId, CancellationToken cancellationToken = default);
}

public sealed class GetPaymentsByBookingUseCase(IPaymentQueries queries) : IGetPaymentsByBookingUseCase
{
    public Task<IReadOnlyList<PaymentDto>> ExecuteAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        if (bookingId <= 0) throw new ValidationException("bookingId phải là số nguyên dương.");
        return queries.ListByBookingAsync(bookingId, cancellationToken);
    }
}
