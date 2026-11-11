using GateService.Application.Features.GateEvents;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Application.Features.GateConsole;

// ===== US-039: PORT NO-SHOW GỌI BOOKINGSERVICE (TV3) =====
// Tra cứu booking / đồng bộ check-in/out dùng chung IGateBookingClient (GateBookingContracts.cs);
// port riêng này chỉ thêm 2 thao tác No-show để không ép các client/fake hiện có phải cài thêm.

/// <summary>US-039 (UC-24): booking quá 30 phút ân hạn check-in – ứng viên No-show hiển thị cho Staff.</summary>
public sealed record GateNoShowCandidate(string Code, int UserId, string PlateNumber, string ParkingLotName,
    string? SlotCode, DateTime StartAtUtc, DateTime EndAtUtc, int MinutesOverdue);

/// <summary>
/// Port No-show với BookingService qua HTTP (US-039).
/// 409 → ConflictException kèm lý do của BookingService; lỗi mạng → DependencyUnavailableException.
/// </summary>
public interface IGateNoShowClient
{
    Task<IReadOnlyList<GateNoShowCandidate>> ListNoShowCandidatesAsync(int parkingLotId, int graceMinutes, CancellationToken cancellationToken);
    /// <summary>US-039: đánh dấu booking No-show (BookingService tự kiểm tra trạng thái + mốc 30 phút).</summary>
    Task MarkNoShowAsync(string bookingCode, int staffUserId, CancellationToken cancellationToken);
}

// ===== PORT GỌI PARKINGSERVICE (TV5) – GHI ĐÈ TRẠNG THÁI SLOT (US-049) / NHẢ CHỖ (US-039) =====
public sealed record SlotStateOverrideResult(int SlotId, string SlotCode, string FromState, string ToState,
    string Source, int? ChangedByUserId, DateTime ChangedAtUtc);

public interface IParkingSlotClient
{
    /// <summary>Ghi đè trạng thái slot (nguồn: StaffOverride/Gate/Sensor/Pms) – ParkingService tự ghi SlotStateLogs.</summary>
    Task<SlotStateOverrideResult> OverrideSlotStateAsync(int parkingLotId, int slotId, SlotState newState,
        SlotStateSource source, int? staffUserId, string reason, CancellationToken cancellationToken);
}

