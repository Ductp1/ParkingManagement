using ParkingManagement.SharedKernel.Contracts;
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

    // ===== US-089: chủ bãi phản hồi tranh chấp =====

    /// <summary>Ticket (được EF theo dõi để cập nhật) thuộc bãi của chủ bãi; null nếu không có hoặc của chủ bãi khác.</summary>
    Task<Complaint?> FindForOwnerAsync(string code, int ownerProfileId, CancellationToken cancellationToken = default);

    /// <summary>Chi tiết ticket cho chủ bãi (ẩn ghi chú nội bộ); null nếu không thuộc chủ bãi này.</summary>
    Task<ComplaintDetailDto?> GetForOwnerAsync(string code, int ownerProfileId, CancellationToken cancellationToken = default);

    /// <summary>Id các ticket đang chờ chủ bãi mà đã quá hạn phản hồi (không theo dõi – mỗi ticket được nạp lại khi xử lý).</summary>
    Task<IReadOnlyList<int>> ListOverdueAwaitingOwnerIdsAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);

    /// <summary>Ticket theo Id (được EF theo dõi để cập nhật); null nếu không có.</summary>
    Task<Complaint?> FindByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu thay đổi của ticket + sự kiện vào Outbox trong cùng 1 transaction, có kiểm tra RowVersion.
    /// Ticket đã bị bên khác cập nhật kể từ lúc nạp → <see cref="ParkingManagement.SharedKernel.Exceptions.ConflictException"/>,
    /// không ghi gì (kể cả sự kiện).
    /// </summary>
    Task UpdateAsync(Complaint complaint, IntegrationEvent integrationEvent, CancellationToken cancellationToken = default);

    /// <summary>Khoản tiền đang bị giữ vì tranh chấp chưa xong; null = mọi chủ bãi (Admin).</summary>
    Task<IReadOnlyList<PayoutHoldDto>> ListPayoutHoldsAsync(int? ownerProfileId, CancellationToken cancellationToken = default);
}
