using ParkingManagement.SharedKernel.Enums;

namespace SupportService.Application.Features.AdminDashboard;

// ===== DTO =====

/// <summary>
/// US-081: tranh chấp / khiếu nại chưa đóng toàn sàn (mọi category, kể cả lỗi app không có chủ bãi).
/// AwaitingOwnerOverdue ⊂ AwaitingOwner: đã quá hạn phản hồi 48 giờ nhưng worker chưa kịp escalate.
/// Chỉ số đếm – không trả người gửi, booking hay nội dung khiếu nại.
/// </summary>
public sealed record AdminComplaintSummaryDto(DateTime AsOfUtc, int Open, int AwaitingOwner, int AwaitingOwnerOverdue, int OwnerResponded,
    int Escalated)
{
    public int Unresolved => Open + AwaitingOwner + OwnerResponded + Escalated;
}

// ===== PORT (đọc) =====

public sealed record ComplaintStatusCountRow(ComplaintStatus Status, int Count);

/// <summary>Đếm ở database (AsNoTracking, GroupBy, Count), không tải bản ghi về.</summary>
public interface IAdminComplaintSummaryQueries
{
    /// <summary>Số khiếu nại theo trạng thái, chỉ các trạng thái chưa đóng.</summary>
    Task<IReadOnlyList<ComplaintStatusCountRow>> CountUnresolvedByStatusAsync(CancellationToken cancellationToken);

    /// <summary>AwaitingOwner có OwnerResponseDueAtUtc &lt; nowUtc.</summary>
    Task<int> CountAwaitingOwnerOverdueAsync(DateTime nowUtc, CancellationToken cancellationToken);
}

// ===== USE CASE =====

public interface IGetAdminComplaintSummaryUseCase
{
    Task<AdminComplaintSummaryDto> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>US-081 (UC-45): tổng tranh chấp đang mở cho Admin Dashboard; trạng thái không có bản ghi nào là 0 thật.</summary>
public sealed class GetAdminComplaintSummaryUseCase(IAdminComplaintSummaryQueries queries, TimeProvider timeProvider)
    : IGetAdminComplaintSummaryUseCase
{
    public async Task<AdminComplaintSummaryDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var counts = await queries.CountUnresolvedByStatusAsync(cancellationToken);
        var overdue = await queries.CountAwaitingOwnerOverdueAsync(now, cancellationToken);
        int Count(ComplaintStatus status) => counts.Where(c => c.Status == status).Sum(c => c.Count);

        return new AdminComplaintSummaryDto(now, Count(ComplaintStatus.Open), Count(ComplaintStatus.AwaitingOwner), overdue,
            Count(ComplaintStatus.OwnerResponded), Count(ComplaintStatus.Escalated));
    }
}
