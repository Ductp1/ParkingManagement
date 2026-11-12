using GateService.Application.Features.GateConsole;
using GateService.Application.Features.GateEvents;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Application.Features.GateExceptions;

/// <summary>US-048 (UC-23): hành động thủ công Staff chọn khi xử lý ngoại lệ tại cổng.</summary>
public enum GateExceptionAction
{
    /// <summary>Chỉ ghi nhận ngoại lệ (không thực thi hành động nào khác).</summary>
    RecordOnly = 0,
    /// <summary>Xác nhận cho xe vào (check-in thủ công – QR/biển số lỗi).</summary>
    ManualCheckIn = 1,
    /// <summary>Xác nhận cho xe ra (check-out thủ công).</summary>
    ManualCheckOut = 2,
    /// <summary>Xác nhận đã thu tiền mặt tại cổng (thanh toán lỗi, không chặn xe – không bỏ qua thanh toán).</summary>
    ConfirmCashPayment = 3,
    /// <summary>Mở barie khẩn cấp (hỏng barie, sơ tán...).</summary>
    EmergencyBarrierOpen = 4
}

/// <summary>US-048 (UC-23): loại ngoại lệ tại cổng – map 1-1 sang GateEventType để tái sử dụng bảng audit.</summary>
public static class GateExceptionTypes
{
    public static readonly GateEventType[] Allowed =
    [
        GateEventType.OcrFailed,
        GateEventType.ManualPlateCorrection,
        GateEventType.PaymentFailed,
        GateEventType.EmergencyBarrierOpen,
        GateEventType.VehicleArrived
    ];

    public static bool IsAllowed(GateEventType type) => Allowed.Contains(type);
}

/// <summary>
/// US-048 (UC-23): lệnh xử lý ngoại lệ thủ công tại cổng. Reason BẮT BUỘC (UC-23 bước 4) –
/// mọi thao tác thủ công đều ghi audit log (staffId, thời điểm, lý do, ảnh) làm bằng chứng tranh chấp.
/// </summary>
public sealed record GateExceptionCommand(
    int ParkingLotId,
    GateExceptionAction Action,
    GateEventType ExceptionType,
    string Reason,
    int StaffUserId,
    string? Plate = null,
    string? BookingCode = null,
    int? ParkingSessionId = null,
    string? ImagePath = null);

public sealed record GateExceptionResultDto(int EventId, int ParkingLotId, string Action, string ExceptionType,
    string? BookingCode, string? PlateDisplay, string? SessionCode, string Status, string AuditNote, DateTime AtUtc);

public interface IRecordGateExceptionUseCase
{
    Task<GateExceptionResultDto> ExecuteAsync(GateExceptionCommand command, CancellationToken cancellationToken = default);
}

/// <summary>US-048: bộ lọc nhật ký ngoại lệ tại cổng cho Chủ bãi/Admin đối soát.</summary>
public sealed record GateExceptionLogQuery(int ParkingLotId, GateEventType? ExceptionType = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null, int Limit = 100);

