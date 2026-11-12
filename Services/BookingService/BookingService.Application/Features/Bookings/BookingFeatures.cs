using System.Security.Cryptography;
using System.Text;
using BookingService.Domain.Entities;
using BookingService.Domain.Rules;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace BookingService.Application.Features.Bookings;

// ===== DTOs (Data Transfer Objects) =====
public sealed record BookingSummaryDto(int Id, string Code, string Status, string ParkingLotName, string? SlotCode,
    string PlateNumber, DateTime StartAtUtc, DateTime EndAtUtc, decimal TotalAmount);

public sealed record BookingDetailDto(int Id, string Code, string Status, int UserId, int VehicleId, string PlateNumber, string VehicleType,
    int ParkingLotId, string ParkingLotName, int? ZoneId, int? SlotId, string? SlotCode, DateTime StartAtUtc, DateTime EndAtUtc,
    DateTime? HoldExpiresAtUtc, decimal TotalAmount, decimal DiscountAmount, decimal PaidAmount, string? PromotionCode,
    PriceSnapshotDto? PriceSnapshot, IReadOnlyList<StatusLogDto> History, string? QrToken = null);

public sealed record PriceSnapshotDto(int RateCardId, decimal BaseAmount, decimal SurchargeAmount, decimal DiscountAmount,
    decimal FinalAmount, int BillingGracePeriodMinutes, string RateCardJson);

public sealed record StatusLogDto(string? FromStatus, string ToStatus, int? ChangedByUserId, string? Reason, DateTime AtUtc);

/// <summary>UC-20 (US-076): 1 dòng booking trong ca trực — chỉ biển số/thông tin vận hành, KHÔNG có SĐT/CCCD (quy tắc riêng tư).</summary>
public sealed record GateBookingListItemDto(string Code, string PlateNumber, string VehicleType, string? SlotCode,
    DateTime StartAtUtc, DateTime EndAtUtc, string Status, bool IsOverdue, decimal TotalAmount);

/// <summary>Tham số list booking trong ca: grace dùng để tính cờ "Quá giờ" (US-044).</summary>
public sealed record GateBookingListQuery(int ParkingLotId, DateTime FromUtc, DateTime ToUtc, string? Search, int GracePeriodMinutes = 15);

public sealed record CancellationPreviewDto(string Code, bool CanCancel, string ResultStatus, int RefundPercent, decimal RefundAmount, string Reason);

public sealed record CancellationResultDto(string Code, string Status, int RefundPercent, decimal RefundAmount, string Reason);

// ===== REQUEST COMMANDs (API Contracts cho TV2, TV4, TV5, TV7) =====
public sealed record CreateBookingCommand(
    int UserId,
    int VehicleId,
    string PlateNumber,
    VehicleType VehicleType,
    int ParkingLotId,
    int OwnerProfileId,
    string ParkingLotName,
    int? ZoneId,
    int? SlotId,
    string? SlotCode,
    AllocationMode AllocationMode,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    decimal TotalAmount,
    string? PromotionCode,
    bool RequiresOwnerApproval = false,
    bool PayAtGate = false);

public sealed record CancelBookingCommand(int UserId, string? Reason = null);

public sealed record ModifyBookingCommand(
    int UserId,
    int? NewVehicleId,
    string? NewPlateNumber,
    DateTime? NewStartAtUtc,
    DateTime? NewEndAtUtc);

public sealed record ExtendBookingCommand(int UserId, DateTime NewEndAtUtc);

public sealed record ReviewBookingCommand(int OwnerProfileId, string? Reason = null);

public sealed record LotCancelBookingCommand(int OwnerProfileId, string Reason);

public sealed record GateActionCommand(int? GateDeviceId = null, int? StaffUserId = null, string? PlateNumber = null, decimal? AdditionalFee = null);

public sealed record ApprovalLogDto(
    int Id,
    int BookingId,
    string BookingCode,
    string PlateNumber,
    int ParkingLotId,
    string ParkingLotName,
    string Result,
    int? ReviewedByUserId,
    string? Reason,
    DateTime CreatedAtUtc);

// ===== US-039: DANH SÁCH BOOKING VƯỢT 30 PHÚT ÂN HẠN CHECK-IN (CHO STAFF HỦY NO-SHOW – UC-24) =====
public sealed record NoShowCandidateDto(string Code, int UserId, string PlateNumber, int ParkingLotId, string ParkingLotName,
    string? SlotCode, DateTime StartAtUtc, DateTime EndAtUtc, decimal TotalAmount, int MinutesOverdue);

// ===== US-103: PORT ĐẾM TẦN SUẤT ĐẶT/HỦY & TỶ LỆ NO-SHOW CỦA MỘT TÀI KHOẢN =====
public sealed record NoShowStatistics(int FinishedBookings, int NoShowBookings);

public interface IBookingAbuseQueries
{
    /// <summary>Số booking tài khoản tạo trong khoảng [since, now].</summary>
    Task<int> CountCreatedSinceAsync(int userId, DateTime sinceUtc, CancellationToken cancellationToken = default);
    /// <summary>Số lượt khách tự hủy (Cancelled / CancelledNoRefund, CancelledByUserId = khách) trong khoảng [since, now].</summary>
    Task<int> CountCancellationsSinceAsync(int userId, DateTime sinceUtc, CancellationToken cancellationToken = default);
    /// <summary>Thống kê No-show trên các booking đã kết thúc trong kỳ nhìn lại (US-103 AC2).</summary>
    Task<NoShowStatistics> GetNoShowStatisticsAsync(int userId, DateTime sinceUtc, CancellationToken cancellationToken = default);
}

