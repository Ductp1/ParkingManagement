using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Domain.Entities;
using SupportService.Domain.Rules;

namespace SupportService.Application.Services;

/// <summary>
/// US-093: Đánh giá bãi sau khi hoàn tất.
/// AC1 booking Completed → 1–5 sao + nhận xét, 1 lần/booking · AC2 chưa Completed → không có nút đánh giá ·
/// AC3 điểm trung bình hiển thị qua GET /api/v1/reviews?parkingLotId= và sự kiện ReviewCreated cho xếp hạng tìm kiếm.
/// </summary>
public sealed class ReviewAppService(IReviewRepository repository, IBookingLookup bookings) : IReviewService
{
    private const string AnonymousReviewer = "Tài xế ẩn danh";

    public async Task<ReviewDetailDto> CreateAsync(CreateReviewRequestDto request, CancellationToken cancellationToken = default)
    {
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        var reviewerName = string.IsNullOrWhiteSpace(request.ReviewerName) ? AnonymousReviewer : request.ReviewerName.Trim();
        Validate(request, comment, reviewerName);

        // 1. Booking phải tồn tại và thuộc về người đánh giá.
        var bookingCode = request.BookingCode.Trim();
        var booking = await FindOwnedBookingAsync(request.UserId, bookingCode, cancellationToken)
                      ?? throw new NotFoundException("Booking", bookingCode);

        // 2. AC1/AC2: chỉ booking đã hoàn tất.
        if (!ReviewRules.IsReviewable(booking.Status))
            throw new ValidationException("Chỉ được đánh giá sau khi booking đã hoàn tất.");

        // 3. AC1: mỗi booking 1 lần. Unique index trên BookingId chặn tiếp trường hợp 2 request cùng lúc (→ 409).
        if (await repository.FindIdByBookingAsync(booking.Id, cancellationToken) is not null)
            throw new ConflictException($"Booking {booking.Code} đã được đánh giá.");

        var review = new Review
        {
            BookingId = booking.Id,
            UserId = request.UserId,
            ReviewerName = reviewerName,
            ParkingLotId = booking.ParkingLotId,
            OwnerProfileId = booking.OwnerProfileId,
            Rating = (byte)request.Rating,
            Comment = comment,
        };
        await repository.AddAsync(review, cancellationToken);

        return new ReviewDetailDto(review.Id, booking.Code, review.ParkingLotId, review.Rating, review.Comment,
            review.ReviewerName, review.CreatedAtUtc);
    }

    public async Task<ReviewEligibilityDto> CheckEligibilityAsync(int userId, string bookingCode, CancellationToken cancellationToken = default)
    {
        if (userId <= 0) throw new ValidationException("userId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(bookingCode)) throw new ValidationException("Mã booking không được để trống.");
        var code = bookingCode.Trim();

        var booking = await FindOwnedBookingAsync(userId, code, cancellationToken)
                      ?? throw new NotFoundException("Booking", code);

        if (!ReviewRules.IsReviewable(booking.Status))
            return new ReviewEligibilityDto(booking.Code, false, "Booking chưa hoàn tất.");

        var reviewId = await repository.FindIdByBookingAsync(booking.Id, cancellationToken);
        return reviewId is null
            ? new ReviewEligibilityDto(booking.Code, true, null)
            : new ReviewEligibilityDto(booking.Code, false, "Booking đã được đánh giá.", reviewId);
    }

    /// <summary>Booking của người khác coi như không tồn tại (không tiết lộ booking người khác).</summary>
    private async Task<BookingSnapshot?> FindOwnedBookingAsync(int userId, string bookingCode, CancellationToken cancellationToken)
    {
        var booking = await bookings.FindByCodeAsync(bookingCode, cancellationToken);
        return booking is not null && booking.UserId == userId ? booking : null;
    }

    private static void Validate(CreateReviewRequestDto request, string? comment, string reviewerName)
    {
        if (request.UserId <= 0) throw new ValidationException("userId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(request.BookingCode)) throw new ValidationException("Mã booking không được để trống.");
        if (!ReviewRules.IsValidRating(request.Rating))
            throw new ValidationException($"Số sao phải từ {ReviewRules.MinRating} đến {ReviewRules.MaxRating}.");
        if (comment is { Length: > ReviewRules.MaxCommentLength })
            throw new ValidationException($"Nhận xét tối đa {ReviewRules.MaxCommentLength} ký tự.");
        if (reviewerName.Length > ReviewRules.MaxReviewerNameLength)
            throw new ValidationException($"Tên hiển thị tối đa {ReviewRules.MaxReviewerNameLength} ký tự.");
    }
}