public interface IListGateExceptionsUseCase
{
    Task<IReadOnlyList<GateEventDto>> ExecuteAsync(GateExceptionLogQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-048: đọc audit log các thao tác thủ công tại cổng (ai làm, lúc nào, lý do gì) từ GateEvents.
/// Không truyền loại → trả mọi loại ngoại lệ hợp lệ (GateExceptionTypes.Allowed), bỏ qua check-in/out thường.
/// </summary>
public sealed class ListGateExceptionsUseCase(IGateEventQueries queries) : IListGateExceptionsUseCase
{
    public async Task<IReadOnlyList<GateEventDto>> ExecuteAsync(GateExceptionLogQuery query, CancellationToken cancellationToken = default)
    {
        if (query.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (query.FromUtc is { } from && query.ToUtc is { } to && to < from)
            throw new ValidationException("Khoảng thời gian không hợp lệ (toUtc phải sau fromUtc).");
        if (query.ExceptionType is { } type && !GateExceptionTypes.IsAllowed(type))
            throw new ValidationException(
                $"Loại ngoại lệ {type} không hợp lệ. Chỉ nhận: {string.Join(", ", GateExceptionTypes.Allowed)}.");

        IReadOnlyCollection<GateEventType> types = query.ExceptionType is { } only ? [only] : GateExceptionTypes.Allowed;
        return await queries.ListAsync(query.ParkingLotId, types, query.FromUtc, query.ToUtc,
            Math.Clamp(query.Limit, 1, 500), cancellationToken);
    }
}

/// <summary>
/// US-048 / UC-23: Xử lý ngoại lệ tại cổng (extend từ UC-21/UC-22 khi không xác minh tự động được).
///  - Support recording exception types and reasons: GateEventType + Reason bắt buộc + ảnh (tuỳ chọn).
///  - Record staff actions and audit logs: thực thi hành động qua use case chuẩn (check-in/out) và
///    luôn ghi GateEvents kèm staffId + ca trực; tăng ManualExceptionCount của ca.
///  - Business rule: thao tác thủ công KHÔNG được bỏ qua thanh toán – ConfirmCashPayment chỉ ghi nhận
///    bằng chứng đã thu tiền mặt để PaymentService đối soát sau.
/// UC-23 E1: xe không có booking hợp lệ và hết vùng đệm vãng lai → luồng check-in/capacity tự chặn.
/// </summary>
public sealed class RecordGateExceptionUseCase(
    ICheckInUseCase checkIn,
    ICheckOutUseCase checkOut,
    IParkingSessionWriter sessionWriter,
    IGateEventWriter gateEvents,
    IShiftStore shiftStore,
    TimeProvider timeProvider) : IRecordGateExceptionUseCase
{
    public async Task<GateExceptionResultDto> ExecuteAsync(GateExceptionCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (command.StaffUserId <= 0) throw new ValidationException("staffUserId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ValidationException("Bắt buộc nhập lý do xử lý ngoại lệ (UC-23 bước 4).");
        if (!GateExceptionTypes.IsAllowed(command.ExceptionType))
            throw new ValidationException(
                $"Loại ngoại lệ {command.ExceptionType} không hợp lệ. Chỉ nhận: {string.Join(", ", GateExceptionTypes.Allowed)}.");

        var plate = NormalizePlate(command.Plate);
        var bookingCode = string.IsNullOrWhiteSpace(command.BookingCode) ? null : command.BookingCode.Trim().ToUpperInvariant();

        // Ca trực đang mở của Staff (nếu có) – gắn sự kiện ngoại lệ vào ca để chốt ca/giám sát.
        var shift = await shiftStore.FindOpenByStaffAsync(command.StaffUserId, cancellationToken);

        // Thực thi hành động qua đúng use case chuẩn để không bỏ sót nghiệp vụ (tạo phiên, chuẩn hoá biển, audit riêng).
        var actionDetail = command.Action switch
        {
            GateExceptionAction.ManualCheckIn => await DoManualCheckInAsync(command, plate, bookingCode, cancellationToken),
            GateExceptionAction.ManualCheckOut => await DoManualCheckOutAsync(command, plate, cancellationToken),
            GateExceptionAction.ConfirmCashPayment => await FindSessionDetailAsync(command.ParkingLotId, plate, cancellationToken,
                " – Xác nhận đã thu tiền mặt tại cổng, chờ PaymentService đối soát."),
            _ => await FindSessionDetailAsync(command.ParkingLotId, plate, cancellationToken, string.Empty)
        };

        // Audit log: staffId, thời điểm, lý do, ảnh – dữ liệu thủ công là nguồn tạm, dùng khi tranh chấp.
        var note = $"[{command.Action}] {command.ExceptionType}: {command.Reason.Trim()}{actionDetail.Detail}";
        var record = await gateEvents.WriteAsync(new GateEventDraft(
            command.ParkingLotId, command.ExceptionType, actionDetail.SessionId, plate, command.StaffUserId, note,
            command.ImagePath, shift?.Id), cancellationToken);

        if (shift is not null)
        {
            shift.ManualExceptionCount++;
            await shiftStore.SaveAsync(cancellationToken);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return new GateExceptionResultDto(record.Id, command.ParkingLotId, command.Action.ToString(),
            command.ExceptionType.ToString(), bookingCode,
            plate is null ? null : PlateNormalizer.Format(plate),
            actionDetail.SessionCode, actionDetail.SessionStatus ?? "Recorded", note, now);
    }

    private async Task<(int? SessionId, string? SessionCode, string? SessionStatus, string Detail)> DoManualCheckInAsync(
        GateExceptionCommand command, string? plate, string? bookingCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plate))
            throw new ValidationException("Xử lý check-in thủ công yêu cầu biển số xe.");
        var dto = await checkIn.ExecuteAsync(
            new CheckInCommand(command.ParkingLotId, GateMethod.Manual, plate, bookingCode, command.StaffUserId),
            cancellationToken);
        return (dto.Id, dto.Code, dto.Status, $" – Đã tạo phiên check-in {dto.Code}.");
    }

    private async Task<(int? SessionId, string? SessionCode, string? SessionStatus, string Detail)> DoManualCheckOutAsync(
        GateExceptionCommand command, string? plate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plate))
            throw new ValidationException("Xử lý check-out thủ công yêu cầu biển số xe.");
        // ConfirmFee = true: check-out thủ công vẫn chốt phí như luồng chuẩn (không được bỏ qua thanh toán).
        var result = await checkOut.ExecuteAsync(
            new CheckOutCommand(command.ParkingLotId, plate, GateMethod.Manual, command.StaffUserId, ConfirmFee: true),
            cancellationToken);
        var session = result.Session;
        var feeText = result.Fee is null ? string.Empty : $" Phí: {session.Fee:N0}đ.";
        return (session.Id, session.Code, session.Status, $" – Đã chốt phiên check-out {session.Code}.{feeText}");
    }

    private async Task<(int? SessionId, string? SessionCode, string? SessionStatus, string Detail)> FindSessionDetailAsync(
        int parkingLotId, string? plate, CancellationToken cancellationToken, string detail)
    {
        if (string.IsNullOrWhiteSpace(plate)) return (null, null, null, detail);
        var normalized = NormalizePlate(plate)!;
        var session = await sessionWriter.FindActiveTrackedAsync(parkingLotId, normalized, cancellationToken);
        return (session?.Id, session?.Code, session?.Status.ToString(), detail);
    }

    private static string? NormalizePlate(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate)) return null;
        return PlateNormalizer.TryCorrect(plate)
            ?? throw new ValidationException($"Không đọc được biển số hợp lệ từ '{plate}'. Staff kiểm tra và nhập tay.");
    }
}