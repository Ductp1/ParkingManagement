using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Domain.Entities;
using SupportService.Domain.Rules;

namespace SupportService.Application.Services;

/// <summary>
/// US-091 – Khách đến nơi nhưng bãi hết chỗ.
///   AC1 ghi nhận sự cố ưu tiên cao + gợi ý bãi thay thế còn chỗ trong 1 km (đọc ParkingService);
///   AC2 tài xế đồng ý → phát LotFullTransferAccepted để BookingService chuyển booking miễn phí;
///   AC3 tài xế không đồng ý → phát LotFullRefundRequested (hoàn 100% + voucher) và LotSlaViolated.
/// SupportService không ghi vào dữ liệu của service khác – booking/payment do service sở hữu xử lý khi nhận sự kiện.
/// </summary>
public sealed class LotFullIncidentService(
    ILotFullIncidentRepository incidents,
    IBookingLookup bookings,
    IParkingLotSearch lots,
    TimeProvider timeProvider) : ILotFullIncidentService
{
    public const int AdminListSize = 200;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<LotFullIncidentDto> ReportAsync(int userId, ReportLotFullRequestDto request, CancellationToken cancellationToken = default)
    {
        if (userId <= 0) throw new ValidationException("Không xác định được tài xế từ token.");
        var code = request.BookingCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) throw new ValidationException("Vui lòng nhập mã booking.");
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note?.Length > LotFullRules.MaxNoteLength)
            throw new ValidationException($"Mô tả tối đa {LotFullRules.MaxNoteLength} ký tự.");

        var booking = await RequireOwnConfirmedBookingAsync(userId, code, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!LotFullRules.IsWithinReportWindow(booking.StartAtUtc, booking.EndAtUtc, now))
            throw new ValidationException(
                $"Chỉ báo sự cố khi đã đến bãi: từ {LotFullRules.ReportFromMinutesBeforeStart} phút trước giờ bắt đầu đến hết khung giờ đã đặt.");
        if (await incidents.ExistsForBookingAsync(booking.Id, cancellationToken))
            throw new ConflictException($"Booking {booking.Code} đã được báo sự cố bãi hết chỗ.");

        // AC2: gợi ý bãi thay thế còn chỗ, đang mở, trong 1 km quanh bãi đã đặt.
        var lot = await lots.GetLotAsync(booking.ParkingLotId, cancellationToken);
        var alternatives = lot is null
            ? []
            : (await lots.SearchAvailableAsync(lot.Latitude, lot.Longitude, LotFullRules.AlternativeRadiusKm, cancellationToken))
                .Where(a => a.ParkingLotId != booking.ParkingLotId && a.AvailableSlots > 0 && a.IsOpenNow
                    && a.DistanceKm <= LotFullRules.AlternativeRadiusKm)
                .OrderBy(a => a.DistanceKm)
                .Take(LotFullRules.MaxAlternatives)
                .ToList();

        var incident = new LotFullIncident
        {
            Code = $"LF-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            UserId = userId,
            BookingId = booking.Id,
            BookingCode = booking.Code,
            ParkingLotId = booking.ParkingLotId,
            OwnerProfileId = booking.OwnerProfileId,
            PaidAmount = booking.PaidAmount,
            Priority = ComplaintPriority.High,   // AC1: luôn ưu tiên cao
            Note = note,
            LotAvailableSlotsAtReport = lot?.AvailableSlots,
            AlternativesJson = JsonSerializer.Serialize(alternatives, Json),
        };
        await incidents.AddAsync(incident,
            [new LotFullIncidentReported(incident.Code, booking.Code, userId, booking.ParkingLotId, booking.OwnerProfileId, alternatives.Count)],
            cancellationToken);
        return ToDto(incident);
    }

    public async Task<LotFullIncidentDto> GetAsync(int userId, string code, CancellationToken cancellationToken = default)
        => ToDto(await FindOwnedAsync(userId, code, cancellationToken));

    public async Task<LotFullIncidentDto> AcceptTransferAsync(int userId, string code, AcceptTransferRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var incident = await FindOpenAsync(userId, code, cancellationToken);
        var lot = Alternatives(incident).FirstOrDefault(a => a.ParkingLotId == request.ParkingLotId)
            ?? throw new ValidationException("Bãi được chọn không nằm trong danh sách bãi thay thế đã gợi ý.");
        // Booking phải vẫn còn Confirmed (tài xế có thể đã tự hủy trong lúc chờ).
        await RequireOwnConfirmedBookingAsync(userId, incident.BookingCode, cancellationToken);

        incident.AcceptTransfer(lot.ParkingLotId, lot.Name, timeProvider.GetUtcNow().UtcDateTime);
        await incidents.UpdateAsync(incident,
            [new LotFullTransferAccepted(incident.Code, incident.BookingId, incident.BookingCode, incident.UserId,
                incident.ParkingLotId, lot.ParkingLotId, lot.Name)],
            cancellationToken);
        return ToDto(incident);
    }

    public async Task<LotFullIncidentDto> DeclineAsync(int userId, string code, CancellationToken cancellationToken = default)
    {
        var incident = await FindOpenAsync(userId, code, cancellationToken);
        await RequireOwnConfirmedBookingAsync(userId, incident.BookingCode, cancellationToken);

        incident.RequestRefund(LotFullRules.RefundPercent, LotFullRules.CompensationVoucherAmount,
            LotFullRules.CompensationVoucherValidDays, timeProvider.GetUtcNow().UtcDateTime);
        await incidents.UpdateAsync(incident,
            [
                new LotFullRefundRequested(incident.Code, incident.BookingId, incident.BookingCode, incident.UserId, incident.OwnerProfileId,
                    LotFullRules.RefundPercent, LotFullRules.CompensationVoucherAmount, LotFullRules.CompensationVoucherValidDays),
                new LotSlaViolated(incident.Code, incident.BookingCode, incident.ParkingLotId, incident.OwnerProfileId),
            ],
            cancellationToken);
        return ToDto(incident);
    }

    public async Task<IReadOnlyList<LotFullIncidentDto>> ListForAdminAsync(int? parkingLotId, string? status, bool slaViolationOnly,
        CancellationToken cancellationToken = default)
    {
        LotFullIncidentStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<LotFullIncidentStatus>(status, ignoreCase: true, out var s) || !Enum.IsDefined(s))
                throw new ValidationException("Trạng thái chỉ nhận Reported, TransferAccepted hoặc RefundRequested.");
            parsed = s;
        }
        var list = await incidents.ListAsync(parkingLotId, parsed, slaViolationOnly, AdminListSize, cancellationToken);
        return list.Select(ToDto).ToList();
    }

    /// <summary>Booking của chính tài xế và còn Confirmed (chưa vào bãi). Booking của người khác → 404, không tiết lộ tồn tại.</summary>
    private async Task<BookingSnapshot> RequireOwnConfirmedBookingAsync(int userId, string bookingCode, CancellationToken cancellationToken)
    {
        var booking = await bookings.FindByCodeAsync(bookingCode, cancellationToken);
        if (booking is null || booking.UserId != userId) throw new NotFoundException("Booking", bookingCode);
        if (booking.Status != BookingStatus.Confirmed)
            throw new ValidationException($"Chỉ xử lý bãi hết chỗ cho booking đã xác nhận và chưa vào bãi (hiện tại: {booking.Status}).");
        return booking;
    }

    private async Task<LotFullIncident> FindOwnedAsync(int userId, string code, CancellationToken cancellationToken)
    {
        var normalized = code?.Trim().ToUpperInvariant() ?? string.Empty;
        var incident = await incidents.FindByCodeAsync(normalized, cancellationToken);
        if (incident is null || incident.UserId != userId) throw new NotFoundException("Sự cố", normalized);
        return incident;
    }

    private async Task<LotFullIncident> FindOpenAsync(int userId, string code, CancellationToken cancellationToken)
    {
        var incident = await FindOwnedAsync(userId, code, cancellationToken);
        if (incident.Status != LotFullIncidentStatus.Reported)
            throw new ConflictException($"Sự cố {incident.Code} đã được xử lý ({incident.Status}).");
        return incident;
    }

    private static IReadOnlyList<AlternativeLotDto> Alternatives(LotFullIncident incident)
        => JsonSerializer.Deserialize<List<AlternativeLotDto>>(incident.AlternativesJson, Json) ?? [];

    public static LotFullIncidentDto ToDto(LotFullIncident i) => new(
        i.Code, i.Status.ToString(), i.Priority.ToString(), i.BookingCode, i.ParkingLotId, i.PaidAmount, i.Note, i.LotAvailableSlotsAtReport,
        Alternatives(i), i.NewParkingLotId, i.NewParkingLotName, i.RefundPercent, i.VoucherAmount, i.VoucherValidDays,
        i.IsSlaViolation, i.CreatedAtUtc, i.DecidedAtUtc);
}
