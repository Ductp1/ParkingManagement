using System.Security.Cryptography;
using System.Text;
using BookingService.Domain.Entities;
using BookingService.Domain.Rules;
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
    bool RequiresOwnerApproval = false);

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

public sealed record GateActionCommand(int GateDeviceId, int StaffUserId, string? PlateNumber = null, decimal? AdditionalFee = null);

// ===== PORTS =====
public interface IBookingQueries
{
    Task<BookingDetailDto?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyList<BookingSummaryDto>> ListByUserAsync(int userId, BookingStatus? status, CancellationToken cancellationToken);
}

public interface IBookingRepository
{
    Task<Booking?> GetEntityByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

// ===== QR TOKEN SERVICE (Hợp đồng bảo mật với TV7 - Hạn 16/10) =====
public interface IQrTokenService
{
    string GenerateToken(string code, string plateNumber, int parkingLotId, DateTime expiresAtUtc);
    bool ValidateToken(string token, out string? code, out string? plateNumber, out int parkingLotId);
}

public sealed class QrTokenService(TimeProvider? timeProvider = null) : IQrTokenService
{
    private const string SecretKey = "SmartParking_BookingService_HMACSHA256_SecretKey_2026";

    public string GenerateToken(string code, string plateNumber, int parkingLotId, DateTime expiresAtUtc)
    {
        var payload = $"{code}|{plateNumber}|{parkingLotId}|{expiresAtUtc.Ticks}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretKey));
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
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretKey));
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
        if (string.IsNullOrWhiteSpace(code)) throw new ValidationException("Mã booking không được rỗng.");
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
        if (userId <= 0) throw new ValidationException("userId phải là số nguyên dương.");
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

        var status = Enum.Parse<BookingStatus>(booking.Status);
        var decision = CancellationPolicy.Evaluate(status, booking.StartAtUtc, timeProvider.GetUtcNow().UtcDateTime);
        var refund = Math.Round(booking.PaidAmount * decision.RefundPercent / 100m, 0);

        return new CancellationPreviewDto(booking.Code, decision.CanCancel, decision.ResultStatus.ToString(),
            decision.RefundPercent, refund, decision.Reason);
    }
}

public interface ICreateBookingUseCase
{
    Task<BookingDetailDto> ExecuteAsync(CreateBookingCommand command, CancellationToken cancellationToken = default);
}

