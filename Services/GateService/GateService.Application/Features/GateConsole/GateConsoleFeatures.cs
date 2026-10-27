using GateService.Application.Features.GateEvents;
using GateService.Application.Features.PlateRecognition;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Application.Features.GateConsole;

// ===== COMMAND =====
/// <summary>
/// Check-in tại cổng vào (T-702 + T-704). Hỗ trợ đủ flow MVP: Qr / BookingCode / nhập tay (Manual) / khách vãng lai.
/// Vãng lai = không kèm BookingCode. Qr = phải kèm QrToken do BookingService cấp (xác thực bằng
/// IBookingQrTokenValidator, mã booking lấy từ payload đã xác thực). Ocr/ANPR để dành cho Phase 2.
/// </summary>
public sealed record CheckInCommand(int ParkingLotId, GateMethod Method, string Plate, string? BookingCode = null,
    int? StaffUserId = null, string? QrToken = null);

/// <summary>
/// Check-out tại cổng ra (T-702 + UC-22). MVP: nhân viên nhập/xác nhận biển số tại console.
/// ConfirmFee = true → chạy đủ luồng chốt phí (exit-quote nội bộ): tính phí PaymentService,
/// ghi Fee/OverstayFee vào lượt, thu tiền phụ thu (Payment idempotent) và đồng bộ booking Completed.
/// </summary>
public sealed record CheckOutCommand(int ParkingLotId, string Plate, GateMethod Method = GateMethod.Manual,
    int? StaffUserId = null, bool ConfirmFee = false);

/// <summary>Kết quả check-out: lượt xe đã đóng + báo giá phí (nếu ConfirmFee) + cảnh báo đối soát (nếu có).</summary>
public sealed record CheckOutResultDto(ParkingSessionDto Session, ExitFeeQuoteDto? Fee, string? Warning);

