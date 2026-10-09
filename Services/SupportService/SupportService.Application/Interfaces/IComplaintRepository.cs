using SupportService.Application.DTOs;
using SupportService.Domain.Entities;

namespace SupportService.Application.Interfaces;

/// <summary>Port đọc/ghi khiếu nại – Infrastructure cài đặt bằng EF Core + LINQ.</summary>
public interface IComplaintRepository
{
    Task<bool> HasOpenComplaintForBookingAsync(int bookingId, CancellationToken cancellationToken = default);

    /// <summary>Lưu ticket + tin nhắn đầu tiên + sự kiện ComplaintCreated vào Outbox trong cùng 1 transaction.</summary>
    Task AddAsync(Complaint complaint, CancellationToken cancellationToken = default);

    /// <summary>Null nếu không có ticket hoặc ticket không thuộc về <paramref name="userId"/>.</summary>
    Task<ComplaintDetailDto?> GetForUserAsync(string code, int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComplaintSummaryDto>> ListForUserAsync(int userId, CancellationToken cancellationToken = default);
}
