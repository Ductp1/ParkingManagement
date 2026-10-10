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
        var evidence = NormalizeEvidence(request.EvidenceUrls);
        Validate(request, description);

        // 1. Booking phải tồn tại và thuộc về người gửi.
        var booking = await bookings.FindByCodeAsync(request.BookingCode.Trim(), cancellationToken);
        if (booking is null || booking.UserId != request.UserId)
            throw new NotFoundException("Booking", request.BookingCode.Trim());

        if (!ComplaintRules.CanComplainAbout(booking.Status))
            throw new ValidationException("Booking chưa thanh toán nên chưa thể gửi khiếu nại.");

        // 2. AC2: chỉ trong 7 ngày làm việc kể từ khi booking kết thúc hoặc có quyết định gần nhất (hủy, check-out...).
        var now = clock.GetUtcNow().UtcDateTime;
        var incidentAt = booking.LastChangedAtUtc is { } changed && changed > booking.EndAtUtc ? changed : booking.EndAtUtc;
        if (!ReviewRules.IsWithinComplaintWindow(incidentAt, now))
            throw new ValidationException(
                $"Đã quá {ReviewRules.ComplaintWindowWorkingDays} ngày làm việc kể từ khi booking kết thúc, không thể gửi khiếu nại mới.");

        if (request.RequestedAmount > booking.PaidAmount)
            throw new ValidationException($"Số tiền yêu cầu không được vượt quá số tiền đã thanh toán ({booking.PaidAmount:N0}đ).");

        // 3. Mỗi booking chỉ có 1 ticket đang xử lý.
        if (await repository.HasOpenComplaintForBookingAsync(booking.Id, cancellationToken))
            throw new ConflictException($"Booking {booking.Code} đã có khiếu nại đang được xử lý.");

        // 4. AC3: phân mức và giao nhóm xử lý; khiếu nại về bãi thì chờ chủ bãi phản hồi 48 giờ.
        var (priority, team) = ComplaintRules.Classify(request.Category);
        var awaitOwner = ComplaintRules.RequiresOwnerResponse(request.Category) && booking.OwnerProfileId is not null;
        var evidenceJson = evidence.Count == 0 ? null : JsonSerializer.Serialize(evidence);

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

    private static List<string> NormalizeEvidence(IReadOnlyList<string>? urls)
    {
        var list = (urls ?? []).Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct().ToList();
        if (list.Count > ComplaintRules.MaxEvidenceFiles)
            throw new ValidationException($"Chỉ được đính kèm tối đa {ComplaintRules.MaxEvidenceFiles} ảnh/video.");
        foreach (var url in list)
        {
            if (url.Length > 500 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                throw new ValidationException($"Đường dẫn bằng chứng không hợp lệ: {url}");
        }
        return list;
    }

    private static ComplaintDetailDto ToDetail(Complaint c) => new(
        c.Id, c.Code, c.Status.ToString(), c.Category.ToString(), c.Priority.ToString(), c.AssignedTeam.ToString(),
        c.BookingCode, c.ParkingLotId, c.Description, ReadUrls(c.EvidenceUrlsJson), c.RequestedAmount,
        c.OwnerResponseDueAtUtc, c.OwnerResponse, c.Resolution, c.ResolvedRefundAmount, c.CreatedAtUtc, c.UpdatedAtUtc,
        c.Messages.Where(m => !m.IsInternal)
            .Select(m => new ComplaintMessageDto(m.Id, m.SenderParty.ToString(), m.Message, ReadUrls(m.AttachmentUrlsJson), m.CreatedAtUtc))
            .ToList());

    /// <summary>Đọc mảng URL đã lưu dạng JSON; chuỗi rỗng/hỏng → danh sách rỗng.</summary>
    public static IReadOnlyList<string> ReadUrls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