// ===== PORT (ghi) =====
/// <summary>Port ghi lượt gửi xe – Infrastructure cài bằng GateDbContext, Application không đụng EF Core.</summary>
public interface IParkingSessionWriter
{
    /// <summary>Lưu lượt mới (chống trùng bằng unique index "1 lượt Active/biển/bãi" và "1 booking/1 lượt").</summary>
    Task<ParkingSession> AddAsync(ParkingSession session, CancellationToken cancellationToken);
    /// <summary>Tìm lượt đang đỗ theo biển số ĐÃ CHUẨN HOÁ, entity có tracking để cập nhật lúc check-out.</summary>
    Task<ParkingSession?> FindActiveTrackedAsync(int parkingLotId, string normalizedPlate, CancellationToken cancellationToken);
    /// <summary>T-704: tìm lượt đã tồn tại của 1 booking (MỌI trạng thái) – 1 booking chỉ được check-in đúng 1 lần.</summary>
    Task<ParkingSession?> FindByBookingCodeAsync(string bookingCode, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

// ===== USE CASE =====
public interface ICheckInUseCase
{
    Task<ParkingSessionDto> ExecuteAsync(CheckInCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// T-702: Check-in cổng vào. Biển số LUÔN đi qua IPlateRecognizer để Phase 2 thay OCR
/// mà không phải sửa use case này. Phí và slot thuộc task khác – KHÔNG làm ở đây.
/// T-704: Method=Qr bắt buộc QR token hợp lệ của BookingService (đúng chữ ký, còn hạn, đúng bãi);
/// token hỏng/hết hạn/sai bãi bị từ chối; 1 booking chỉ check-in được 1 lần; biển số thực tế khác
/// biển trên QR thì vẫn cho vào nhưng GHI audit note vào GateEvents để đối soát.
/// UC-21/US-044: booking phải ở trạng thái Confirmed (BookingService là nguồn chân lý);
/// đến trễ ≤ CheckInGracePeriodMinutes (15') → vào bình thường, phí vẫn tính từ giờ đã đặt;
/// trễ hơn → vẫn cho vào khi staff xác nhận nhưng ghi cảnh báo vào audit (hủy no-show là UC-24 sau 30').
/// Sau khi tạo lượt, đồng bộ booking → CheckedIn qua BookingService (idempotent cho phép retry).
/// </summary>
public sealed class CheckInUseCase(IPlateRecognizer plateRecognizer, IBookingQrTokenValidator qrTokens,
    IGateBookingClient bookings, IParkingSessionWriter writer, IGateEventWriter gateEvents,
    TimeProvider timeProvider, GateConsoleOptions options) : ICheckInUseCase
{
    public async Task<ParkingSessionDto> ExecuteAsync(CheckInCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (command.Method is not (GateMethod.Qr or GateMethod.BookingCode or GateMethod.Manual))
            throw new ValidationException("Check-in chỉ hỗ trợ Qr, BookingCode hoặc Manual (Ocr/ANPR sẽ có ở Phase 2).");

        // Xác định mã booking theo method: Qr lấy từ payload đã xác thực của token, BookingCode lấy từ request.
        string? bookingCode;
        string? bookingPlate = null;
        if (command.Method == GateMethod.Qr)
        {
            if (string.IsNullOrWhiteSpace(command.QrToken))
                throw new ValidationException("Check-in bằng QR phải kèm mã token QR.");
            if (!qrTokens.TryValidate(command.QrToken, command.ParkingLotId, out var claims))
                throw new ValidationException("Mã QR không hợp lệ: token sai định dạng, chữ ký không khớp, đã hết hạn hoặc không thuộc bãi xe này.");
            bookingCode = claims!.BookingCode.Trim().ToUpperInvariant();
            bookingPlate = claims.PlateNumber;
        }
        else
        {
            bookingCode = command.BookingCode?.Trim().ToUpperInvariant();
            if (command.Method == GateMethod.BookingCode && string.IsNullOrEmpty(bookingCode))
                throw new ValidationException("Check-in bằng QR/Booking Code phải kèm mã booking.");
        }

        // Nhận diện biển số luôn qua abstraction – MVP: ManualPlateRecognizer.
        var recognized = await plateRecognizer.RecognizeAsync(new PlateRecognitionRequest(command.Plate), cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // UC-21: kiểm tra booking với BookingService – trạng thái phải Confirmed, không check-in booking hủy/hết hạn.
        GateBookingSnapshot? booking = null;
        var lateMinutes = 0;
        if (bookingCode is not null)
        {
            booking = await bookings.FindByCodeAsync(bookingCode, cancellationToken)
                ?? throw new NotFoundException("Booking", bookingCode);

            if (!string.Equals(booking.Status, BookingStatus.Confirmed.ToString(), StringComparison.OrdinalIgnoreCase))
                throw new ConflictException(
                    $"Booking {bookingCode} ở trạng thái {booking.Status} – chỉ booking Confirmed mới được check-in (UC-21).");

            // Đã có lượt của booking này? → có thể là retry sau khi đồng bộ booking gặp lỗi mạng:
            // thử sync lại và trả lượt hiện có (idempotent), tránh tạo lượt thứ hai (unique index cũng chặn).
            // (Kiểm tra TRƯỚC guard biển số đang đỗ vì lượt retry chính là lượt đang đỗ của xe này.)
            if (await writer.FindByBookingCodeAsync(bookingCode, cancellationToken) is { } existing)
            {
                await bookings.SyncCheckInAsync(bookingCode, command.StaffUserId, cancellationToken);
                return ParkingSessionMapper.ToDto(existing, now);
            }

            lateMinutes = (int)Math.Floor((now - booking.StartAtUtc).TotalMinutes);
        }
        // T-704: 1 booking chỉ sinh đúng 1 lượt gửi xe – race còn bị unique index chặn ở writer.
        if (await writer.FindActiveTrackedAsync(command.ParkingLotId, recognized.PlateNumber, cancellationToken) is not null)
            throw new ConflictException($"Xe {recognized.PlateDisplay} đang có lượt gửi chưa kết thúc tại bãi này.");

        var session = new ParkingSession
        {
            Code = $"PS-{now:yyMMddHHmmssfff}{Random.Shared.Next(1000, 9999)}",
            ParkingLotId = command.ParkingLotId,
            BookingCode = bookingCode,
            PlateNumber = recognized.PlateNumber,
            VehicleType = booking is not null && Enum.TryParse<VehicleType>(booking.VehicleType, out var bookedType)
                ? bookedType
                : VehicleType.Sedan, // vãng lai: OCR hiện chưa trả loại xe – mặc định Sedan, staff có thể sửa ở luồng ngoại lệ
            IsWalkIn = bookingCode is null,
            EntryAtUtc = now,
            CheckInMethod = command.Method,
            CheckedInByStaffId = command.StaffUserId,
            Status = ParkingSessionStatus.Active,
        };
        await writer.AddAsync(session, cancellationToken);

        // Mọi thao tác tay tại cổng phải được ghi GateEvents. Biển thực tế khác biển trên QR (nhân viên
        // xác nhận cho vào) thì ghi rõ vào note làm bằng chứng đối soát – không chặn check-in.
        string note;
        if (bookingCode is null)
            note = "Khách vãng lai – nhân viên nhập biển số.";
        else
        {
            note = bookingPlate is not null && PlateNormalizer.Normalize(bookingPlate) != recognized.PlateNumber
                ? $"Check-in theo booking {bookingCode} – biển số thực tế {PlateNormalizer.Format(recognized.PlateNumber)} khác biển số trên QR ({PlateNormalizer.Format(bookingPlate)}). Nhân viên xác nhận cho vào, ghi nhận để đối soát."
                : $"Check-in theo booking {bookingCode}.";

            // US-044: ân hạn 15 phút – vẫn cho vào bình thường, phí vẫn tính từ giờ bắt đầu đã đặt (không bù giờ).
            if (lateMinutes > options.CheckInGracePeriodMinutes)
                note += $" Đến trễ {lateMinutes} phút – VƯỢT ân hạn {options.CheckInGracePeriodMinutes} phút, nhân viên xác nhận cho vào (theo dõi no-show UC-24).";
            else if (lateMinutes > 0)
                note += $" Đến trễ {lateMinutes} phút – trong ân hạn {options.CheckInGracePeriodMinutes} phút (US-044), tính phí từ giờ bắt đầu đã đặt.";
        }
        await gateEvents.WriteAsync(new GateEventDraft(command.ParkingLotId, GateEventType.CheckIn, session.Id,
            recognized.PlateNumber, command.StaffUserId, note),
            cancellationToken);

        // Đồng bộ booking → CheckedIn (UC-21). Lỗi hạ tầng → 503; client retry sẽ đi nhánh idempotent
        // phía trên (trả lại lượt vừa tạo thay vì tạo lượt mới).
        if (bookingCode is not null)
            await bookings.SyncCheckInAsync(bookingCode, command.StaffUserId, cancellationToken);

        return ParkingSessionMapper.ToDto(session, now);
    }
}

public interface ICheckOutUseCase
{
    Task<CheckOutResultDto> ExecuteAsync(CheckOutCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// T-702: Check-out cổng ra – tìm lượt đang đỗ, đóng lượt và ghi GateEvents.
/// ConfirmFee = false: hành vi cũ – chỉ đóng lượt, Fee để null (tính phí thuộc PaymentService).
/// ConfirmFee = true (UC-22, US-045 → 047): nhờ PaymentService chốt phí (Billing Grace 15' miễn phí,
/// quá giờ × hệ số Overstay), ghi Fee/OverstayFee vào lượt, thu tiền phụ thu qua Payment idempotent
/// (staff xác nhận số tiền hiển thị ở bước exit-quote), rồi đồng bộ booking → Completed.
/// Lỗi đồng bộ booking không chặn việc cho xe ra – ghi cảnh báo trả về cho staff đối soát.
/// </summary>
public sealed class CheckOutUseCase(IPlateRecognizer plateRecognizer, IGateBookingClient bookings,
    IExitFeeCalculator exitFees, IGateSettlementRecorder settlements, IParkingSessionWriter writer,
    IGateEventWriter gateEvents, TimeProvider timeProvider, GateConsoleOptions options) : ICheckOutUseCase
{
    public async Task<CheckOutResultDto> ExecuteAsync(CheckOutCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (command.Method is not (GateMethod.Qr or GateMethod.Manual))
            throw new ValidationException("Check-out chỉ hỗ trợ Qr hoặc Manual (Ocr/ANPR sẽ có ở Phase 2).");

        var recognized = await plateRecognizer.RecognizeAsync(new PlateRecognitionRequest(command.Plate), cancellationToken);
        var session = await writer.FindActiveTrackedAsync(command.ParkingLotId, recognized.PlateNumber, cancellationToken)
            ?? throw new NotFoundException("Lượt gửi xe đang đỗ", recognized.PlateDisplay);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // UC-22: chốt phí TRƯỚC khi đóng lượt (cần EntryAtUtc của lượt). Số tiền hiển thị ở exit-quote
        // được tính lại server-side tại đây – không tin số tiền từ client.
        ExitFeeQuoteDto? quote = null;
        var paymentNote = string.Empty;
        if (command.ConfirmFee)
        {
            quote = await ExitFeeQuoter.QuoteAsync(ParkingSessionMapper.ToDto(session, now), bookings, exitFees, options, now, cancellationToken);

            if (quote.AdditionalDue > 0)
            {
                var payment = await settlements.RecordAsync(new GateSettlementRequest(
                    session.Code, session.Id, session.ParkingLotId, session.OwnerProfileId,
                    session.BookingCode, quote.AdditionalDue, "Cash", command.StaffUserId), cancellationToken);
                paymentNote = $" Đã ghi thu tiền mặt {quote.AdditionalDue:N0}đ – Payment {payment.PaymentCode}.";
            }
        }

        session.ExitAtUtc = now;
        session.CheckOutMethod = command.Method;
        session.CheckedOutByStaffId = command.StaffUserId;
        session.Status = ParkingSessionStatus.Completed;
        if (quote is not null)
        {
            session.Fee = quote.SessionFee;
            session.OverstayFee = quote.AdditionalDue;
        }
        await writer.SaveAsync(cancellationToken);

        // Đồng bộ booking → Completed (booking đã CheckedIn khi xe vào). Lỗi hạ tầng không chặn barie –
        // trả cảnh báo để staff đối soát (dữ liệu gate là nguồn tạm, booking tự chốt bởi worker sau).
        string? warning = null;
        if (session.BookingCode is not null && command.ConfirmFee)
        {
            try
            {
                await bookings.SyncCheckOutAsync(session.BookingCode, command.StaffUserId, cancellationToken);
            }
            catch (DependencyUnavailableException)
            {
                warning = $"Không đồng bộ được booking {session.BookingCode} về Completed – cần đối soát thủ công (BookingService chưa sẵn sàng).";
            }
        }

        var note = command.ConfirmFee && quote is not null
            ? $"Check-out chốt phí: tổng {quote.SessionFee:N0}đ (đã trả trước {quote.PrepaidAmount:N0}đ, thu thêm {quote.AdditionalDue:N0}đ).{paymentNote}"
            : null;
        await gateEvents.WriteAsync(new GateEventDraft(command.ParkingLotId, GateEventType.CheckOut, session.Id,
            recognized.PlateNumber, command.StaffUserId, note), cancellationToken);

        return new CheckOutResultDto(ParkingSessionMapper.ToDto(session, now), quote, warning);
    }
}

/// <summary>Map entity → DTO dùng chung cho Check-in/Check-out (định dạng biển số chạy trong bộ nhớ).</summary>
public static class ParkingSessionMapper
{
    public static ParkingSessionDto ToDto(ParkingSession s, DateTime nowUtc) => new(
        s.Id, s.Code, s.ParkingLotId, s.PlateNumber, PlateNormalizer.Format(s.PlateNumber), s.IsWalkIn,
        s.BookingCode, s.SlotCode, s.Status.ToString(), s.EntryAtUtc, s.ExitAtUtc,
        (int)Math.Ceiling(((s.ExitAtUtc ?? nowUtc) - s.EntryAtUtc).TotalMinutes),
        s.CheckInMethod.ToString(), s.EntryOcrConfidence, s.Fee, s.VehicleType.ToString());
}
