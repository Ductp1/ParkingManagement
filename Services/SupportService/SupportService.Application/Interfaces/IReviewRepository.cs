using SupportService.Domain.Entities;

namespace SupportService.Application.Interfaces;

/// <summary>Port đọc/ghi đánh giá – Infrastructure cài đặt bằng EF Core.</summary>
public interface IReviewRepository
{
    /// <summary>Id đánh giá đã có cho booking (tính cả đánh giá đã bị xóa mềm); null nếu chưa có.</summary>
    Task<int?> FindIdByBookingAsync(int bookingId, CancellationToken cancellationToken = default);

    /// <summary>Lưu đánh giá + sự kiện ReviewCreated vào Outbox trong cùng 1 transaction.</summary>
    Task AddAsync(Review review, CancellationToken cancellationToken = default);
}
