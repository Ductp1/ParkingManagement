using ParkingManagement.SharedKernel.Contracts;
using SupportService.Application.DTOs;
using SupportService.Domain.Entities;

namespace SupportService.Application.Interfaces;

/// <summary>Port đọc/ghi đánh giá – Infrastructure cài đặt bằng EF Core.</summary>
public interface IReviewRepository
{
    /// <summary>Id đánh giá đã có cho booking (tính cả đánh giá đã bị xóa mềm); null nếu chưa có.</summary>
    Task<int?> FindIdByBookingAsync(int bookingId, CancellationToken cancellationToken = default);

    /// <summary>Lưu đánh giá + sự kiện ReviewCreated vào Outbox trong cùng 1 transaction.</summary>
    Task AddAsync(Review review, CancellationToken cancellationToken = default);

    // ===== US-094: chủ bãi phản hồi / khiếu nại, Admin gỡ =====

    /// <summary>Đánh giá theo Id (được EF theo dõi để cập nhật); null nếu không có hoặc đã xóa mềm.</summary>
    Task<Review?> FindByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu thay đổi + các sự kiện vào Outbox trong 1 transaction, có kiểm tra RowVersion.
    /// Bên khác đã cập nhật đánh giá kể từ lúc nạp → <see cref="ParkingManagement.SharedKernel.Exceptions.ConflictException"/>, không ghi gì.
    /// </summary>
    Task UpdateAsync(Review review, IReadOnlyCollection<IntegrationEvent> events, CancellationToken cancellationToken = default);

    /// <summary>Hàng chờ Admin: đánh giá đang bị chủ bãi khiếu nại, cũ nhất trước.</summary>
    Task<IReadOnlyList<ModeratedReviewDto>> ListReportedAsync(int take, CancellationToken cancellationToken = default);
}

/// <summary>Port tra các bãi của 1 chủ bãi – Infrastructure cài đặt bằng HTTP tới ParkingService.</summary>
public interface IOwnerLotLookup
{
    Task<IReadOnlyCollection<int>> GetLotIdsAsync(int ownerProfileId, CancellationToken cancellationToken = default);
}

/// <summary>US-094: chủ bãi phản hồi / khiếu nại đánh giá; Admin xem hàng chờ, gỡ hoặc bác khiếu nại.</summary>
public interface IReviewModerationService
{
    Task<ModeratedReviewDto> ReplyAsync(int ownerProfileId, int reviewId, OwnerReplyRequestDto request, CancellationToken cancellationToken = default);
    Task<ModeratedReviewDto> ReportAsync(int ownerProfileId, int reviewId, ReviewReasonRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModeratedReviewDto>> ListReportedAsync(CancellationToken cancellationToken = default);
    Task<ModeratedReviewDto> HideAsync(int adminUserId, int reviewId, ReviewReasonRequestDto request, CancellationToken cancellationToken = default);
    Task<ModeratedReviewDto> DismissReportAsync(int adminUserId, int reviewId, ReviewReasonRequestDto request, CancellationToken cancellationToken = default);
}
