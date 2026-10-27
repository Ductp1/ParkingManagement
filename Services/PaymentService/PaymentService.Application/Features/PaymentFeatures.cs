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

/// <summary>Cổng truy xuất dữ liệu ngày lễ quốc gia và địa phương phục vụ tính hệ số phụ thu (US-071).</summary>
public interface IHolidayRepository
{
    /// <summary>Lấy danh sách các ngày nghỉ lễ nằm trong khoảng thời gian đỗ xe theo lịch Việt Nam.</summary>
    Task<IReadOnlyCollection<DateOnly>> GetHolidaysInRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
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

/// <summary>UC-11: Báo giá trước khi đặt chỗ (US-070, US-071). BookingService gọi API này rồi lưu kết quả vào PriceSnapshot (Price Lock).</summary>
public sealed class QuotePriceUseCase(
    IRateCardRepository rateCards,
    IHolidayRepository holidayRepo) : IQuotePriceUseCase
{
    public async Task<PriceQuoteDto> ExecuteAsync(QuotePriceQuery query, CancellationToken cancellationToken = default)
    {
        if (query.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (query.EndAtUtc <= query.StartAtUtc) throw new ValidationException("Giờ kết thúc phải sau giờ bắt đầu.");
        if ((query.EndAtUtc - query.StartAtUtc).TotalDays > 30) throw new ValidationException("Thời gian đỗ tối đa 30 ngày.");

        var card = await rateCards.GetActiveAsync(query.ParkingLotId, query.StartAtUtc, cancellationToken)
            ?? throw new NotFoundException("Bảng giá của bãi", query.ParkingLotId);

        // Quy đổi sang ngày địa phương Việt Nam để so khớp chuẩn với bảng lịch nghỉ lễ
        var startVnDate = DateOnly.FromDateTime(query.StartAtUtc.AddHours(7));
        var endVnDate = DateOnly.FromDateTime(query.EndAtUtc.AddHours(7));
        var holidays = await holidayRepo.GetHolidaysInRangeAsync(startVnDate, endVnDate, cancellationToken);

        var q = PriceCalculator.Calculate(card, query.VehicleType, query.StartAtUtc, query.EndAtUtc, holidays);
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

// ===== UC-22 / US-045 → US-047: Chốt phí lượt gửi xe tại cổng ra =====

public sealed record SessionFeeQuery(int ParkingLotId, string VehicleType, DateTime EntryAtUtc, DateTime ExitAtUtc,
    DateTime? BookedEndAtUtc = null, int? BillingGracePeriodMinutes = null, decimal? OverstayMultiplier = null);

public sealed record SessionFeeDto(int ParkingLotId, int RateCardId, string VehicleType, int DurationMinutes,
    bool IsWithinBillingGrace, decimal BaseAmount, decimal Multiplier, int OverstayMinutes, decimal OverstayAmount,
    decimal FinalAmount, IReadOnlyList<PriceLine> Lines);

public interface IComputeSessionFeeUseCase
{
    Task<SessionFeeDto> ExecuteAsync(SessionFeeQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// UC-22 bước 2: tính tổng tiền khi xe ra cổng. Ưu tiên Price Lock của booking
/// (BillingGracePeriodMinutes + OverstayMultiplier trong PriceSnapshot); biểu giá bậc lũy tiến
/// lấy từ Rate Card hiệu lực của bãi tại thời điểm vào (snapshot JSON hiện chưa đủ cấu trúc để tái tạo RateCard).
/// </summary>
public sealed class ComputeSessionFeeUseCase(
    IRateCardRepository rateCards,
    IHolidayRepository holidayRepo) : IComputeSessionFeeUseCase
{
    public async Task<SessionFeeDto> ExecuteAsync(SessionFeeQuery query, CancellationToken cancellationToken = default)
    {
        if (query.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (query.ExitAtUtc <= query.EntryAtUtc) throw new ValidationException("Giờ ra phải sau giờ vào.");
        if (!Enum.TryParse<VehicleType>(query.VehicleType, ignoreCase: true, out var vehicleType))
            throw new ValidationException($"Loại xe '{query.VehicleType}' không hợp lệ.");

        var card = await rateCards.GetActiveAsync(query.ParkingLotId, query.EntryAtUtc, cancellationToken)
            ?? throw new NotFoundException("Bảng giá của bãi", query.ParkingLotId);

        var startVnDate = DateOnly.FromDateTime(query.EntryAtUtc.AddHours(7));
        var endVnDate = DateOnly.FromDateTime(query.ExitAtUtc.AddHours(7));
        var holidays = await holidayRepo.GetHolidaysInRangeAsync(startVnDate, endVnDate, cancellationToken);

        var q = PriceCalculator.CalculateSession(card, vehicleType, query.EntryAtUtc, query.ExitAtUtc,
            query.BookedEndAtUtc, query.BillingGracePeriodMinutes ?? PriceCalculator.DefaultGracePeriodMinutes,
            query.OverstayMultiplier, holidays);

        return new SessionFeeDto(query.ParkingLotId, card.Id, vehicleType.ToString(), q.DurationMinutes,
            q.IsWithinBillingGrace, q.BaseAmount, q.Multiplier, q.OverstayMinutes, q.OverstayAmount, q.FinalAmount, q.Lines);
    }
}

// ===== UC-22 bước 4 / UC-23 A2: Staff xác nhận thu tiền mặt tại cổng và ghi thanh toán =====

/// <summary>Port ghi giao dịch thanh toán – Infrastructure cài bằng PaymentDbContext.</summary>
public interface IPaymentWriter
{
    /// <summary>Tìm giao dịch theo khóa chống trừ tiền 2 lần (idempotent cho callback/thao tác lặp).</summary>
    Task<Payment?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

public sealed record GateSettlementCommand(string SessionCode, int ParkingSessionId, int ParkingLotId, int OwnerProfileId,
    string? BookingCode, decimal Amount, string Method, int? ConfirmedByStaffId);

public interface IRecordGateSettlementUseCase
{
    Task<PaymentDto> ExecuteAsync(GateSettlementCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Staff xác nhận đã thu tiền mặt/quét VietQR tại cổng ra (UC-22 A2, US-048 AC2) → ghi Payment Succeeded.
/// Idempotent theo SessionCode: gọi lặp (mất mạng, bấm 2 lần) không tạo giao dịch trùng.
/// </summary>
public sealed class RecordGateSettlementUseCase(IPaymentWriter writer, TimeProvider timeProvider) : IRecordGateSettlementUseCase
{
    public async Task<PaymentDto> ExecuteAsync(GateSettlementCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.SessionCode)) throw new ValidationException("SessionCode không được để trống.");
        if (command.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (command.Amount < 0) throw new ValidationException("Số tiền thu tại cổng không được âm.");
        if (!Enum.TryParse<PaymentMethod>(command.Method, ignoreCase: true, out var method))
            throw new ValidationException($"Phương thức thu tại cổng '{command.Method}' không hợp lệ (Cash hoặc VietQr).");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var idempotencyKey = $"gate-session:{command.SessionCode.Trim().ToUpperInvariant()}";

        // Idempotent: thao tác lặp trả lại giao dịch đã ghi, không trừ/tạo lần 2.
        var existing = await writer.FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
            return Map(existing);

        var payment = new Payment
        {
            Code = $"PM-{now:yyMMddHHmmssfff}{Random.Shared.Next(1000, 9999)}",
            ParkingSessionId = command.ParkingSessionId,
            ParkingLotId = command.ParkingLotId,
            OwnerProfileId = command.OwnerProfileId,
            BookingCode = command.BookingCode,
            Purpose = command.BookingCode is null ? PaymentPurpose.WalkIn : PaymentPurpose.Overstay,
            Method = method,
            Status = PaymentStatus.Succeeded,
            Amount = command.Amount,
            IdempotencyKey = idempotencyKey,
            TransferContent = command.SessionCode,
            PaidAtUtc = now,
            ConfirmedByStaffId = command.ConfirmedByStaffId,
        };

        await writer.AddAsync(payment, cancellationToken);
        await writer.SaveAsync(cancellationToken);
        return Map(payment);
    }

    private static PaymentDto Map(Payment p) => new(p.Id, p.Code, p.BookingId, p.BookingCode, p.Method.ToString(),
        p.Status.ToString(), p.Amount, p.RefundedAmount, p.ProviderTransactionId, p.PaidAtUtc, null);
}
