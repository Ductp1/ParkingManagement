using GateService.Application.Features.GateConsole;
using GateService.Application.Features.GateEvents;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Application.Features.PmsIntegration;

// ===== HỢP ĐỒNG API PMS (US-052, UC-51 – Mức 1) =====
/// <summary>Sự kiện PMS đẩy về nền tảng: xe vào/ra theo booking hoặc biển số (khách vãng lai qua PMS).</summary>
public sealed record PmsEventCommand(
    Guid EventId,
    PmsEventType EventType,
    int ParkingLotId,
    string PlateNumber,
    DateTime OccurredAtUtc,
    string? BookingCode = null,
    string? GateId = null,
    string? ImageUrl = null);

/// <summary>Kết quả định danh bãi khi xác thực API key thành công.</summary>
public sealed record PmsLotIdentity(int ParkingLotId, int IntegrationId, string ProviderName);

/// <summary>Kết quả xử lý một sự kiện PMS (trả về cho PMS trong ack).</summary>
public sealed record PmsEventResultDto(Guid EventId, bool Processed, string Outcome, string? BookingCode,
    string? SessionCode, string? Message);

/// <summary>Port xác thực API key của PMS – ParkingService lưu hash, nền tảng kiểm tra qua ParkingService (US-052).</summary>
public interface IPmsAuthClient
{
    /// <summary>Trả null nếu API key không hợp lệ hoặc tích hợp đã bị vô hiệu/báo lỗi.</summary>
    Task<PmsLotIdentity?> VerifyAsync(string apiKey, CancellationToken cancellationToken);
}

/// <summary>Port chống xử lý trùng sự kiện (idempotency) – Infrastructure cài bằng TransactionalInbox + advisory lock.</summary>
public interface IPmsInbox
{
    /// <summary>true = sự kiện được xử lý trong lần gọi này; false = sự kiện trùng đã xử lý trước đó.</summary>
    Task<bool> ApplyOnceAsync(Guid eventId, Func<CancellationToken, Task> apply, CancellationToken cancellationToken);
}