/// <summary>US-022: Tạo booking & giữ chỗ 15 phút (vòng đời trạng thái Created -> PendingPayment).</summary>
public sealed class CreateBookingUseCase(
    IBookingRepository repository,
    IQrTokenService qrService,
    TimeProvider timeProvider) : ICreateBookingUseCase
{
    public async Task<BookingDetailDto> ExecuteAsync(CreateBookingCommand command, CancellationToken cancellationToken = default)
    {
        if (command.UserId <= 0) throw new ValidationException("UserId không hợp lệ.");
        if (command.VehicleId <= 0) throw new ValidationException("VehicleId không hợp lệ.");
        if (command.ParkingLotId <= 0) throw new ValidationException("ParkingLotId không hợp lệ.");
        if (string.IsNullOrWhiteSpace(command.PlateNumber)) throw new ValidationException("Biển số xe không được để trống.");
        if (command.EndAtUtc <= command.StartAtUtc) throw new ValidationException("Thời gian kết thúc phải sau thời gian bắt đầu.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (command.StartAtUtc < now.AddMinutes(-5))
            throw new ValidationException("Không thể đặt chỗ cho khung giờ trong quá khứ.");

        var code = $"BK-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var holdExpires = now.AddMinutes(15);
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
            HoldExpiresAtUtc = holdExpires,
            Status = BookingStatus.PendingPayment,
            RequiresOwnerApproval = command.RequiresOwnerApproval,
            TotalAmount = command.TotalAmount,
            PaidAmount = 0,
            PromotionCode = command.PromotionCode,
            QrToken = qrToken
        };

        booking.StatusLogs.Add(new BookingStatusLog
        {
            FromStatus = BookingStatus.Created,
            ToStatus = BookingStatus.PendingPayment,
            ChangedByUserId = command.UserId,
            Reason = "Tạo booking thành công, bắt đầu giữ chỗ 15 phút"
        });

        await repository.AddAsync(booking, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

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

/// <summary>US-038: Khách hủy booking theo Cancellation Window 1 giờ.</summary>
public sealed class CancelBookingUseCase(
    IBookingRepository repository,
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

        var oldStatus = booking.Status;
        booking.Status = decision.ResultStatus;
        booking.CancelledAtUtc = now;
        booking.CancelReason = command.Reason ?? decision.Reason;
        booking.CancelledByUserId = command.UserId;

        booking.StatusLogs.Add(new BookingStatusLog
        {
            FromStatus = oldStatus,
            ToStatus = decision.ResultStatus,
            ChangedByUserId = command.UserId,
            Reason = command.Reason ?? decision.Reason
        });

        await repository.SaveChangesAsync(cancellationToken);
        var refund = Math.Round(booking.PaidAmount * decision.RefundPercent / 100m, 0);

        return new CancellationResultDto(booking.Code, booking.Status.ToString(), decision.RefundPercent, refund, decision.Reason);
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

        if (command.NewStartAtUtc.HasValue && command.NewEndAtUtc.HasValue)
        {
            if (command.NewEndAtUtc <= command.NewStartAtUtc)
                throw new ValidationException("Thời gian kết thúc mới phải sau thời gian bắt đầu mới.");

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
    IBookingQueries queries) : IExtendBookingUseCase
{
    public async Task<BookingDetailDto> ExecuteAsync(string code, ExtendBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetEntityByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        if (booking.Status != BookingStatus.CheckedIn && booking.Status != BookingStatus.Parking && booking.Status != BookingStatus.Confirmed)
            throw new ValidationException($"Chỉ có thể gia hạn khi đang đỗ hoặc đã xác nhận. Hiện tại: {booking.Status}");

        if (command.NewEndAtUtc <= booking.EndAtUtc)
            throw new ValidationException("Thời gian gia hạn mới phải sau thời gian kết thúc hiện tại.");

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

        if (booking.Status != BookingStatus.PendingOwnerApproval && booking.Status != BookingStatus.PendingPayment)
            throw new ValidationException($"Chỉ duyệt booking ở trạng thái chờ duyệt. Hiện tại: {booking.Status}");

        var oldStatus = booking.Status;
        booking.Status = BookingStatus.Confirmed;
        booking.ConfirmedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        booking.StatusLogs.Add(new BookingStatusLog
        {
            FromStatus = oldStatus,
            ToStatus = BookingStatus.Confirmed,
            Reason = command.Reason ?? "Chủ bãi duyệt thành công"
        });

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

        var oldStatus = booking.Status;
        booking.Status = BookingStatus.Rejected;
        booking.CancelReason = command.Reason ?? "Chủ bãi từ chối do hết chỗ";
        booking.CancelledAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        booking.StatusLogs.Add(new BookingStatusLog
        {
            FromStatus = oldStatus,
            ToStatus = BookingStatus.Rejected,
            Reason = booking.CancelReason
        });

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

        var oldStatus = booking.Status;
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        booking.CancelReason = $"Chủ bãi hủy sự cố: {command.Reason}";

        booking.StatusLogs.Add(new BookingStatusLog
        {
            FromStatus = oldStatus,
            ToStatus = BookingStatus.Cancelled,
            Reason = booking.CancelReason
        });

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

        var oldStatus = booking.Status;
        booking.Status = BookingStatus.CheckedIn;
        booking.CheckedInAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        booking.StatusLogs.Add(new BookingStatusLog
        {
            FromStatus = oldStatus,
            ToStatus = BookingStatus.CheckedIn,
            ChangedByUserId = command.StaffUserId,
            Reason = $"Cổng xác nhận check-in (Device {command.GateDeviceId})"
        });

        await repository.SaveChangesAsync(cancellationToken);
        return await queries.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }

    public async Task<BookingDetailDto> CheckOutAsync(string code, GateActionCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetEntityByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        var oldStatus = booking.Status;
        booking.Status = BookingStatus.Completed;
        booking.CheckedOutAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        booking.CompletedAtUtc = booking.CheckedOutAtUtc;

        booking.StatusLogs.Add(new BookingStatusLog
        {
            FromStatus = oldStatus,
            ToStatus = BookingStatus.Completed,
            ChangedByUserId = command.StaffUserId,
            Reason = $"Cổng xác nhận check-out hoàn tất (Device {command.GateDeviceId})"
        });

        await repository.SaveChangesAsync(cancellationToken);
        return await queries.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }

    public async Task<BookingDetailDto> NoShowAsync(string code, GateActionCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetEntityByCodeAsync(code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Booking", code);

        var oldStatus = booking.Status;
        booking.Status = BookingStatus.NoShow;
        booking.CancelledAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        booking.CancelReason = "Khách không đến sau 30 phút (No-show)";

        booking.StatusLogs.Add(new BookingStatusLog
        {
            FromStatus = oldStatus,
            ToStatus = BookingStatus.NoShow,
            ChangedByUserId = command.StaffUserId,
            Reason = booking.CancelReason
        });

        await repository.SaveChangesAsync(cancellationToken);
        return await queries.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Booking", code);
    }
}
