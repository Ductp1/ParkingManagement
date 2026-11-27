using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Domain.Entities;
using SupportService.Domain.Rules;

namespace SupportService.Application.Services;

/// <summary>
/// US-090: Chủ bãi phán quyết tranh chấp &amp; override hoàn tiền – chỉ chủ bãi xử lý, không ai khác can thiệp.
/// AC1 hồ sơ đầy đủ (hội thoại + log hệ thống, lịch sử booking, log check-in/out, bằng chứng) ·
/// AC2 chuyển chỗ / ép hoàn tiền / bác khiếu nại (+ phạt xe đậu sai chỗ theo mức phạt của bãi) → sự kiện cho
/// Booking/Payment/Notification, lưu tiền lệ · AC3 SLA phản hồi đầu 24 giờ, kết luận 3–5 ngày làm việc ·
/// AC4 mỗi tranh chấp 1 phán quyết, ghi audit log, không sửa / xóa được.
/// </summary>
public sealed class DisputeResolutionService(IComplaintRepository repository, IBookingHistoryLookup bookings, IGateSessionLookup gate,
    IOwnerLotLookup ownerLots, ILotFineRepository fines, TimeProvider clock) : IDisputeResolutionService
{
    public const int QueueSize = 200;
    public const int PrecedentPageSize = 50;
    private const string SourceService = "SupportService";

    public async Task<IReadOnlyList<DisputeQueueItemDto>> ListQueueAsync(int ownerProfileId, ComplaintStatus? status, bool slaBreachedOnly,
        CancellationToken cancellationToken = default)
    {
        RequireOwner(ownerProfileId);
        if (status is ComplaintStatus.Resolved or ComplaintStatus.Rejected)
            throw new ValidationException("Hàng chờ chỉ gồm tranh chấp chưa đóng; tra cứu phán quyết đã có ở /disputes/precedents.");
        var lots = await ownerLots.GetLotIdsAsync(ownerProfileId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var open = await repository.ListOpenForOwnerAsync(ownerProfileId, lots, status, QueueSize, cancellationToken);

        return open
            .Select(c => new DisputeQueueItemDto(c.Code, c.Status.ToString(), c.Category.ToString(), c.Priority.ToString(), c.ParkingLotId,
                c.BookingCode, c.RequestedAmount, c.CreatedAtUtc, Sla(c, now)))
            .Where(x => !slaBreachedOnly || x.Sla.FirstResponseBreached || x.Sla.ResolutionBreached)
            // Quá hạn lên đầu, sau đó hạn kết luận gần nhất trước.
            .OrderByDescending(x => x.Sla.FirstResponseBreached || x.Sla.ResolutionBreached)
            .ThenBy(x => x.Sla.ResolutionDueAtUtc)
            .ToList();
    }

    public async Task<DisputeCaseFileDto> GetCaseFileAsync(int ownerProfileId, string code, CancellationToken cancellationToken = default)
    {
        RequireOwner(ownerProfileId);
        var complaintCode = RequireCode(code);
        var complaint = await LoadOwnedAsync(ownerProfileId, complaintCode, forUpdate: false, cancellationToken);

        // Bằng chứng từ service khác: nguồn nào lỗi thì ghi tên vào UnavailableSources, vẫn trả phần còn lại.
        var unavailable = new List<string>();
        BookingEvidenceDto? booking = null;
        IReadOnlyList<GateSessionEvidenceDto> sessions = [];
        if (complaint.BookingCode is { } bookingCode)
        {
            try { booking = await bookings.GetAsync(bookingCode, cancellationToken); }
            catch (DependencyUnavailableException) { unavailable.Add("BookingService"); }
            try { sessions = await gate.ListForBookingAsync(complaint.ParkingLotId, bookingCode, cancellationToken); }
            catch (DependencyUnavailableException) { unavailable.Add("GateService"); }
        }
        return ToCaseFile(complaint, booking, sessions, unavailable, clock.GetUtcNow().UtcDateTime);
    }

    public async Task<DisputeCaseFileDto> AddMessageAsync(int ownerProfileId, int ownerUserId, string code, DisputeMessageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        RequireOwner(ownerProfileId, ownerUserId);
        var complaintCode = RequireCode(code);
        var message = RequireText(request.Message, DisputeRules.MinMessageLength, DisputeRules.MaxMessageLength, "Nội dung tin nhắn");
        var attachments = EvidenceUrls.Normalize(request.AttachmentUrls, DisputeRules.MaxAttachments);

        var complaint = await LoadOwnedAsync(ownerProfileId, complaintCode, forUpdate: true, cancellationToken);
        if (!ComplaintRules.IsOpen(complaint.Status)) throw new ConflictException("Tranh chấp đã có kết luận.");

        complaint.Messages.Add(new ComplaintMessage
        {
            SenderParty = ComplaintParty.LotOwner,
            SenderUserId = ownerUserId,
            Message = message,
            AttachmentUrlsJson = EvidenceUrls.ToJson(attachments),
        });
        complaint.RecordFirstResponse(clock.GetUtcNow().UtcDateTime);   // AC3
        await repository.UpdateAsync(complaint, [], cancellationToken);

        return await GetCaseFileAsync(ownerProfileId, complaintCode, cancellationToken);
    }

    public async Task<DisputeCaseFileDto> RuleAsync(int ownerProfileId, int ownerUserId, string code, DisputeRulingRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra đầu vào (không tốn truy vấn).
        RequireOwner(ownerProfileId, ownerUserId);
        var complaintCode = RequireCode(code);
        if (!Enum.IsDefined(request.Decision)) throw new ValidationException("Loại phán quyết không hợp lệ.");
        var reason = RequireText(request.Reason, DisputeRules.MinReasonLength, DisputeRules.MaxReasonLength, "Lý do phán quyết");
        var newSlot = string.IsNullOrWhiteSpace(request.NewSlotCode) ? null : request.NewSlotCode.Trim().ToUpperInvariant();
        ValidateDecisionData(request, newSlot);
        var violatorPlate = string.IsNullOrWhiteSpace(request.ViolatorPlate) ? null
            : PlateNormalizer.TryCorrect(request.ViolatorPlate) ?? throw new ValidationException($"Biển số xe vi phạm không hợp lệ: {request.ViolatorPlate}");

        // 2. Ticket phải thuộc bãi của chủ bãi, là tranh chấp về bãi, còn mở và chưa có phán quyết.
        var complaint = await LoadOwnedAsync(ownerProfileId, complaintCode, forUpdate: true, cancellationToken);
        EnsureCanRule(complaint);
        if ((request.Decision == DisputeDecision.MoveToSlot || violatorPlate is not null) && complaint.Category != ComplaintCategory.SlotOccupied)
            throw new ValidationException("Chuyển chỗ và phạt xe đậu sai chỗ chỉ áp dụng cho khiếu nại \"chỗ đã đặt bị chiếm\" (SlotOccupied).");

        // 3. Đối chiếu booking thật (BookingService): tiền đã trả, chỗ đã đặt, biển số của tài xế.
        BookingEvidenceDto? booking = null;
        if (request.Decision != DisputeDecision.RejectComplaint || violatorPlate is not null)
        {
            if (complaint.BookingCode is not { } bookingCode)
                throw new ValidationException("Khiếu nại không gắn booking nên không chuyển chỗ / hoàn tiền / lập biên bản được.");
            booking = await bookings.GetAsync(bookingCode, cancellationToken)
                      ?? throw new ConflictException($"Không tìm thấy booking {bookingCode}.");
        }
        if (request.Decision == DisputeDecision.ForceRefund && request.RefundAmount > booking!.PaidAmount)
            throw new ValidationException($"Số tiền hoàn tối đa là {Vnd(booking.PaidAmount)} (số tài xế đã trả).");
        if (newSlot is not null && string.Equals(newSlot, booking!.SlotCode, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException($"Chỗ mới phải khác chỗ đã đặt ({booking.SlotCode}).");

        // 4. Phạt xe đậu sai chỗ theo mức phạt chủ bãi đã cấu hình cho bãi này.
        var now = clock.GetUtcNow().UtcDateTime;
        ParkingViolation? violation = null;
        if (violatorPlate is not null)
        {
            if (PlateNormalizer.Normalize(booking!.PlateNumber) == violatorPlate)
                throw new ValidationException("Biển số xe vi phạm trùng với xe của tài xế đã đặt chỗ.");
            var rate = await fines.FindRateAsync(complaint.ParkingLotId, cancellationToken)
                       ?? throw new ConflictException("Bãi chưa cấu hình mức phạt đậu sai chỗ (PUT /api/v1/complaints/fine-rates/{parkingLotId}).");
            violation = new ParkingViolation
            {
                // Trùng mã (rất hiếm) → unique index → 23505 → 409, chủ bãi gửi lại.
                Code = $"VI-{now:yyyyMMdd}-{Random.Shared.Next(100000, 1000000)}",
                ParkingLotId = complaint.ParkingLotId,
                OwnerProfileId = complaint.OwnerProfileId,
                PlateNumber = violatorPlate,
                SlotCode = booking.SlotCode,
                FineAmount = rate.WrongSlotFine,
                IssuedByUserId = ownerUserId,
            };
            complaint.Violations.Add(violation);
        }

        // 5. Áp phán quyết: đóng ticket, giải phóng khoản giữ, gắn phán quyết (không sửa được), nhắn cho tài xế.
        var before = new { Status = complaint.Status.ToString(), complaint.PayoutHeld };
        var ruling = complaint.ApplyOwnerRuling(ownerUserId, request.Decision, request.RefundAmount, newSlot, reason, now);
        complaint.Messages.Add(new ComplaintMessage
        {
            SenderParty = ComplaintParty.LotOwner,
            SenderUserId = ownerUserId,
            Message = $"Phán quyết của chủ bãi: {Describe(ruling)}" +
                      (violation is null ? "" : $"; xe {violation.PlateNumber} đậu sai chỗ bị phạt {Vnd(violation.FineAmount)}") +
                      $". Lý do: {reason}",
        });

        // 6. Thực thi + thông báo qua Outbox (cùng transaction với phán quyết) + audit log (AdminService lưu, không sửa được).
        var after = new
        {
            Status = complaint.Status.ToString(), complaint.PayoutHeld,
            Decision = ruling.Decision.ToString(), ruling.RefundAmount, ruling.NewSlotCode, ViolationFine = violation?.FineAmount,
        };
        var events = new List<IntegrationEvent>
        {
            new DisputeRuled(complaint.Id, complaint.Code, ruling.Decision.ToString(), complaint.UserId, complaint.ParkingLotId,
                complaint.OwnerProfileId, complaint.BookingId ?? booking?.Id, complaint.BookingCode, ruling.RefundAmount, booking?.SlotCode,
                ruling.NewSlotCode, ownerUserId),
            new AuditRecorded(SourceService, ownerUserId,
                ruling.Decision == DisputeDecision.ForceRefund ? "DisputeRefundOverride" : "DisputeRuled",
                nameof(Complaint), complaint.Id.ToString(), JsonSerializer.Serialize(before), JsonSerializer.Serialize(after), reason),
        };
        if (violation is not null)
            events.Add(new ParkingViolationFined(violation.Code, complaint.Id, violation.ParkingLotId, violation.OwnerProfileId,
                violation.PlateNumber, violation.SlotCode, violation.FineAmount));
        await repository.UpdateAsync(complaint, events, cancellationToken);

        return await GetCaseFileAsync(ownerProfileId, complaintCode, cancellationToken);
    }

    public async Task<IReadOnlyList<DisputePrecedentDto>> ListPrecedentsAsync(int ownerProfileId, ComplaintCategory? category,
        CancellationToken cancellationToken = default)
    {
        RequireOwner(ownerProfileId);
        if (category is { } c && !Enum.IsDefined(c)) throw new ValidationException("Loại khiếu nại không hợp lệ.");
        var lots = await ownerLots.GetLotIdsAsync(ownerProfileId, cancellationToken);
        var rulings = await repository.ListRulingsAsync(lots, category, PrecedentPageSize, cancellationToken);
        return rulings.Select(r => new DisputePrecedentDto(r.Complaint.Code, r.Category.ToString(), r.Decision.ToString(),
            r.RefundAmount, r.Reason, r.CreatedAtUtc)).ToList();
    }

    // ===== Quy tắc / ánh xạ =====

    /// <summary>AC3: SLA tính từ lúc tài xế gửi khiếu nại.</summary>
    public static DisputeSlaDto Sla(Complaint c, DateTime nowUtc)
    {
        var start = c.CreatedAtUtc;
        var firstDue = start.AddHours(DisputeRules.FirstResponseHours);
        var resolutionDue = DisputeRules.AddBusinessDays(start, DisputeRules.ResolutionDeadlineBusinessDays);
        return new DisputeSlaDto(start, firstDue, c.FirstRespondedAtUtc,
            DisputeRules.IsBreached(firstDue, c.FirstRespondedAtUtc, nowUtc),
            DisputeRules.AddBusinessDays(start, DisputeRules.ResolutionTargetBusinessDays), resolutionDue, c.ResolvedAtUtc,
            DisputeRules.IsBreached(resolutionDue, c.ResolvedAtUtc, nowUtc));
    }

    /// <summary>
    /// Ticket về bãi của chủ bãi: khớp chủ bãi đã lưu, hoặc chưa lưu chủ bãi nhưng bãi nằm trong danh sách bãi của chủ bãi
    /// (ParkingService). Ticket của bãi khác → 404 (không lộ dữ liệu).
    /// </summary>
    private async Task<Complaint> LoadOwnedAsync(int ownerProfileId, string code, bool forUpdate, CancellationToken cancellationToken)
    {
        var complaint = (forUpdate
                            ? await repository.FindByCodeAsync(code, cancellationToken)
                            : await repository.GetCaseFileAsync(code, cancellationToken))
                        ?? throw new NotFoundException("Complaint", code);
        if (complaint.OwnerProfileId == ownerProfileId) return complaint;

        // BookingService chưa trả chủ bãi nên nhiều ticket có OwnerProfileId = null → đối chiếu theo bãi.
        if (complaint.OwnerProfileId is null
            && (await ownerLots.GetLotIdsAsync(ownerProfileId, cancellationToken)).Contains(complaint.ParkingLotId))
        {
            if (forUpdate) complaint.OwnerProfileId = ownerProfileId;   // bổ sung chủ bãi cho ticket cũ
            return complaint;
        }
        throw new NotFoundException("Complaint", code);
    }

    private static void ValidateDecisionData(DisputeRulingRequestDto request, string? newSlot)
    {
        var error = request.Decision switch
        {
            DisputeDecision.ForceRefund when request.RefundAmount is not > 0 || newSlot is not null
                => "Ép hoàn tiền cần refundAmount > 0 và không kèm newSlotCode.",
            DisputeDecision.MoveToSlot when newSlot is null || request.RefundAmount is not null
                => "Chuyển chỗ cần newSlotCode và không kèm refundAmount.",
            DisputeDecision.MoveToSlot when newSlot!.Length > SlotOccupiedRules.MaxSlotCodeLength
                => $"Mã chỗ tối đa {SlotOccupiedRules.MaxSlotCodeLength} ký tự.",
            DisputeDecision.RejectComplaint when request.RefundAmount is not null || newSlot is not null
                => "Bác khiếu nại không kèm số tiền hoàn hay chỗ mới.",
            _ => null,
        };
        if (error is not null) throw new ValidationException(error);
    }

    private static void EnsureCanRule(Complaint complaint)
    {
        if (DisputeRules.CanOwnerRule(complaint.Status, complaint.Category, complaint.Ruling is not null)) return;
        throw new ConflictException(complaint.Ruling is not null || !ComplaintRules.IsOpen(complaint.Status)
            ? "Tranh chấp đã có phán quyết; mỗi tranh chấp chỉ phán quyết 1 lần."
            : "Khiếu nại lỗi ứng dụng / khác không phải tranh chấp với bãi, do bộ phận hỗ trợ xử lý.");
    }

    private static string Describe(DisputeRuling r) => r.Decision switch
    {
        DisputeDecision.ForceRefund => $"hoàn {Vnd(r.RefundAmount ?? 0)} cho tài xế",
        DisputeDecision.MoveToSlot => $"chuyển tài xế sang chỗ {r.NewSlotCode}, giữ nguyên giá đã trả",
        _ => "bác khiếu nại",
    };

    /// <summary>Số tiền kiểu Việt Nam (10.000đ), không phụ thuộc culture của máy chủ.</summary>
    internal static string Vnd(decimal amount) => amount.ToString("#,0", VndFormat) + "đ";

    private static readonly System.Globalization.NumberFormatInfo VndFormat = new() { NumberGroupSeparator = "." };

    internal static void RequireOwner(int ownerProfileId)
    {
        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
    }

    internal static void RequireOwner(int ownerProfileId, int ownerUserId)
    {
        RequireOwner(ownerProfileId);
        if (ownerUserId <= 0) throw new ValidationException("userId phải là số nguyên dương.");
    }

    private static string RequireCode(string code)
        => string.IsNullOrWhiteSpace(code) ? throw new ValidationException("Mã khiếu nại không được để trống.") : code.Trim();

    private static string RequireText(string? value, int min, int max, string field)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length < min || text.Length > max)
            throw new ValidationException($"{field} phải dài từ {min} đến {max} ký tự.");
        return text;
    }

    private static DisputeCaseFileDto ToCaseFile(Complaint c, BookingEvidenceDto? booking, IReadOnlyList<GateSessionEvidenceDto> sessions,
        IReadOnlyList<string> unavailable, DateTime nowUtc) => new(
        c.Id, c.Code, c.Status.ToString(), c.Category.ToString(), c.Priority.ToString(), c.UserId, c.ParkingLotId, c.OwnerProfileId,
        c.BookingCode, c.Description, ComplaintAppService.ReadUrls(c.EvidenceUrlsJson), c.RequestedAmount, c.OwnerResponse,
        c.OwnerResponseDueAtUtc, c.OwnerRespondedAtUtc, c.PayoutHeld, DisputeRules.CanOwnerRule(c.Status, c.Category, c.Ruling is not null),
        Sla(c, nowUtc),
        c.Messages.Where(m => !m.IsInternal).OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id)
            .Select(m => new DisputeMessageDto(m.Id, m.SenderParty.ToString(), m.SenderUserId, m.Message,
                ComplaintAppService.ReadUrls(m.AttachmentUrlsJson), m.CreatedAtUtc)).ToList(),
        c.Ruling is { } r
            ? new DisputeRulingDto(r.Decision.ToString(), r.Outcome.ToString(), r.RefundAmount, r.NewSlotCode, r.Reason, r.DecidedByUserId, r.CreatedAtUtc)
            : null,
        c.Violations.OrderBy(v => v.CreatedAtUtc)
            .Select(v => new ParkingViolationDto(v.Code, c.Code, v.ParkingLotId, v.PlateNumber, v.SlotCode, v.FineAmount, v.CreatedAtUtc))
            .ToList(),
        booking, sessions, unavailable, c.CreatedAtUtc);
}