/// <summary>Port lưu sự kiện PMS không khớp booking (UC-51 E1) – hàng chờ xử lý tay cho Staff.</summary>
public interface IPmsQuarantineStore
{
    Task AddPendingAsync(PmsUnmatchedEvent item, CancellationToken cancellationToken);
    Task<IReadOnlyList<PmsUnmatchedEvent>> ListPendingAsync(int? parkingLotId, CancellationToken cancellationToken);
    Task<PmsUnmatchedEvent?> FindByIdAsync(int id, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

public interface IProcessPmsEventUseCase
{
    Task<PmsEventResultDto> ExecuteAsync(string apiKey, PmsEventCommand command, CancellationToken cancellationToken = default);
}

public interface IListQuarantinedPmsEventsUseCase
{
    Task<IReadOnlyList<PmsUnmatchedEvent>> ExecuteAsync(int? parkingLotId, CancellationToken cancellationToken = default);
}

public sealed record ResolveQuarantinedPmsEventCommand(int StaffUserId, string? Note = null, bool Discard = false);

public interface IResolveQuarantinedPmsEventUseCase
{
    Task<PmsUnmatchedEvent> ExecuteAsync(int eventId, ResolveQuarantinedPmsEventCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-052 / UC-51: Nhận và xử lý sự kiện xe vào/ra từ PMS (Mức 1).
///  - Authentication: X-Api-Key xác thực qua ParkingService (LotIntegration – chỉ lưu hash).
///  - Duplicate events: TransactionalInbox + advisory lock theo EventId – event trùng bỏ qua (idempotent).
///  - Map and synchronize: tạo/đóng ParkingSession tại GateService và đồng bộ booking ở BookingService
///    (Confirmed → CheckedIn → Completed); xe không có booking xử lý như vãng lai.
///  - Integration failures: booking/event không khớp → quarantine cho Staff xử lý tay (UC-51 E1);
///    lỗi hệ thống → ném exception để PMS retry (transaction trong inbox rollback toàn bộ).
/// </summary>
public sealed class ProcessPmsEventUseCase(
    IPmsAuthClient authClient,
    IPmsInbox inbox,
    IGateBookingClient bookingClient,
    IParkingSessionWriter sessionWriter,
    IGateEventWriter gateEvents,
    IPmsQuarantineStore quarantine,
    TimeProvider timeProvider) : IProcessPmsEventUseCase
{
    public async Task<PmsEventResultDto> ExecuteAsync(string apiKey, PmsEventCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AuthenticationException("Thiếu header X-Api-Key – PMS phải gửi API key đã cấp cho bãi.");
        if (command.EventId == Guid.Empty) throw new ValidationException("eventId không được rỗng.");
        if (command.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(command.PlateNumber)) throw new ValidationException("plateNumber là bắt buộc.");

        // ===== Authentication =====
        var identity = await authClient.VerifyAsync(apiKey, cancellationToken)
            ?? throw new AuthenticationException("API key PMS không hợp lệ hoặc tích hợp đã bị vô hiệu.");
        if (identity.ParkingLotId != command.ParkingLotId)
            throw new ForbiddenException($"API key này thuộc bãi {identity.ParkingLotId}, không có quyền gửi sự kiện cho bãi {command.ParkingLotId}.");

        var plate = PlateNormalizer.TryCorrect(command.PlateNumber)
            ?? throw new ValidationException($"PMS gửi biển số không hợp lệ: '{command.PlateNumber}'.");
        var bookingCode = string.IsNullOrWhiteSpace(command.BookingCode) ? null : command.BookingCode.Trim().ToUpperInvariant();

        // ===== Duplicate events (idempotency) =====
        var processed = await inbox.ApplyOnceAsync(command.EventId, ct =>
            ApplyEventAsync(identity, command, plate, bookingCode, ct), cancellationToken);
        if (!processed)
            return new PmsEventResultDto(command.EventId, false, "Duplicate", bookingCode, null,
                "Sự kiện đã được xử lý trước đó – bỏ qua (idempotent).");

        var (outcome, sessionCode, message) = await DescribeOutcomeAsync(command.ParkingLotId, plate, cancellationToken);
        return new PmsEventResultDto(command.EventId, true, outcome, bookingCode, sessionCode, message);
    }

    private async Task ApplyEventAsync(PmsLotIdentity identity, PmsEventCommand command, string plate,
        string? bookingCode, CancellationToken cancellationToken)
    {
        switch (command.EventType)
        {
            case PmsEventType.VehicleEntry:
                await ApplyEntryAsync(identity, command, plate, bookingCode, cancellationToken);
                break;
            case PmsEventType.VehicleExit:
                await ApplyExitAsync(identity, command, plate, cancellationToken);
                break;
            default:
                throw new ValidationException($"eventType {command.EventType} không hỗ trợ (chỉ VehicleEntry/VehicleExit).");
        }
    }

    /// <summary>Báo cho controller biết sự kiện phải vào quarantine thay vì retry (UC-51 E1).</summary>
    public sealed class QuarantinedException(string message) : Exception(message);

    private async Task ApplyEntryAsync(PmsLotIdentity identity, PmsEventCommand command, string plate,
        string? bookingCode, CancellationToken cancellationToken)
    {
        // PMS gửi lại event xe vào khi phiên đã tồn tại → bỏ qua (idempotent), không tạo phiên lần hai.
        if (bookingCode is not null)
        {
            if (await sessionWriter.FindByBookingCodeAsync(bookingCode, cancellationToken) is not null) return;
        }
        else if (await sessionWriter.FindActiveTrackedAsync(command.ParkingLotId, plate, cancellationToken) is not null)
        {
            return;
        }

        GateBookingSnapshot? booking = null;
        if (bookingCode is not null)
        {
            booking = await bookingClient.FindByCodeAsync(bookingCode, cancellationToken);

            // UC-51 E1: event không khớp booking → hàng chờ xử lý tay cho Staff (không tạo phiên).
            if (booking is null)
                throw new QuarantinedException($"Không tìm thấy booking {bookingCode} trong hệ thống.");
            if (booking.ParkingLotId != command.ParkingLotId)
                throw new QuarantinedException($"Booking {bookingCode} thuộc bãi {booking.ParkingLotId}, khác bãi nhận sự kiện.");
            if (booking.Status is not (BookingStatusConsts.Confirmed or BookingStatusConsts.CheckedIn))
                throw new QuarantinedException($"Booking {bookingCode} ở trạng thái {booking.Status}, không thể check-in qua PMS.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var session = new ParkingSession
        {
            Code = $"PS-{now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
            ParkingLotId = command.ParkingLotId,
            OwnerProfileId = 0, // Không bắt buộc ở GateService – đối soát qua booking/bãi.
            BookingId = null,   // Id nội bộ của BookingService không truy ngược từ snapshot – dùng BookingCode.
            BookingCode = bookingCode,
            PlateNumber = plate,
            IsWalkIn = booking is null,
            EntryAtUtc = command.OccurredAtUtc > now ? now : command.OccurredAtUtc,
            CheckInMethod = GateMethod.Pms,
            EntryImagePath = command.ImageUrl,
            CheckedInByStaffId = null, // Tự động từ PMS, không phải Staff.
            Status = ParkingSessionStatus.Active,
        };
        await sessionWriter.AddAsync(session, cancellationToken);

        var note = bookingCode is null
            ? $"PMS ({identity.ProviderName}) báo xe vào – khách vãng lai qua PMS. Gate: {command.GateId ?? "n/a"}."
            : $"PMS ({identity.ProviderName}) báo xe vào theo booking {bookingCode}. Gate: {command.GateId ?? "n/a"}.";
        await gateEvents.WriteAsync(new GateEventDraft(command.ParkingLotId, GateEventType.CheckIn,
            session.Id, plate, null, note, command.ImageUrl), cancellationToken);

        // Đồng bộ booking (UC-51 bước 3): booking Confirmed → CheckedIn. Lỗi → exception để PMS retry.
        if (booking is not null && booking.Status == BookingStatusConsts.Confirmed)
            await bookingClient.SyncCheckInAsync(bookingCode!, null, cancellationToken);
    }

    private async Task ApplyExitAsync(PmsLotIdentity identity, PmsEventCommand command, string plate,
        CancellationToken cancellationToken)
    {
        var session = await sessionWriter.FindActiveTrackedAsync(command.ParkingLotId, plate, cancellationToken)
            ?? throw new QuarantinedException(
                $"Không tìm thấy lượt đang đỗ của biển {PlateNormalizer.Format(plate)} tại bãi {command.ParkingLotId} để chốt xe ra.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        session.ExitAtUtc = command.OccurredAtUtc > now ? now : command.OccurredAtUtc;
        session.CheckOutMethod = GateMethod.Pms;
        session.ExitImagePath = command.ImageUrl;
        session.CheckedOutByStaffId = null;
        session.Status = ParkingSessionStatus.Completed;
        await sessionWriter.SaveAsync(cancellationToken);

        var note = $"PMS ({identity.ProviderName}) báo xe ra. Gate: {command.GateId ?? "n/a"}." +
                   (session.BookingCode is null ? string.Empty : $" Booking: {session.BookingCode}.");
        await gateEvents.WriteAsync(new GateEventDraft(command.ParkingLotId, GateEventType.CheckOut,
            session.Id, plate, null, note, command.ImageUrl), cancellationToken);

        // Đồng bộ booking (UC-51 bước 3): booking CheckedIn → Completed.
        if (session.BookingCode is not null)
        {
            var booking = await bookingClient.FindByCodeAsync(session.BookingCode, cancellationToken);
            if (booking is not null && booking.Status == BookingStatusConsts.CheckedIn)
                await bookingClient.SyncCheckOutAsync(session.BookingCode, null, cancellationToken);
        }
    }

    private async Task<(string Outcome, string? SessionCode, string Message)> DescribeOutcomeAsync(
        int parkingLotId, string plate, CancellationToken cancellationToken)
    {
        var session = await sessionWriter.FindActiveTrackedAsync(parkingLotId, plate, cancellationToken);
        return session is not null
            ? ("SessionActive", session.Code, "Đã ghi nhận sự kiện PMS.")
            : ("SessionClosed", null, "Đã ghi nhận sự kiện PMS và chốt phiên đỗ.");
    }
}

/// <summary>US-052: Staff xem danh sách sự kiện PMS đưa vào hàng chờ (UC-51 E1).</summary>
public sealed class ListQuarantinedPmsEventsUseCase(IPmsQuarantineStore store) : IListQuarantinedPmsEventsUseCase
{
    public Task<IReadOnlyList<PmsUnmatchedEvent>> ExecuteAsync(int? parkingLotId, CancellationToken cancellationToken = default)
        => store.ListPendingAsync(parkingLotId, cancellationToken);
}

/// <summary>US-052: Staff xử lý tay sự kiện PMS hàng chờ – đánh dấu đã xử lý hoặc bỏ qua kèm ghi chú.</summary>
public sealed class ResolveQuarantinedPmsEventUseCase(IPmsQuarantineStore store, TimeProvider timeProvider) : IResolveQuarantinedPmsEventUseCase
{
    public async Task<PmsUnmatchedEvent> ExecuteAsync(int eventId, ResolveQuarantinedPmsEventCommand command, CancellationToken cancellationToken = default)
    {
        if (command.StaffUserId <= 0) throw new ValidationException("staffUserId phải là số nguyên dương.");
        var item = await store.FindByIdAsync(eventId, cancellationToken)
            ?? throw new NotFoundException("Sự kiện PMS hàng chờ", eventId);
        if (item.Status != PmsQuarantineStatus.Pending)
            throw new ConflictException($"Sự kiện {item.EventId} đã được xử lý ({item.Status}).");

        item.Status = command.Discard ? PmsQuarantineStatus.Discarded : PmsQuarantineStatus.Handled;
        item.HandledByUserId = command.StaffUserId;
        item.HandledAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        item.Note = command.Note;
        await store.SaveAsync(cancellationToken);
        return item;
    }
}