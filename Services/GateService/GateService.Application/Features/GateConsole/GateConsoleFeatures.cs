using GateService.Application.Features.GateEvents;
using GateService.Application.Features.PlateRecognition;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Application.Features.GateConsole;

// ===== COMMAND =====
/// <summary>
/// Check-in tại cổng vào (T-702). Hỗ trợ đủ flow MVP: Qr / BookingCode / nhập tay (Manual) / khách vãng lai.
/// Vãng lai = không kèm BookingCode. Ocr/ANPR để dành cho Phase 2.
/// </summary>
public sealed record CheckInCommand(int ParkingLotId, GateMethod Method, string Plate, string? BookingCode = null, int? StaffUserId = null);

/// <summary>Check-out tại cổng ra (T-702). MVP: nhân viên nhập/xác nhận biển số tại console.</summary>
public sealed record CheckOutCommand(int ParkingLotId, string Plate, GateMethod Method = GateMethod.Manual, int? StaffUserId = null);

// ===== PORT (ghi) =====
/// <summary>Port ghi lượt gửi xe – Infrastructure cài bằng GateDbContext, Application không đụng EF Core.</summary>
public interface IParkingSessionWriter
{
    /// <summary>Lưu lượt mới (chống trùng bằng unique index "1 lượt Active/biển/bãi").</summary>
    Task<ParkingSession> AddAsync(ParkingSession session, CancellationToken cancellationToken);
    /// <summary>Tìm lượt đang đỗ theo biển số ĐÃ CHUẨN HOÁ, entity có tracking để cập nhật lúc check-out.</summary>
    Task<ParkingSession?> FindActiveTrackedAsync(int parkingLotId, string normalizedPlate, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

// ===== USE CASE =====
public interface ICheckInUseCase
{
    Task<ParkingSessionDto> ExecuteAsync(CheckInCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// T-702: Check-in cổng vào. Biển số LUÔN đi qua IPlateRecognizer để Phase 2 thay OCR
/// mà không phải sửa use case này. Phí, slot, grace period... thuộc task khác – KHÔNG làm ở đây.
/// </summary>
public sealed class CheckInUseCase(IPlateRecognizer plateRecognizer, IParkingSessionWriter writer,
    IGateEventWriter gateEvents, TimeProvider timeProvider) : ICheckInUseCase
{
    public async Task<ParkingSessionDto> ExecuteAsync(CheckInCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (command.Method is not (GateMethod.Qr or GateMethod.BookingCode or GateMethod.Manual))
            throw new ValidationException("Check-in chỉ hỗ trợ Qr, BookingCode hoặc Manual (Ocr/ANPR sẽ có ở Phase 2).");
        var bookingCode = command.BookingCode?.Trim().ToUpperInvariant();
        if (command.Method is GateMethod.Qr or GateMethod.BookingCode && string.IsNullOrEmpty(bookingCode))
            throw new ValidationException("Check-in bằng QR/Booking Code phải kèm mã booking.");

        // Nhận diện biển số luôn qua abstraction – MVP: ManualPlateRecognizer.
        var recognized = await plateRecognizer.RecognizeAsync(new PlateRecognitionRequest(command.Plate), cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (await writer.FindActiveTrackedAsync(command.ParkingLotId, recognized.PlateNumber, cancellationToken) is not null)
            throw new ConflictException($"Xe {recognized.PlateDisplay} đang có lượt gửi chưa kết thúc tại bãi này.");

        var session = new ParkingSession
        {
            Code = $"PS-{now:yyMMddHHmmssfff}{Random.Shared.Next(1000, 9999)}",
            ParkingLotId = command.ParkingLotId,
            BookingCode = bookingCode,
            PlateNumber = recognized.PlateNumber,
            IsWalkIn = bookingCode is null,
            EntryAtUtc = now,
            CheckInMethod = command.Method,
            CheckedInByStaffId = command.StaffUserId,
            Status = ParkingSessionStatus.Active,
        };
        await writer.AddAsync(session, cancellationToken);

        // Mọi thao tác tay tại cổng phải được ghi GateEvents.
        await gateEvents.WriteAsync(new GateEventDraft(command.ParkingLotId, GateEventType.CheckIn, session.Id,
            recognized.PlateNumber, command.StaffUserId,
            bookingCode is null ? "Khách vãng lai – nhân viên nhập biển số." : $"Check-in theo booking {bookingCode}."),
            cancellationToken);

        return ParkingSessionMapper.ToDto(session, now);
    }
}

public interface ICheckOutUseCase
{
    Task<ParkingSessionDto> ExecuteAsync(CheckOutCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// T-702: Check-out cổng ra – tìm lượt đang đỗ, đóng lượt và ghi GateEvents.
/// Tính phí thuộc PaymentService → Fee để null, không tính ở đây.
/// </summary>
public sealed class CheckOutUseCase(IPlateRecognizer plateRecognizer, IParkingSessionWriter writer,
    IGateEventWriter gateEvents, TimeProvider timeProvider) : ICheckOutUseCase
{
    public async Task<ParkingSessionDto> ExecuteAsync(CheckOutCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (command.Method is not (GateMethod.Qr or GateMethod.Manual))
            throw new ValidationException("Check-out chỉ hỗ trợ Qr hoặc Manual (Ocr/ANPR sẽ có ở Phase 2).");

        var recognized = await plateRecognizer.RecognizeAsync(new PlateRecognitionRequest(command.Plate), cancellationToken);
        var session = await writer.FindActiveTrackedAsync(command.ParkingLotId, recognized.PlateNumber, cancellationToken)
            ?? throw new NotFoundException("Lượt gửi xe đang đỗ", recognized.PlateDisplay);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        session.ExitAtUtc = now;
        session.CheckOutMethod = command.Method;
        session.CheckedOutByStaffId = command.StaffUserId;
        session.Status = ParkingSessionStatus.Completed;
        await writer.SaveAsync(cancellationToken);

        await gateEvents.WriteAsync(new GateEventDraft(command.ParkingLotId, GateEventType.CheckOut, session.Id,
            recognized.PlateNumber, command.StaffUserId), cancellationToken);

        return ParkingSessionMapper.ToDto(session, now);
    }
}

/// <summary>Map entity → DTO dùng chung cho Check-in/Check-out (định dạng biển số chạy trong bộ nhớ).</summary>
public static class ParkingSessionMapper
{
    public static ParkingSessionDto ToDto(ParkingSession s, DateTime nowUtc) => new(
        s.Id, s.Code, s.ParkingLotId, s.PlateNumber, PlateNormalizer.Format(s.PlateNumber), s.IsWalkIn,
        s.BookingCode, s.SlotCode, s.Status.ToString(), s.EntryAtUtc, s.ExitAtUtc,
        (int)Math.Ceiling(((s.ExitAtUtc ?? nowUtc) - s.EntryAtUtc).TotalMinutes),
        s.CheckInMethod.ToString(), s.EntryOcrConfidence, s.Fee);
}