/// <summary>US-090: chủ bãi tự đặt mức phạt đậu sai chỗ cho bãi của mình và xem biên bản vi phạm.</summary>
public sealed class LotFineService(ILotFineRepository repository, IOwnerLotLookup ownerLots) : ILotFineService
{
    public const int ViolationPageSize = 200;

    public async Task<LotFineRateDto> GetRateAsync(int ownerProfileId, int parkingLotId, CancellationToken cancellationToken = default)
    {
        DisputeResolutionService.RequireOwner(ownerProfileId);
        await EnsureOwnLotAsync(ownerProfileId, parkingLotId, cancellationToken);
        var rate = await repository.FindRateAsync(parkingLotId, cancellationToken) ?? throw new NotFoundException("LotFineRate", parkingLotId);
        return ToDto(rate);
    }

    public async Task<LotFineRateDto> SetRateAsync(int ownerProfileId, int ownerUserId, int parkingLotId, LotFineRateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        DisputeResolutionService.RequireOwner(ownerProfileId, ownerUserId);
        if (request.WrongSlotFine is <= 0 or > SlotOccupiedRules.MaxWrongSlotFine)
            throw new ValidationException($"Mức phạt phải lớn hơn 0 và không quá {DisputeResolutionService.Vnd(SlotOccupiedRules.MaxWrongSlotFine)}.");
        await EnsureOwnLotAsync(ownerProfileId, parkingLotId, cancellationToken);

        var rate = await repository.FindRateAsync(parkingLotId, cancellationToken) ?? new LotFineRate { ParkingLotId = parkingLotId };
        rate.OwnerProfileId = ownerProfileId;
        rate.WrongSlotFine = request.WrongSlotFine;
        rate.UpdatedByUserId = ownerUserId;
        await repository.SaveRateAsync(rate, cancellationToken);
        return ToDto(rate);
    }

