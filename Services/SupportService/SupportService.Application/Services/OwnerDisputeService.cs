using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Domain.Entities;
using SupportService.Domain.Rules;

namespace SupportService.Application.Services;

/// <summary>
/// US-089: Chủ bãi phản hồi tranh chấp.
/// AC1 có 48 giờ để phản hồi kèm bằng chứng · AC2 quá hạn → nền tảng xử lý (xem ComplaintEscalationService) ·
/// AC3 khoản tiền tranh chấp bị phong tỏa đến khi có phán quyết.
/// </summary>
public sealed class OwnerDisputeService(IComplaintRepository repository, TimeProvider clock) : IOwnerDisputeService
{
    public async Task<ComplaintDetailDto> GetAsync(int ownerProfileId, string code, CancellationToken cancellationToken = default)
    {
        var complaintCode = ValidateOwnerAndCode(ownerProfileId, code);
        return await repository.GetForOwnerAsync(complaintCode, ownerProfileId, cancellationToken)
               ?? throw new NotFoundException("Complaint", complaintCode);
    }

    public async Task<ComplaintDetailDto> RespondAsync(int ownerProfileId, int? ownerUserId, string code, OwnerResponseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra đầu vào (không tốn truy vấn).
        var complaintCode = ValidateOwnerAndCode(ownerProfileId, code);
        var response = request.Response?.Trim() ?? string.Empty;
        if (response.Length is < ComplaintRules.MinOwnerResponseLength or > ComplaintRules.MaxOwnerResponseLength)
            throw new ValidationException(
                $"Nội dung phản hồi phải dài từ {ComplaintRules.MinOwnerResponseLength} đến {ComplaintRules.MaxOwnerResponseLength} ký tự.");
        var evidence = EvidenceUrls.Normalize(request.EvidenceUrls, ComplaintRules.MaxOwnerEvidenceFiles);

        // 2. Ticket phải thuộc bãi của chủ bãi đang đăng nhập (của chủ bãi khác → 404, không lộ ticket).
        var complaint = await repository.FindForOwnerAsync(complaintCode, ownerProfileId, cancellationToken)
                        ?? throw new NotFoundException("Complaint", complaintCode);

        // 3. Chỉ phản hồi khi ticket đang chờ chủ bãi và còn trong 48 giờ.
        EnsureAwaitingOwner(complaint.Status);
        var now = clock.GetUtcNow().UtcDateTime;
        if (complaint.OwnerResponseDueAtUtc is { } due && now > due)
            throw new ConflictException(
                $"Đã quá hạn {ReviewRules.OwnerResponseHours} giờ phản hồi; tranh chấp được chuyển cho nền tảng xử lý dựa trên bằng chứng hiện có.");

        // 4. Ghi phản hồi + tin nhắn kèm bằng chứng; báo cho tài xế qua Outbox.
        complaint.Status = ComplaintStatus.OwnerResponded;
        complaint.OwnerResponse = response;
        complaint.OwnerRespondedAtUtc = now;
        complaint.Messages.Add(new ComplaintMessage
        {
            SenderParty = ComplaintParty.LotOwner,
            SenderUserId = ownerUserId,
            Message = response,
            AttachmentUrlsJson = EvidenceUrls.ToJson(evidence),
        });
        await repository.UpdateAsync(complaint,
            new ComplaintOwnerResponded(complaint.Id, complaint.Code, complaint.UserId, complaint.ParkingLotId), cancellationToken);

        return await repository.GetForOwnerAsync(complaintCode, ownerProfileId, cancellationToken)
               ?? throw new NotFoundException("Complaint", complaintCode);
    }

    public Task<IReadOnlyList<PayoutHoldDto>> ListPayoutHoldsAsync(int? ownerProfileId, CancellationToken cancellationToken = default)
    {
        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        return repository.ListPayoutHoldsAsync(ownerProfileId, cancellationToken);
    }

    private static string ValidateOwnerAndCode(int ownerProfileId, string code)
    {
        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(code)) throw new ValidationException("Mã khiếu nại không được để trống.");
        return code.Trim();
    }

    private static void EnsureAwaitingOwner(ComplaintStatus status)
    {
        var message = status switch
        {
            ComplaintStatus.AwaitingOwner => null,
            ComplaintStatus.OwnerResponded => "Bạn đã phản hồi tranh chấp này.",
            ComplaintStatus.Escalated => "Đã quá hạn phản hồi; tranh chấp đang được nền tảng xử lý.",
            ComplaintStatus.Resolved or ComplaintStatus.Rejected => "Tranh chấp đã có kết luận.",
            _ => "Tranh chấp này không chờ phản hồi của chủ bãi.",
        };
        if (message is not null) throw new ConflictException(message);
    }
}

/// <summary>US-089 AC2: quá 48 giờ không phản hồi → Escalated, giao Supervisor, ghi tin nhắn hệ thống và phát sự kiện.</summary>
public sealed class ComplaintEscalationService(IComplaintRepository repository, TimeProvider clock) : IComplaintEscalationService
{
    public const int BatchSize = 100;
    public const string Reason = "OwnerNoResponse48h";

    public async Task<int> EscalateOverdueAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var overdueIds = await repository.ListOverdueAwaitingOwnerIdsAsync(now, BatchSize, cancellationToken);
        var escalated = 0;

        foreach (var id in overdueIds)
        {
            // Nạp lại từng ticket ngay trước khi xử lý để có trạng thái + RowVersion mới nhất.
            var complaint = await repository.FindByIdAsync(id, cancellationToken);
            // Kiểm tra lại bằng quy tắc Domain: chủ bãi có thể vừa phản hồi giữa lúc truy vấn danh sách và lúc xử lý.
            if (complaint is null || !ReviewRules.ShouldEscalate(complaint.Status, complaint.OwnerResponseDueAtUtc, now)) continue;

            complaint.Status = ComplaintStatus.Escalated;
            complaint.EscalatedAtUtc = now;
            complaint.AssignedTeam = SupportTeam.Supervisor;
            complaint.Messages.Add(new ComplaintMessage
            {
                SenderParty = ComplaintParty.System,
                Message = $"Chủ bãi không phản hồi trong {ReviewRules.OwnerResponseHours} giờ. Tranh chấp được chuyển cho nền tảng xử lý " +
                          "dựa trên bằng chứng hiện có, ưu tiên dữ liệu phiên đỗ (thiết bị tự động/LPR > GPS/biên lai > thao tác tay).",
            });
            try
            {
                await repository.UpdateAsync(complaint,
                    new ComplaintEscalated(complaint.Id, complaint.Code, complaint.UserId, complaint.ParkingLotId, complaint.OwnerProfileId, Reason),
                    cancellationToken);
                escalated++;
            }
            catch (ConflictException)
            {
                // RowVersion đã đổi: chủ bãi vừa phản hồi hoặc instance khác vừa escalate → không ghi đè, không phát sự kiện trùng.
            }
        }
        return escalated;
    }
}
