using SupportService.Application.DTOs;

namespace SupportService.Application.Interfaces;

/// <summary>Nghiệp vụ đánh giá bãi sau khi hoàn tất (US-093).</summary>
public interface IReviewService
{
    Task<ReviewDetailDto> CreateAsync(CreateReviewRequestDto request, CancellationToken cancellationToken = default);
    Task<ReviewEligibilityDto> CheckEligibilityAsync(int userId, string bookingCode, CancellationToken cancellationToken = default);
}