    public async Task<IReadOnlyList<ParkingViolationDto>> ListViolationsAsync(int ownerProfileId, int? parkingLotId,
        CancellationToken cancellationToken = default)
    {
        DisputeResolutionService.RequireOwner(ownerProfileId);
        var owned = await ownerLots.GetLotIdsAsync(ownerProfileId, cancellationToken);
        if (parkingLotId is { } requested && !owned.Contains(requested)) throw new NotFoundException("ParkingLot", requested);
        var rows = await repository.ListViolationsAsync(parkingLotId is { } lot ? [lot] : owned, ViolationPageSize, cancellationToken);
        return rows.Select(v => new ParkingViolationDto(v.Code, v.Complaint.Code, v.ParkingLotId, v.PlateNumber, v.SlotCode,
            v.FineAmount, v.CreatedAtUtc)).ToList();
    }

    /// <summary>Chủ bãi chỉ thao tác trên bãi của mình (bãi khác → 404).</summary>
    private async Task EnsureOwnLotAsync(int ownerProfileId, int parkingLotId, CancellationToken cancellationToken)
    {
        if (parkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (!(await ownerLots.GetLotIdsAsync(ownerProfileId, cancellationToken)).Contains(parkingLotId))
            throw new NotFoundException("ParkingLot", parkingLotId);
    }

    private static LotFineRateDto ToDto(LotFineRate r) => new(r.ParkingLotId, r.OwnerProfileId, r.WrongSlotFine, r.UpdatedAtUtc ?? r.CreatedAtUtc);
}