// ===== US-052: PORT ĐẨY BOOKING MỚI VỀ PMS CỦA BÃI (UC-51 BƯỚC 1) =====
public sealed record PmsBookingPush(string BookingCode, int ParkingLotId, string PlateNumber, string VehicleType,
    DateTime StartAtUtc, DateTime EndAtUtc, decimal TotalAmount);

public interface ILotIntegrationClient
{
    /// <summary>
    /// Đẩy booking mới về PMS của bãi qua ParkingService (webhook có chữ ký HMAC). 
    /// Trả false khi bãi không cấu hình PMS hoặc đẩy thất bại – KHÔNG được chặn việc tạo booking.
    /// </summary>
    Task<bool> PushBookingCreatedAsync(PmsBookingPush booking, CancellationToken cancellationToken = default);
}

// US-039 (UC-24): port đọc riêng (tách khỏi IBookingQueries để không ép các fake hiện có phải cài thêm).
public interface INoShowCandidateQueries
{
    /// <summary>Booking Confirmed chưa check-in đã quá {graceMinutes} phút kể từ giờ bắt đầu, lọc theo bãi.</summary>
    Task<IReadOnlyList<NoShowCandidateDto>> ListNoShowCandidatesAsync(int parkingLotId, DateTime nowUtc, int graceMinutes, CancellationToken cancellationToken);
}

// ===== PORTS =====
public interface IBookingQueries
{
    Task<BookingDetailDto?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyList<BookingSummaryDto>> ListByUserAsync(int userId, BookingStatus? status, CancellationToken cancellationToken);

    /// <summary>UC-20 (US-076): booking của 1 bãi trong khung giờ ca trực cho Staff App — KHÔNG chứa SĐT/CCCD của khách.</summary>
    Task<IReadOnlyList<GateBookingListItemDto>> ListForGateAsync(int parkingLotId, DateTime fromUtc, DateTime toUtc,
        DateTime nowUtc, int gracePeriodMinutes, string? search, CancellationToken cancellationToken);

    /// <summary>T-303: Tra cứu lịch sử duyệt/từ chối đơn đặt chỗ bãi Mức 0 từ BookingStatusLogs.</summary>
    Task<IReadOnlyList<ApprovalLogDto>> ListApprovalLogsAsync(
        int? parkingLotId, DateTime? fromUtc, DateTime? toUtc, string? result, CancellationToken cancellationToken = default);
}

public interface IBookingRepository
{
    Task<Booking?> GetEntityByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Booking?> GetEntityByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingBookingForSlotAsync(int slotId, DateTime startUtc, DateTime endUtc, int? excludeBookingId = null, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingBookingForUserAsync(int userId, DateTime startUtc, DateTime endUtc, int? excludeBookingId = null, CancellationToken cancellationToken = default);
    Task<bool> IsSlotAssignedToActiveMonthlyPassAsync(int slotId, CancellationToken cancellationToken = default);
    Task<int> CountCancellationsTodayAsync(int userId, DateTime todayUtc, CancellationToken cancellationToken = default);
    Task AddOutboxMessageAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default);
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

// ===== QR TOKEN SERVICE (Hợp đồng bảo mật với TV7 - Không hardcode secret) =====
public interface IQrTokenService
{
    string GenerateToken(string code, string plateNumber, int parkingLotId, DateTime expiresAtUtc);
    bool ValidateToken(string token, out string? code, out string? plateNumber, out int parkingLotId);
}

public sealed class QrTokenService(TimeProvider? timeProvider = null, string? secretKey = null) : IQrTokenService
{
    private readonly string _secretKey = secretKey
        ?? Environment.GetEnvironmentVariable("QRTOKEN_SECRET")
        ?? throw new InvalidOperationException("Biến môi trường 'QRTOKEN_SECRET' chưa được cấu hình.");

    public string GenerateToken(string code, string plateNumber, int parkingLotId, DateTime expiresAtUtc)
    {
        var payload = $"{code}|{plateNumber}|{parkingLotId}|{expiresAtUtc.Ticks}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        var hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        return $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))}.{hash}";
    }

    public bool ValidateToken(string token, out string? code, out string? plateNumber, out int parkingLotId)
    {
        code = null; plateNumber = null; parkingLotId = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Split('.');
        if (parts.Length != 2) return false;

        try
        {
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var expectedHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
            if (parts[1] != expectedHash) return false;

            var fields = payload.Split('|');
            if (fields.Length != 4) return false;

            code = fields[0];
            plateNumber = fields[1];
            parkingLotId = int.Parse(fields[2]);
            var expiry = new DateTime(long.Parse(fields[3]), DateTimeKind.Utc);
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            return expiry > now;
        }
        catch
        {
            return false;
        }
    }
}

// ===== USE CASES =====

public interface IGetBookingByCodeUseCase
{
    Task<BookingDetailDto> ExecuteAsync(string code, CancellationToken cancellationToken = default);
}

public sealed class GetBookingByCodeUseCase(IBookingQueries queries) : IGetBookingByCodeUseCase
{
    public async Task<BookingDetailDto> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ValidationException("Mã booking không được để trống.");
        return await queries.GetByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }
}

public interface IListMyBookingsUseCase
{
    Task<IReadOnlyList<BookingSummaryDto>> ExecuteAsync(int userId, BookingStatus? status, CancellationToken cancellationToken = default);
}

