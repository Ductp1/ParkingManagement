using System.Text.Json;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Domain.Entities;
using SupportService.Domain.Rules;

namespace SupportService.Application.Services;

/// <summary>
/// US-088: Gửi khiếu nại / ticket hỗ trợ.
/// AC1 tạo ticket có mã + trạng thái · AC2 chỉ trong 7 ngày · AC3 phân mức Low/Medium/High · AC4 theo dõi trạng thái.
/// </summary>
public sealed class ComplaintAppService(IComplaintRepository repository, IBookingLookup bookings, TimeProvider clock) : IComplaintService
{
    public async Task<ComplaintDetailDto> CreateAsync(CreateComplaintRequestDto request, CancellationToken cancellationToken = default)
    {
        var description = request.Description?.Trim() ?? string.Empty;
        var evidence = EvidenceUrls.Normalize(request.EvidenceUrls, ComplaintRules.MaxEvidenceFiles);
        Validate(request, description);

        // 1. Booking phải tồn tại và thuộc về người gửi.
        var booking = await bookings.FindByCodeAsync(request.BookingCode.Trim(), cancellationToken);
        if (booking is null || booking.UserId != request.UserId)
            throw new NotFoundException("Booking", request.BookingCode.Trim());

        if (!ComplaintRules.CanComplainAbout(booking.Status))
            throw new ValidationException("Booking chưa thanh toán nên chưa thể gửi khiếu nại.");

        // 2. Sự cố phải đã xảy ra: booking đang diễn ra thì phải bắt đầu rồi; mốc sự cố không được nằm ở tương lai.
        var now = clock.GetUtcNow().UtcDateTime;
        if (ComplaintRules.IsOngoing(booking.Status) && booking.StartAtUtc > now)
            throw new ValidationException("Booking chưa diễn ra nên chưa thể gửi khiếu nại.");

        var incidentAt = ComplaintRules.ResolveIncidentAtUtc(booking.Status, booking.EndAtUtc, booking.LastChangedAtUtc, now);
        if (incidentAt > now)
            throw new ValidationException("Thời điểm sự cố không hợp lệ (nằm ở tương lai).");

        // 3. AC2: chỉ trong 7 ngày làm việc kể từ mốc sự cố.
        if (!ReviewRules.IsWithinComplaintWindow(incidentAt, now))
            throw new ValidationException(
                $"Đã quá {ReviewRules.ComplaintWindowWorkingDays} ngày làm việc kể từ khi booking kết thúc hoặc có quyết định, không thể gửi khiếu nại mới.");

        if (request.RequestedAmount > booking.PaidAmount)
            throw new ValidationException($"Số tiền yêu cầu không được vượt quá số tiền đã thanh toán ({booking.PaidAmount:N0}đ).");

        // 4. Mỗi booking chỉ có 1 ticket đang xử lý.
        if (await repository.HasOpenComplaintForBookingAsync(booking.Id, cancellationToken))
            throw new ConflictException($"Booking {booking.Code} đã có khiếu nại đang được xử lý.");

        // 5. AC3: phân mức và giao nhóm xử lý; khiếu nại về bãi thì chờ chủ bãi phản hồi 48 giờ.
        var (priority, team) = ComplaintRules.Classify(request.Category);
        var awaitOwner = ComplaintRules.RequiresOwnerResponse(request.Category) && booking.OwnerProfileId is not null;
        var evidenceJson = EvidenceUrls.ToJson(evidence);

        var complaint = new Complaint
        {
            Code = $"CP-{now:yyyyMMdd}-{Random.Shared.Next(1000, 10000)}",
            UserId = request.UserId,
            BookingId = booking.Id,
            BookingCode = booking.Code,
            ParkingLotId = booking.ParkingLotId,
            OwnerProfileId = booking.OwnerProfileId,
            Category = request.Category,
            Priority = priority,
            AssignedTeam = team,
            Description = description,
            EvidenceUrlsJson = evidenceJson,
            RequestedAmount = request.RequestedAmount,
            Status = awaitOwner ? ComplaintStatus.AwaitingOwner : ComplaintStatus.Open,
            OwnerResponseDueAtUtc = awaitOwner ? now.AddHours(ReviewRules.OwnerResponseHours) : null,
            // US-089 AC3: giữ tiền quyết toán của chủ bãi cho tới khi có phán quyết.
            PayoutHeld = ComplaintRules.ShouldHoldPayout(request.Category, request.RequestedAmount),
        };
        complaint.Messages.Add(new ComplaintMessage
        {
            SenderParty = ComplaintParty.Driver,
            SenderUserId = request.UserId,
            Message = description,
            AttachmentUrlsJson = evidenceJson,
        });

        await repository.AddAsync(complaint, cancellationToken);
        return ToDetail(complaint);
    }

    public async Task<ComplaintDetailDto> GetForUserAsync(string code, int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0) throw new ValidationException("userId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(code)) throw new ValidationException("Mã khiếu nại không được để trống.");
        return await repository.GetForUserAsync(code.Trim(), userId, cancellationToken)
               ?? throw new NotFoundException("Complaint", code.Trim());
    }

    public Task<IReadOnlyList<ComplaintSummaryDto>> ListForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0) throw new ValidationException("userId phải là số nguyên dương.");
        return repository.ListForUserAsync(userId, cancellationToken);
    }

    private static void Validate(CreateComplaintRequestDto request, string description)
    {
        if (request.UserId <= 0) throw new ValidationException("userId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(request.BookingCode)) throw new ValidationException("Mã booking không được để trống.");
        if (!Enum.IsDefined(request.Category)) throw new ValidationException("Loại khiếu nại không hợp lệ.");
        if (description.Length is < ComplaintRules.MinDescriptionLength or > ComplaintRules.MaxDescriptionLength)
            throw new ValidationException($"Mô tả phải dài từ {ComplaintRules.MinDescriptionLength} đến {ComplaintRules.MaxDescriptionLength} ký tự.");
        if (request.RequestedAmount < 0) throw new ValidationException("Số tiền yêu cầu không được âm.");
    }

    private static ComplaintDetailDto ToDetail(Complaint c) => new(
        c.Id, c.Code, c.Status.ToString(), c.Category.ToString(), c.Priority.ToString(), c.AssignedTeam.ToString(),
        c.BookingCode, c.ParkingLotId, c.Description, ReadUrls(c.EvidenceUrlsJson), c.RequestedAmount,
        c.OwnerResponseDueAtUtc, c.OwnerResponse, c.Resolution, c.ResolvedRefundAmount, c.CreatedAtUtc, c.UpdatedAtUtc,
        c.Messages.Where(m => !m.IsInternal)
            .Select(m => new ComplaintMessageDto(m.Id, m.SenderParty.ToString(), m.Message, ReadUrls(m.AttachmentUrlsJson), m.CreatedAtUtc))
            .ToList(),
        c.OwnerRespondedAtUtc, c.EscalatedAtUtc, c.PayoutHeld);

    /// <summary>Đọc mảng URL đã lưu dạng JSON; chuỗi rỗng/hỏng → danh sách rỗng.</summary>
    public static IReadOnlyList<string> ReadUrls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