// ===== PORT CA TRỰC =====
public interface IShiftStore
{
    Task<Shift?> FindOpenByStaffAsync(int staffUserId, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

// ===== US-039 (UC-24): XEM ỨNG VIÊN NO-SHOW =====
public interface IListNoShowCandidatesUseCase
{
    Task<IReadOnlyList<GateNoShowCandidate>> ExecuteAsync(int parkingLotId, int graceMinutes = 30, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-039 bước 1: hệ thống gửi alert No-show cho Staff – lấy danh sách booking Confirmed chưa check-in
/// đã quá mốc ân hạn 30 phút (NO_SHOW_CANCEL_AFTER_MINUTES) kể từ giờ bắt đầu của bãi Staff phụ trách.
/// </summary>
public sealed class ListNoShowCandidatesUseCase(IGateNoShowClient noShowClient, GateConsoleOptions? options = null) : IListNoShowCandidatesUseCase
{
    private readonly GateConsoleOptions _options = options ?? new GateConsoleOptions();

    public Task<IReadOnlyList<GateNoShowCandidate>> ExecuteAsync(int parkingLotId, int graceMinutes = 30, CancellationToken cancellationToken = default)
    {
        if (parkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (graceMinutes <= 0) graceMinutes = _options.NoShowGraceMinutes;
        return noShowClient.ListNoShowCandidatesAsync(parkingLotId, graceMinutes, cancellationToken);
    }
}

// ===== US-039 (UC-24): STAFF HỦY BOOKING NO-SHOW =====
public sealed record CancelNoShowCommand(int ParkingLotId, int StaffUserId, string? Reason = null);

public sealed record NoShowCancelResultDto(string BookingCode, string BookingStatus, DateTime CancelledAtUtc,
    int MinutesOverdue, string? SlotCode, bool SlotReleased, bool ShiftFlagged, string AuditNote);

public interface ICancelNoShowUseCase
{
    Task<NoShowCancelResultDto> ExecuteAsync(string bookingCode, CancelNoShowCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-039 / UC-24: Staff hủy booking No-show sau 30 phút quá hạn check-in. Theo checklist:
/// 1) Kiểm tra booking vượt quá 30 phút ân hạn check-in (UC-24 E1: trước mốc → từ chối ở Backend).
/// 2) Kiểm tra trạng thái booking (chỉ Confirmed) và bản ghi check-in (chưa có lượt gửi xe nào của booking).
/// 3) Chuyển booking sang NoShow (BookingService idempotent; tiền cọc giữ lại theo chính sách).
/// 4) Nhả chỗ đỗ đã giữ (slot về Available qua ParkingService).
/// 5) Ghi GateEvents (NoShowCancel) + tăng NoShowCancelCount ca trực; vượt ngưỡng → gắn cờ ca (US-077, UC-24 E2).
/// UC-24 A1 (khách báo đang đến) = Staff không gọi API này, chỗ được giữ nguyên.
/// </summary>
public sealed class CancelNoShowUseCase(
    IGateBookingClient bookingClient,
    IGateNoShowClient noShowClient,
    IParkingSlotClient slotClient,
    IParkingSessionWriter sessionWriter,
    IGateEventWriter gateEvents,
    IShiftStore shiftStore,
    TimeProvider timeProvider,
    GateConsoleOptions? options = null) : ICancelNoShowUseCase
{
    private readonly GateConsoleOptions _options = options ?? new GateConsoleOptions();

    public async Task<NoShowCancelResultDto> ExecuteAsync(string bookingCode, CancelNoShowCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (command.StaffUserId <= 0) throw new ValidationException("staffUserId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(bookingCode)) throw new ValidationException("Mã booking không được để trống.");
        var code = bookingCode.Trim().ToUpperInvariant();

        // (1) Booking phải tồn tại và thuộc bãi Staff đang trực.
        var booking = await bookingClient.FindByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Booking", bookingCode);
        if (booking.ParkingLotId != command.ParkingLotId)
            throw new ValidationException($"Booking {code} không thuộc bãi {command.ParkingLotId}.");

        // (2a) Kiểm tra trạng thái booking: chỉ Confirmed mới chuyển được sang No-show.
        if (booking.Status == BookingStatusConsts.NoShow)
            throw new ConflictException($"Booking {code} đã được xử lý No-show trước đó.");
        if (booking.Status != BookingStatusConsts.Confirmed)
            throw new ConflictException($"Booking {code} ở trạng thái {booking.Status}, chỉ có thể hủy No-show với booking ở trạng thái Confirmed.");

        // (2b) Kiểm tra mốc 30 phút ân hạn (UC-24 E1 – nút Hủy bị disable ở Backend trước mốc).
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var overdueMinutes = (int)(now - booking.StartAtUtc).TotalMinutes;
        if (overdueMinutes < _options.NoShowGraceMinutes)
            throw new ValidationException(
                $"Chưa quá {_options.NoShowGraceMinutes} phút từ giờ bắt đầu (mới quá {Math.Max(0, overdueMinutes)} phút). " +
                "Vui lòng chờ hết mốc ân hạn hoặc liên hệ khách qua kênh in-app.");

        // (2c) Kiểm tra bản ghi check-in: booking đã có lượt gửi xe tại cổng thì không được hủy No-show.
        var session = await sessionWriter.FindByBookingCodeAsync(code, cancellationToken);
        if (session is not null)
            throw new ConflictException(
                $"Booking {code} đã có lượt check-in tại cổng (phiên {session.Code}), không thể hủy No-show. Hãy xử lý theo luồng check-out.");

        return await FinalizeNoShowAsync(code, booking, session, command, overdueMinutes, cancellationToken);
    }

    private async Task<NoShowCancelResultDto> FinalizeNoShowAsync(string code, GateBookingSnapshot booking, ParkingSession? session,
        CancelNoShowCommand command, int overdueMinutes, CancellationToken cancellationToken)
    {
        // (3) Chuyển booking sang NoShow – BookingService tự chặn trạng thái/mốc; 409 do đã NoShow (retry) → bỏ qua.
        var alreadyMarked = false;
        try
        {
            await noShowClient.MarkNoShowAsync(code, command.StaffUserId, cancellationToken);
        }
        catch (ConflictException)
        {
            var latest = await bookingClient.FindByCodeAsync(code, cancellationToken)
                ?? throw new NotFoundException("Booking", code);
            if (latest.Status != BookingStatusConsts.NoShow) throw;
            alreadyMarked = true;
        }

        // (4) Nhả chỗ đỗ đã giữ (release the reserved parking slot).
        var slotCode = booking.SlotCode;
        var slotReleased = false;
        if (booking.SlotId is { } slotId)
        {
            var releaseReason = alreadyMarked
                ? $"No-show (retry): nhả chỗ cho booking {code}"
                : $"US-039: No-show quá {overdueMinutes} phút – nhả chỗ cho booking {code}";
            var slotResult = await slotClient.OverrideSlotStateAsync(command.ParkingLotId, slotId, SlotState.Available,
                SlotStateSource.Gate, command.StaffUserId, releaseReason, cancellationToken);
            slotCode = slotResult.SlotCode;
            slotReleased = true;
        }

        // (5) Audit: mọi thao tác tay tại cổng phải ghi GateEvents (US-077 cũng lấy dữ liệu hủy từ đây).
        var reasonText = string.IsNullOrWhiteSpace(command.Reason) ? "Khách không đến sau 30 phút" : command.Reason.Trim();
        var auditNote = $"Hủy No-show booking {code} (quá hạn {overdueMinutes} phút). Lý do: {reasonText}" +
                        (slotReleased ? ". Đã nhả chỗ đỗ." : ".");
        await gateEvents.WriteAsync(new GateEventDraft(command.ParkingLotId, GateEventType.NoShowCancel,
            session?.Id, booking.PlateNumber, command.StaffUserId, auditNote), cancellationToken);

        // (6) Ca trực: tăng bộ đếm; vượt ngưỡng → gắn cờ ca cho Chủ bãi/Admin xem xét.
        var shiftFlagged = false;
        var shift = await shiftStore.FindOpenByStaffAsync(command.StaffUserId, cancellationToken);
        if (shift is not null)
        {
            shift.NoShowCancelCount++;
            if (shift.NoShowCancelCount >= _options.AbnormalNoShowCancelThreshold)
            {
                shift.Status = ShiftStatus.Flagged;
                shiftFlagged = true;
            }
            await shiftStore.SaveAsync(cancellationToken);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return new NoShowCancelResultDto(code, BookingStatusConsts.NoShow, now, Math.Max(0, overdueMinutes),
            slotCode, slotReleased, shiftFlagged, auditNote);
    }
}

/// <summary>Hằng chuỗi trạng thái booking dùng so sánh với Status trả về từ BookingService (JSON string enum).</summary>
public static class BookingStatusConsts
{
    public const string Confirmed = nameof(BookingStatus.Confirmed);
    public const string NoShow = nameof(BookingStatus.NoShow);
    public const string CheckedIn = nameof(BookingStatus.CheckedIn);
    public const string Completed = nameof(BookingStatus.Completed);
}