public sealed class ListMyBookingsUseCase(IBookingQueries queries) : IListMyBookingsUseCase
{
    public Task<IReadOnlyList<BookingSummaryDto>> ExecuteAsync(int userId, BookingStatus? status, CancellationToken cancellationToken = default)
    {
        if (userId <= 0) throw new ValidationException("UserId phải là số nguyên dương.");
        return queries.ListByUserAsync(userId, status, cancellationToken);
    }
}

public interface IPreviewCancellationUseCase
{
    Task<CancellationPreviewDto> ExecuteAsync(string code, CancellationToken cancellationToken = default);
}

public sealed class PreviewCancellationUseCase(IBookingQueries queries, TimeProvider timeProvider) : IPreviewCancellationUseCase
{
    public async Task<CancellationPreviewDto> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    {
        var booking = await queries.GetByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        if (!Enum.TryParse<BookingStatus>(booking.Status, out var currentStatus))
            throw new ValidationException("Trạng thái booking không hợp lệ.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var decision = CancellationPolicy.Evaluate(currentStatus, booking.StartAtUtc, now);
        var refundAmount = Math.Round(booking.PaidAmount * decision.RefundPercent / 100m, 0);

        return new CancellationPreviewDto(booking.Code, decision.CanCancel, decision.ResultStatus.ToString(), decision.RefundPercent, refundAmount, decision.Reason);
    }
}

public interface ICreateBookingUseCase
{
    Task<BookingDetailDto> ExecuteAsync(CreateBookingCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-103: Lớp chặn hành vi lạm dụng đặt/hủy trước khi tạo booking.
/// Đếm tần suất đặt + hủy trong cửa sổ cấu hình và tỷ lệ No-show trong kỳ nhìn lại;
/// vi phạm → ConflictException (HTTP 409) kèm thông điệp hướng dẫn cho khách.
/// </summary>
public sealed class BookingAbuseGuard(IBookingAbuseQueries abuseQueries, TimeProvider timeProvider, BookingAbuseOptions? options = null)
{
    private readonly BookingAbuseOptions _options = options ?? BookingAbuseOptions.Default;

    public async Task EnsureCanCreateBookingAsync(int userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var windowStart = now.AddMinutes(-_options.ActionWindowMinutes);
        var created = await abuseQueries.CountCreatedSinceAsync(userId, windowStart, cancellationToken);
        var cancelled = await abuseQueries.CountCancellationsSinceAsync(userId, windowStart, cancellationToken);
        var frequency = BookingAbusePolicy.EvaluateActionFrequency(created, cancelled, _options);
        if (!frequency.Allowed)
            throw new ConflictException(frequency.RestrictionMessage!);

        var lookbackStart = now.AddDays(-_options.NoShowLookbackDays);
        var stats = await abuseQueries.GetNoShowStatisticsAsync(userId, lookbackStart, cancellationToken);
        var noShow = BookingAbusePolicy.EvaluateNoShowRate(stats.FinishedBookings, stats.NoShowBookings, _options);
        if (!noShow.Allowed)
            throw new ConflictException(noShow.RestrictionMessage!);
    }

    public async Task EnsureCanCancelBookingAsync(int userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var windowStart = now.AddMinutes(-_options.ActionWindowMinutes);
        var created = await abuseQueries.CountCreatedSinceAsync(userId, windowStart, cancellationToken);
        var cancelled = await abuseQueries.CountCancellationsSinceAsync(userId, windowStart, cancellationToken);
        var frequency = BookingAbusePolicy.EvaluateActionFrequency(created, cancelled, _options);
        if (!frequency.Allowed)
            throw new ConflictException(frequency.RestrictionMessage!);
    }
}

/// <summary>US-022: Tạo booking & giữ chỗ 15 phút (TC-BOOK-01).</summary>
public sealed class CreateBookingUseCase(
    IBookingRepository repository,
    IQrTokenService qrService,
    BookingAbuseGuard abuseGuard,
    ILotIntegrationClient lotIntegration,
    TimeProvider timeProvider,
    ILogger<CreateBookingUseCase>? logger = null) : ICreateBookingUseCase
{
    public async Task<BookingDetailDto> ExecuteAsync(CreateBookingCommand command, CancellationToken cancellationToken = default)
    {
        if (command.UserId <= 0) throw new ValidationException("UserId không hợp lệ.");
        if (command.VehicleId <= 0) throw new ValidationException("VehicleId không hợp lệ.");
        if (command.ParkingLotId <= 0) throw new ValidationException("ParkingLotId không hợp lệ.");
        if (string.IsNullOrWhiteSpace(command.PlateNumber)) throw new ValidationException("Biển số xe không được để trống.");
        if (command.EndAtUtc <= command.StartAtUtc) throw new ValidationException("Thời gian kết thúc phải sau thời gian bắt đầu.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (command.StartAtUtc < now.AddMinutes(15))
            throw new ValidationException("Thời gian đặt chỗ phải trước giờ bắt đầu ít nhất 15 phút.");

        if (command.StartAtUtc > now.AddDays(30))
            throw new ValidationException("Chỉ được đặt chỗ trước tối đa 30 ngày.");

        // US-103 (UC-10 E1b): chặn tài khoản đặt/hủy quá tần suất hoặc vượt ngưỡng No-show trước khi giữ chỗ.
        // Chạy SAU khi kiểm tra dữ liệu đầu vào: yêu cầu sai luôn trả 400, chỉ yêu cầu hợp lệ mới bị đánh giá lạm dụng (409).
        await abuseGuard.EnsureCanCreateBookingAsync(command.UserId, cancellationToken);

        // US-025: Giới hạn 1 booking Active/Pending cùng khung giờ cho 1 tài xế (Idempotency Lock / Chống giữ chỗ ảo)
        if (await repository.HasOverlappingBookingForUserAsync(command.UserId, command.StartAtUtc, command.EndAtUtc, null, cancellationToken))
            throw new ValidationException("Bạn đã có một đơn đặt chỗ khác đang hoạt động trong khung giờ này.");

        // US-024: Chống đặt trùng chỗ (Concurrency / Double Booking Prevention)
        if (command.SlotId.HasValue && command.SlotId.Value > 0)
        {
            // Kiểm tra trùng với ô riêng của khách hàng Vé tháng (Mục 1.1)
            var isMonthlySlot = await repository.IsSlotAssignedToActiveMonthlyPassAsync(command.SlotId.Value, cancellationToken);
            if (isMonthlySlot)
            {
                var slotDisplay = string.IsNullOrWhiteSpace(command.SlotCode) ? command.SlotId.Value.ToString() : command.SlotCode;
                throw new ConflictException($"Vị trí đỗ {slotDisplay} là ô riêng của khách hàng vé tháng đang hiệu lực. Vui lòng chọn vị trí khác.");
            }

            var isSlotOccupied = await repository.HasOverlappingBookingForSlotAsync(command.SlotId.Value, command.StartAtUtc, command.EndAtUtc, null, cancellationToken);
            if (isSlotOccupied)
            {
                var slotDisplay = string.IsNullOrWhiteSpace(command.SlotCode) ? command.SlotId.Value.ToString() : command.SlotCode;
                throw new ConflictException($"Vị trí đỗ {slotDisplay} đã có người đặt trước trong khung giờ này. Vui lòng chọn vị trí hoặc khung giờ khác.");
            }
        }

        var code = $"BK-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var qrToken = qrService.GenerateToken(code, command.PlateNumber, command.ParkingLotId, command.EndAtUtc.AddHours(2));

        var booking = new Booking
        {
            Code = code,
            UserId = command.UserId,
            VehicleId = command.VehicleId,
            PlateNumber = command.PlateNumber.Trim().ToUpperInvariant(),
            VehicleType = command.VehicleType,
            ParkingLotId = command.ParkingLotId,
            OwnerProfileId = command.OwnerProfileId,
            ParkingLotName = command.ParkingLotName,
            ZoneId = command.ZoneId,
            SlotId = command.SlotId,
            SlotCode = command.SlotCode,
            AllocationMode = command.AllocationMode,
            StartAtUtc = command.StartAtUtc,
            EndAtUtc = command.EndAtUtc,
            RequiresOwnerApproval = command.RequiresOwnerApproval,
            TotalAmount = command.TotalAmount,
            PaidAmount = 0,
            PromotionCode = command.PromotionCode,
            QrToken = qrToken
        };

        // Kích hoạt State Machine: Bắt đầu giữ chỗ (nếu PayAtGate thì giữ đến giờ hẹn + 15p, nếu online giữ 15p)
        booking.MarkAsPendingPayment(now, command.PayAtGate, command.StartAtUtc);

        // Lưu PriceSnapshot khóa giá lúc đặt
        booking.PriceSnapshot = new PriceSnapshot
        {
            BookingId = booking.Id,
            RateCardId = 1,
            BaseAmount = command.TotalAmount,
            SurchargeAmount = 0,
            DiscountAmount = 0,
            FinalAmount = command.TotalAmount,
            BillingGracePeriodMinutes = 15,
            RateCardJson = "{}"
        };

        await repository.AddAsync(booking, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        // US-052 (UC-51 bước 1): đẩy booking mới về PMS của bãi (nếu bãi cấu hình tích hợp Mức 1).
        // Lỗi đẩy webhook KHÔNG được làm hỏng booking – chỉ ghi log để đối soát/retry sau.
        try
        {
            var pushed = await lotIntegration.PushBookingCreatedAsync(new PmsBookingPush(
                booking.Code, booking.ParkingLotId, booking.PlateNumber, booking.VehicleType.ToString(),
                booking.StartAtUtc, booking.EndAtUtc, booking.TotalAmount), cancellationToken);
            if (!pushed)
                logger?.LogInformation("Bãi {ParkingLotId} chưa cấu hình PMS hoặc đẩy webhook thất bại – booking {Code} vẫn hợp lệ.",
                    booking.ParkingLotId, booking.Code);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Không đẩy được booking {Code} về PMS của bãi {ParkingLotId}.",
                booking.Code, booking.ParkingLotId);
        }

        return new BookingDetailDto(
            booking.Id, booking.Code, booking.Status.ToString(), booking.UserId, booking.VehicleId,
            booking.PlateNumber, booking.VehicleType.ToString(), booking.ParkingLotId, booking.ParkingLotName,
            booking.ZoneId, booking.SlotId, booking.SlotCode, booking.StartAtUtc, booking.EndAtUtc,
            booking.HoldExpiresAtUtc, booking.TotalAmount, booking.DiscountAmount, booking.PaidAmount,
            booking.PromotionCode, null,
            booking.StatusLogs.Select(l => new StatusLogDto(l.FromStatus?.ToString(), l.ToStatus.ToString(), l.ChangedByUserId, l.Reason, l.CreatedAtUtc)).ToList(),
            booking.QrToken);
    }
}

public interface ICancelBookingUseCase
{
    Task<CancellationResultDto> ExecuteAsync(string code, CancelBookingCommand command, CancellationToken cancellationToken = default);
}

/// <summary>US-038: Khách hủy booking theo Cancellation Window 1 giờ. US-103: chặn hủy quá tần suất.</summary>
public sealed class CancelBookingUseCase(
    IBookingRepository repository,
    BookingAbuseGuard abuseGuard,
    TimeProvider timeProvider) : ICancelBookingUseCase
{
    public async Task<CancellationResultDto> ExecuteAsync(string code, CancelBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetEntityByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var decision = CancellationPolicy.Evaluate(booking.Status, booking.StartAtUtc, now);
        if (!decision.CanCancel)
            throw new ValidationException(decision.Reason);

        // US-103 (AC1): chặn tài khoản hủy/đặt quá tần suất – chỉ đánh giá khi yêu cầu hủy đã hợp lệ theo chính sách.
        await abuseGuard.EnsureCanCancelBookingAsync(command.UserId, cancellationToken);

        // Gọi phương thức State Machine của Entity
        booking.CancelByCustomer(decision.ResultStatus, now, command.UserId, command.Reason ?? decision.Reason);

        var refund = Math.Round(booking.PaidAmount * decision.RefundPercent / 100m, 0);

        // US-038: Ghi sự kiện vào bảng OutboxMessages để PaymentService thực hiện hoàn tiền VNPAY
        if (refund > 0)
        {
            var cancelEvent = new BookingCancelled(booking.Id, booking.Code, booking.UserId, refund, command.Reason ?? decision.Reason);
            await repository.AddOutboxMessageAsync(nameof(BookingCancelled), System.Text.Json.JsonSerializer.Serialize(cancelEvent), cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return new CancellationResultDto(booking.Code, booking.Status.ToString(), decision.RefundPercent, refund, decision.Reason);
    }
}

public interface IListNoShowCandidatesUseCase
{
    Task<IReadOnlyList<NoShowCandidateDto>> ExecuteAsync(int parkingLotId, int graceMinutes = 30, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-039 (UC-24 bước 1): hệ thống gửi cảnh báo No-show cho Staff – danh sách booking Confirmed
/// chưa check-in đã quá mốc ân hạn 30 phút (NO_SHOW_CANCEL_AFTER_MINUTES) kể từ giờ bắt đầu.
/// </summary>
public sealed class ListNoShowCandidatesUseCase(INoShowCandidateQueries queries, TimeProvider timeProvider) : IListNoShowCandidatesUseCase
{
    public Task<IReadOnlyList<NoShowCandidateDto>> ExecuteAsync(int parkingLotId, int graceMinutes = 30, CancellationToken cancellationToken = default)
    {
        if (parkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (graceMinutes <= 0) graceMinutes = 30;
        return queries.ListNoShowCandidatesAsync(parkingLotId, timeProvider.GetUtcNow().UtcDateTime, graceMinutes, cancellationToken);
    }
}

public interface IModifyBookingUseCase
{
    Task<BookingDetailDto> ExecuteAsync(string code, ModifyBookingCommand command, CancellationToken cancellationToken = default);
}

/// <summary>US-029: Sửa booking trước Check-in (đổi giờ hoặc đổi xe).</summary>
public sealed class ModifyBookingUseCase(
    IBookingRepository repository,
    IBookingQueries queries,
    IQrTokenService qrService,
    TimeProvider timeProvider) : IModifyBookingUseCase
{
    public async Task<BookingDetailDto> ExecuteAsync(string code, ModifyBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetEntityByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        if (booking.Status != BookingStatus.Confirmed && booking.Status != BookingStatus.PendingPayment)
            throw new ValidationException($"Chỉ có thể sửa booking khi ở trạng thái Confirmed hoặc PendingPayment. Hiện tại: {booking.Status}");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (booking.StartAtUtc <= now)
            throw new ValidationException("Không thể sửa booking khi đã đến hoặc quá giờ bắt đầu.");

        // Quy tắc chống lách luật: Đổi giờ phải trước giờ hẹn cũ ít nhất 60 phút
        if (command.NewStartAtUtc.HasValue && booking.StartAtUtc - now < TimeSpan.FromMinutes(60))
        {
            throw new ValidationException("Chỉ được phép đổi giờ đặt chỗ trước giờ hẹn cũ ít nhất 60 phút. Do thời gian còn lại dưới 60 phút, bạn không thể thay đổi giờ hẹn.");
        }

        if (command.NewStartAtUtc.HasValue && command.NewEndAtUtc.HasValue)
        {
            if (command.NewEndAtUtc <= command.NewStartAtUtc)
                throw new ValidationException("Thời gian kết thúc mới phải sau thời gian bắt đầu mới.");

            // Kiểm tra trùng chỗ cho khung giờ mới (US-024)
            if (booking.SlotId.HasValue && booking.SlotId.Value > 0)
            {
                var isSlotOccupied = await repository.HasOverlappingBookingForSlotAsync(
                    booking.SlotId.Value, command.NewStartAtUtc.Value, command.NewEndAtUtc.Value, booking.Id, cancellationToken);
                if (isSlotOccupied)
                    throw new ConflictException($"Vị trí đỗ {booking.SlotCode ?? booking.SlotId.Value.ToString()} đã có người đặt trước trong khung giờ mới này. Vui lòng chọn khung giờ khác.");
            }

            // Kiểm tra trùng lịch của chính user (US-025)
            if (await repository.HasOverlappingBookingForUserAsync(booking.UserId, command.NewStartAtUtc.Value, command.NewEndAtUtc.Value, booking.Id, cancellationToken))
                throw new ValidationException("Bạn đã có một đơn đặt chỗ khác đang hoạt động trong khung giờ mới này.");

            booking.Modifications.Add(new BookingModification
            {
                BookingId = booking.Id,
                ModificationType = BookingModificationType.ChangeTime,
                OldStartAtUtc = booking.StartAtUtc,
                OldEndAtUtc = booking.EndAtUtc,
                NewStartAtUtc = command.NewStartAtUtc.Value,
                NewEndAtUtc = command.NewEndAtUtc.Value,
                RequestedByUserId = command.UserId
            });

            booking.StartAtUtc = command.NewStartAtUtc.Value;
            booking.EndAtUtc = command.NewEndAtUtc.Value;

            // Cập nhật lại thời hạn của QrToken
            booking.QrToken = qrService.GenerateToken(booking.Code, booking.PlateNumber, booking.ParkingLotId, booking.EndAtUtc.AddHours(2));
        }

        if (command.NewVehicleId.HasValue && !string.IsNullOrWhiteSpace(command.NewPlateNumber))
        {
            booking.Modifications.Add(new BookingModification
            {
                BookingId = booking.Id,
                ModificationType = BookingModificationType.ChangeVehicle,
                OldVehicleId = booking.VehicleId,
                NewVehicleId = command.NewVehicleId.Value,
                RequestedByUserId = command.UserId
            });

            booking.VehicleId = command.NewVehicleId.Value;
            booking.PlateNumber = command.NewPlateNumber.Trim().ToUpperInvariant();

            // Sửa bug mục 4: Đổi xe phải cập nhật lại chữ ký số QR Token theo biển số mới
            booking.QrToken = qrService.GenerateToken(booking.Code, booking.PlateNumber, booking.ParkingLotId, booking.EndAtUtc.AddHours(2));
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await queries.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }
}

public interface IExtendBookingUseCase
{
    Task<BookingDetailDto> ExecuteAsync(string code, ExtendBookingCommand command, CancellationToken cancellationToken = default);
}

/// <summary>US-030: Gia hạn thời gian đỗ xe.</summary>
public sealed class ExtendBookingUseCase(
    IBookingRepository repository,
    IBookingQueries queries,
    IQrTokenService qrService) : IExtendBookingUseCase
{
    public async Task<BookingDetailDto> ExecuteAsync(string code, ExtendBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetEntityByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        if (booking.Status != BookingStatus.CheckedIn && booking.Status != BookingStatus.Parking && booking.Status != BookingStatus.Confirmed)
            throw new ValidationException($"Chỉ có thể gia hạn khi đang đỗ hoặc đã xác nhận. Hiện tại: {booking.Status}");

        if (command.NewEndAtUtc <= booking.EndAtUtc)
            throw new ValidationException("Thời gian gia hạn mới phải sau thời gian kết thúc hiện tại.");

        // Sửa bug mục 6: Kiểm tra vị trí đỗ có bị ai đặt trong khoảng [booking.EndAtUtc, command.NewEndAtUtc] không
        if (booking.SlotId.HasValue && booking.SlotId.Value > 0)
        {
            var isSlotOccupied = await repository.HasOverlappingBookingForSlotAsync(
                booking.SlotId.Value, booking.EndAtUtc, command.NewEndAtUtc, booking.Id, cancellationToken);
            if (isSlotOccupied)
                throw new ConflictException($"Không thể gia hạn do vị trí đỗ {booking.SlotCode ?? booking.SlotId.Value.ToString()} đã có khách khác đặt trước trong khoảng thời gian tiếp theo.");
        }

        // Sửa bug mục 6: Tính phụ phí gia hạn đỗ xe
        var oldDurationHours = (decimal)Math.Max(1, Math.Ceiling((booking.EndAtUtc - booking.StartAtUtc).TotalHours));
        var addedHours = (decimal)Math.Ceiling((command.NewEndAtUtc - booking.EndAtUtc).TotalHours);
        var baseHourlyRate = Math.Round(booking.TotalAmount / oldDurationHours, 0);
        var additionalFee = baseHourlyRate * addedHours;

        booking.Modifications.Add(new BookingModification
        {
            BookingId = booking.Id,
            ModificationType = BookingModificationType.ChangeTime,
            OldStartAtUtc = booking.StartAtUtc,
            OldEndAtUtc = booking.EndAtUtc,
            NewStartAtUtc = booking.StartAtUtc,
            NewEndAtUtc = command.NewEndAtUtc,
            RequestedByUserId = command.UserId
        });

        booking.EndAtUtc = command.NewEndAtUtc;
        booking.TotalAmount += additionalFee;

        // Cập nhật lại thời hạn QrToken tương ứng với giờ kết thúc mới
        booking.QrToken = qrService.GenerateToken(booking.Code, booking.PlateNumber, booking.ParkingLotId, booking.EndAtUtc.AddHours(2));

        await repository.SaveChangesAsync(cancellationToken);

        return await queries.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }
}

public interface IReviewBookingUseCase
{
    Task<BookingDetailDto> ApproveAsync(string code, ReviewBookingCommand command, CancellationToken cancellationToken = default);
    Task<BookingDetailDto> RejectAsync(string code, ReviewBookingCommand command, CancellationToken cancellationToken = default);
}

/// <summary>US-031: Chủ bãi duyệt hoặc từ chối booking tại bãi Mức 0 (thủ công).</summary>
public sealed class ReviewBookingUseCase(
    IBookingRepository repository,
    IBookingQueries queries,
    TimeProvider timeProvider) : IReviewBookingUseCase
{
    public async Task<BookingDetailDto> ApproveAsync(string code, ReviewBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetEntityByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        if (booking.OwnerProfileId != command.OwnerProfileId)
            throw new ValidationException("Bạn không có quyền duyệt booking của bãi này.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        booking.ApproveByOwner(now, command.OwnerProfileId, command.Reason);

        await repository.SaveChangesAsync(cancellationToken);
        return await queries.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }

    public async Task<BookingDetailDto> RejectAsync(string code, ReviewBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetEntityByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        if (booking.OwnerProfileId != command.OwnerProfileId)
            throw new ValidationException("Bạn không có quyền từ chối booking của bãi này.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        booking.RejectByOwner(now, command.OwnerProfileId, command.Reason ?? "Chủ bãi từ chối do hết chỗ");

        await repository.SaveChangesAsync(cancellationToken);
        return await queries.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }
}

public interface ILotCancelBookingUseCase
{
    Task<BookingDetailDto> ExecuteAsync(string code, LotCancelBookingCommand command, CancellationToken cancellationToken = default);
}

/// <summary>US-042: Chủ bãi hủy booking do bãi gặp sự cố bất khả kháng (Hoàn tiền 100%).</summary>
public sealed class LotCancelBookingUseCase(
    IBookingRepository repository,
    IBookingQueries queries,
    TimeProvider timeProvider) : ILotCancelBookingUseCase
{
    public async Task<BookingDetailDto> ExecuteAsync(string code, LotCancelBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetEntityByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        if (booking.OwnerProfileId != command.OwnerProfileId)
            throw new ValidationException("Bạn không có quyền hủy booking của bãi này.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        booking.CancelByOwner(now, command.OwnerProfileId, command.Reason);

        await repository.SaveChangesAsync(cancellationToken);
        return await queries.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }
}

public interface IGateBookingActionsUseCase
{
    Task<BookingDetailDto> CheckInAsync(string code, GateActionCommand command, CancellationToken cancellationToken = default);
    Task<BookingDetailDto> CheckOutAsync(string code, GateActionCommand command, CancellationToken cancellationToken = default);
    Task<BookingDetailDto> NoShowAsync(string code, GateActionCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Nội bộ cho TV7 (GateService): Check-in, Check-out, No-show.</summary>
public sealed class GateBookingActionsUseCase(
    IBookingRepository repository,
    IBookingQueries queries,
    TimeProvider timeProvider) : IGateBookingActionsUseCase
{
    public async Task<BookingDetailDto> CheckInAsync(string code, GateActionCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetEntityByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Chặn check-in quá sớm trước giờ hẹn hơn 15 phút
        if (now < booking.StartAtUtc.AddMinutes(-15))
        {
            var earlyMinutes = (int)Math.Ceiling((booking.StartAtUtc - now).TotalMinutes);
            throw new ValidationException($"Chưa đến khung giờ check-in hợp lệ. Bạn đến sớm {earlyMinutes} phút (chỉ cho phép vào trước giờ hẹn tối đa 15 phút).");
        }

        // Chặn check-in khi đã quá hạn giờ kết thúc
        if (now > booking.EndAtUtc)
        {
            throw new ValidationException($"Đơn đặt chỗ đã quá hạn giờ kết thúc ({booking.EndAtUtc:HH:mm dd/MM/yyyy UTC}), không thể check-in.");
        }

        booking.CheckIn(now, command.StaffUserId, command.GateDeviceId);

        await repository.SaveChangesAsync(cancellationToken);
        return await queries.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }

    public async Task<BookingDetailDto> CheckOutAsync(string code, GateActionCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetEntityByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        booking.CheckOut(now, command.StaffUserId, command.GateDeviceId);

        await repository.SaveChangesAsync(cancellationToken);
        return await queries.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }

    public async Task<BookingDetailDto> NoShowAsync(string code, GateActionCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetEntityByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        booking.MarkAsNoShow(now, command.StaffUserId);

        await repository.SaveChangesAsync(cancellationToken);
        return await queries.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }
}

// ===== UC-20 (US-076): Danh sách booking trong ca trực cho Staff App =====

public interface IListGateBookingsUseCase
{
    Task<IReadOnlyList<GateBookingListItemDto>> ExecuteAsync(GateBookingListQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Staff xem booking của bãi được gán trong ca trực, tìm theo mã booking/biển số (UC-20).
/// Gồm booking sắp đến (Confirmed trong khung giờ), xe đang trong bãi (CheckedIn/Parking) và No-show trong ca.
/// Cờ IsOverdue = đến giờ + 15' ân hạn (US-044) mà khách chưa check-in → Staff chuẩn bị cảnh báo/hủy no-show.
/// Kết quả KHÔNG chứa SĐT/CCCD của khách (quy tắc riêng tư UC-20).
/// </summary>
public sealed class ListGateBookingsUseCase(IBookingQueries queries, TimeProvider timeProvider) : IListGateBookingsUseCase
{
    private const int MaxWindowDays = 7;

    public async Task<IReadOnlyList<GateBookingListItemDto>> ExecuteAsync(GateBookingListQuery query, CancellationToken cancellationToken = default)
    {
        if (query.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (query.ToUtc <= query.FromUtc) throw new ValidationException("Khung giờ ca trực không hợp lệ (toUtc phải sau fromUtc).");
        if ((query.ToUtc - query.FromUtc).TotalDays > MaxWindowDays)
            throw new ValidationException($"Khung giờ tra cứu tối đa {MaxWindowDays} ngày.");
        if (query.GracePeriodMinutes < 0) throw new ValidationException("gracePeriodMinutes không được âm.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return await queries.ListForGateAsync(query.ParkingLotId, query.FromUtc, query.ToUtc, now,
            query.GracePeriodMinutes, query.Search, cancellationToken);
    }
}

// ===== T-302: SAGA BOOKING â” PAYMENT (EVENT-DRIVEN RABBITMQ) =====

public interface IProcessPaymentResultUseCase
{
    Task<BookingDetailDto> ProcessPaymentSucceededAsync(PaymentSucceeded @event, CancellationToken cancellationToken = default);
    Task<BookingDetailDto> ProcessPaymentFailedAsync(PaymentFailed @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// T-302: Saga Booking â” Payment: Láº¯ng nghe káº¿t quáº£ thanh toĂ¡n tá»« PaymentService qua RabbitMQ.
/// Chuyá»ƒn Ä‘á»•i tráº¡ng thĂ¡i Ä‘Æ¡n Ä‘áº·t chá»— vĂ  xá»­ lĂ½ bĂ¹ trá»« (hoĂ n tiá»n) khi cĂ³ sá»± cá»‘.
/// </summary>
public sealed class ProcessPaymentResultUseCase(
    IBookingRepository repository,
    IBookingQueries queries,
    TimeProvider timeProvider) : IProcessPaymentResultUseCase
{
    public async Task<BookingDetailDto> ProcessPaymentSucceededAsync(PaymentSucceeded @event, CancellationToken cancellationToken = default)
    {
        if (!@event.BookingId.HasValue)
            throw new ValidationException("Sự kiện PaymentSucceeded không có BookingId.");

        var booking = await repository.GetEntityByIdAsync(@event.BookingId.Value, cancellationToken)
            ?? throw new NotFoundException("Booking", @event.BookingId.Value.ToString());

        // Idempotency: Nếu đơn đã được xác nhận hoặc duyệt rồi thì bỏ qua không xử lý lặp
        if (booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.PendingOwnerApproval)
        {
            return await queries.GetByCodeAsync(booking.Code, cancellationToken)
                ?? throw new NotFoundException("Booking", booking.Code);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Kiểm tra quá hạn thời gian giữ chỗ 15 phút (HoldExpiresAtUtc)
        if (booking.HoldExpiresAtUtc.HasValue && now > booking.HoldExpiresAtUtc.Value)
        {
            // Đã quá hạn giữ chỗ 15 phút -> Kích hoạt Expired
            booking.Expire(now, $"Thanh toán thành công {@event.Amount:N0}đ qua {@event.Method} nhưng đơn đã quá hạn giữ chỗ 15 phút. Kích hoạt hoàn tiền bù trừ.");

            // Ghi sự kiện vào OutboxMessages để PaymentService tự động hoàn tiền bù trừ
            var cancelEvent = new BookingCancelled(booking.Id, booking.Code, booking.UserId, @event.Amount, "Thanh toán thành công nhưng quá hạn giữ chỗ 15 phút. Yêu cầu hoàn tiền tự động.");
            await repository.AddOutboxMessageAsync(nameof(BookingCancelled), System.Text.Json.JsonSerializer.Serialize(cancelEvent), cancellationToken);

            await repository.SaveChangesAsync(cancellationToken);
            return await queries.GetByCodeAsync(booking.Code, cancellationToken)
                ?? throw new NotFoundException("Booking", booking.Code);
        }


        // Nếu bãi Mức 0: Chờ chủ bãi duyệt
        if (booking.RequiresOwnerApproval)
        {
            booking.RequireOwnerApproval();
            booking.PaidAmount = @event.Amount;
        }
        else
        {
            // Bãi tự động (Mức 1-3): Xác nhận giữ chỗ thành công ngay lập tức
            booking.ConfirmPayment(@event.Amount, now);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await queries.GetByCodeAsync(booking.Code, cancellationToken)
            ?? throw new NotFoundException("Booking", booking.Code);
    }

    public async Task<BookingDetailDto> ProcessPaymentFailedAsync(PaymentFailed @event, CancellationToken cancellationToken = default)
    {
        if (!@event.BookingId.HasValue)
            throw new ValidationException("Sự kiện PaymentFailed không có BookingId.");

        var booking = await repository.GetEntityByIdAsync(@event.BookingId.Value, cancellationToken)
            ?? throw new NotFoundException("Booking", @event.BookingId.Value.ToString());

        if (booking.Status == BookingStatus.Cancelled)
        {
            return await queries.GetByCodeAsync(booking.Code, cancellationToken)
                ?? throw new NotFoundException("Booking", booking.Code);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        booking.FailPayment(@event.Reason, now);

        await repository.SaveChangesAsync(cancellationToken);
        return await queries.GetByCodeAsync(booking.Code, cancellationToken)
            ?? throw new NotFoundException("Booking", booking.Code);
    }
}

// ===== T-303: APPROVAL LOGS (OWNER / ADMIN PORTAL) =====
public interface IGetApprovalLogsUseCase
{
    Task<IReadOnlyList<ApprovalLogDto>> ExecuteAsync(
        int? parkingLotId, DateTime? fromUtc, DateTime? toUtc, string? result, CancellationToken cancellationToken = default);
}

public sealed class GetApprovalLogsUseCase(IBookingQueries queries) : IGetApprovalLogsUseCase
{
    public Task<IReadOnlyList<ApprovalLogDto>> ExecuteAsync(
        int? parkingLotId, DateTime? fromUtc, DateTime? toUtc, string? result, CancellationToken cancellationToken = default)
    {
        if (fromUtc.HasValue && toUtc.HasValue && toUtc < fromUtc)
            throw new ValidationException("Khoảng thời gian tra cứu không hợp lệ (toUtc phải sau fromUtc).");

        return queries.ListApprovalLogsAsync(parkingLotId, fromUtc, toUtc, result, cancellationToken);
    }
}
