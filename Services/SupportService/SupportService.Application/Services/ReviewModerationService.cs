using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Domain.Entities;
using SupportService.Domain.Rules;

namespace SupportService.Application.Services;

/// <summary>
/// US-094: Chủ bãi phản hồi đánh giá.
/// AC1 chủ bãi viết 1 phản hồi công khai cho đánh giá về bãi mình ·
/// AC2 chủ bãi khiếu nại đánh giá → chuyển Admin xem xét; chỉ Admin được gỡ (mọi thao tác của Admin ghi AuditRecorded).
/// </summary>
public sealed class ReviewModerationService(IReviewRepository repository, IOwnerLotLookup ownerLots, TimeProvider clock)
    : IReviewModerationService
{
    public const int ReportedQueueSize = 100;
    private const string SourceService = "SupportService";

    public async Task<ModeratedReviewDto> ReplyAsync(int ownerProfileId, int reviewId, OwnerReplyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var reply = RequireText(request.Reply, ReviewRules.MinOwnerReplyLength, ReviewRules.MaxOwnerReplyLength, "Phản hồi");
        var review = await FindOwnedAsync(ownerProfileId, reviewId, cancellationToken);
        if (review.OwnerReply is not null) throw new ConflictException("Bạn đã phản hồi đánh giá này (mỗi đánh giá chỉ 1 phản hồi).");

        review.ReplyAsOwner(reply, clock.GetUtcNow().UtcDateTime);
        await repository.UpdateAsync(review, [new ReviewOwnerReplied(review.Id, review.ParkingLotId, review.UserId)], cancellationToken);
        return ToDto(review);
    }

    public async Task<ModeratedReviewDto> ReportAsync(int ownerProfileId, int reviewId, ReviewReasonRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var reason = RequireText(request.Reason, ReviewRules.MinReasonLength, ReviewRules.MaxReasonLength, "Lý do khiếu nại");
        var review = await FindOwnedAsync(ownerProfileId, reviewId, cancellationToken);
        if (review.IsFlagged) throw new ConflictException("Đánh giá này đang chờ Admin xem xét.");

        review.ReportByOwner(ownerProfileId, reason, clock.GetUtcNow().UtcDateTime);
        await repository.UpdateAsync(review, [], cancellationToken);
        return ToDto(review);
    }

    public Task<IReadOnlyList<ModeratedReviewDto>> ListReportedAsync(CancellationToken cancellationToken = default)
        => repository.ListReportedAsync(ReportedQueueSize, cancellationToken);

    public async Task<ModeratedReviewDto> HideAsync(int adminUserId, int reviewId, ReviewReasonRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var reason = RequireText(request.Reason, ReviewRules.MinReasonLength, ReviewRules.MaxReasonLength, "Lý do gỡ");
        var review = await FindForAdminAsync(adminUserId, reviewId, cancellationToken);
        if (review.IsHidden) throw new ConflictException("Đánh giá đã bị gỡ trước đó.");

        var wasReported = review.IsFlagged;
        review.HideByAdmin(adminUserId, reason, clock.GetUtcNow().UtcDateTime);
        await repository.UpdateAsync(review,
        [
            new ReviewHidden(review.Id, review.ParkingLotId, review.Rating),   // ParkingService trừ khỏi điểm xếp hạng
            Audit(adminUserId, "ReviewHidden", review, new { IsHidden = false, IsFlagged = wasReported }, new { IsHidden = true, IsFlagged = false }, reason),
        ], cancellationToken);
        return ToDto(review);
    }

    public async Task<ModeratedReviewDto> DismissReportAsync(int adminUserId, int reviewId, ReviewReasonRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var reason = RequireText(request.Reason, ReviewRules.MinReasonLength, ReviewRules.MaxReasonLength, "Lý do bác khiếu nại");
        var review = await FindForAdminAsync(adminUserId, reviewId, cancellationToken);
        if (!review.IsFlagged) throw new ConflictException("Đánh giá không có khiếu nại đang chờ xử lý.");

        review.DismissReport();
        await repository.UpdateAsync(review,
            [Audit(adminUserId, "ReviewReportDismissed", review, new { IsFlagged = true }, new { IsFlagged = false }, reason)], cancellationToken);
        return ToDto(review);
    }

    /// <summary>
    /// Đánh giá thuộc bãi của chủ bãi đang đăng nhập: khớp OwnerProfileId đã lưu, hoặc bãi nằm trong danh sách bãi
    /// của chủ bãi (ParkingService). Đánh giá của bãi khác / đã bị gỡ → 404 (không lộ dữ liệu).
    /// </summary>
    private async Task<Review> FindOwnedAsync(int ownerProfileId, int reviewId, CancellationToken cancellationToken)
    {
        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        if (reviewId <= 0) throw new ValidationException("Id đánh giá phải là số nguyên dương.");

        var review = await repository.FindByIdAsync(reviewId, cancellationToken);
        if (review is null || review.IsHidden) throw new NotFoundException("Review", reviewId);
        if (review.OwnerProfileId == ownerProfileId) return review;

        // BookingService chưa trả chủ bãi nên nhiều đánh giá có OwnerProfileId = null → đối chiếu theo bãi.
        var lots = await ownerLots.GetLotIdsAsync(ownerProfileId, cancellationToken);
        if (!lots.Contains(review.ParkingLotId)) throw new NotFoundException("Review", reviewId);
        review.OwnerProfileId ??= ownerProfileId;   // bổ sung chủ bãi cho đánh giá cũ
        return review;
    }

    private async Task<Review> FindForAdminAsync(int adminUserId, int reviewId, CancellationToken cancellationToken)
    {
        if (adminUserId <= 0) throw new ValidationException("adminUserId phải là số nguyên dương.");
        if (reviewId <= 0) throw new ValidationException("Id đánh giá phải là số nguyên dương.");
        return await repository.FindByIdAsync(reviewId, cancellationToken) ?? throw new NotFoundException("Review", reviewId);
    }

    private static string RequireText(string? value, int min, int max, string field)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length < min || text.Length > max)
            throw new ValidationException($"{field} phải dài từ {min} đến {max} ký tự.");
        return text;
    }

    /// <summary>Mọi thao tác kiểm duyệt của Admin đều ghi audit log (AdminService lưu, không sửa được – US-090 AC4).</summary>
    private static AuditRecorded Audit(int adminUserId, string action, Review review, object oldValues, object newValues, string reason)
        => new(SourceService, adminUserId, action, nameof(Review), review.Id.ToString(),
            JsonSerializer.Serialize(oldValues), JsonSerializer.Serialize(newValues), reason);

    public static ModeratedReviewDto ToDto(Review r) => new(
        r.Id, r.ParkingLotId, r.ReviewerName, r.Rating, r.Comment, r.OwnerReply, r.OwnerRepliedAtUtc,
        r.IsFlagged, r.FlagReason, r.FlaggedAtUtc, r.IsHidden, r.CreatedAtUtc);
}
