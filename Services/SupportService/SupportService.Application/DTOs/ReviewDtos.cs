namespace SupportService.Application.DTOs;

/// <summary>Body của POST /api/v1/reviews – tài xế đánh giá bãi sau khi booking hoàn tất (US-093).</summary>
/// <param name="ReviewerName">Tên hiển thị; bỏ trống → "Tài xế ẩn danh".</param>
public sealed record CreateReviewRequestDto(
    int UserId,
    string BookingCode,
    int Rating,
    string? Comment = null,
    string? ReviewerName = null);

/// <summary>Đánh giá vừa tạo.</summary>
public sealed record ReviewDetailDto(int Id, string BookingCode, int ParkingLotId, int Rating, string? Comment,
    string ReviewerName, DateTime CreatedAtUtc);

/// <summary>US-093 AC2: app dùng để quyết định có hiện nút "Đánh giá" cho booking hay không.</summary>
/// <param name="ReviewId">Id đánh giá đã có (nếu booking đã được đánh giá).</param>
public sealed record ReviewEligibilityDto(string BookingCode, bool CanReview, string? Reason, int? ReviewId = null);
