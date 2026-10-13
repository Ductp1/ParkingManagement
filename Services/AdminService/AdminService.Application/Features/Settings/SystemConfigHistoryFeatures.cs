using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Settings;

// ===== PORT (đọc) =====
/// <summary>Một dòng lịch sử thay đổi đầy đủ, đúng như lưu trong pm_admin.</summary>
public sealed record SystemConfigChangeRow(int Id, string Value, DateTime EffectiveFromUtc, string Reason, int CreatedByUserId,
    DateTime CreatedAtUtc, DateTime? CancelledAtUtc, int? CancelledByUserId, string? CancelReason);

/// <summary>Giá trị gốc + toàn bộ lịch sử thay đổi của một tham số (chưa tính trạng thái).</summary>
public sealed record SystemConfigHistoryRecord(string Key, string DefaultValue, IReadOnlyList<SystemConfigChangeRow> Changes);

public interface ISystemConfigChangeQueries
{
    /// <summary>Lịch sử thay đổi của một tham số theo khóa (khớp chính xác); null nếu khóa không tồn tại.</summary>
    Task<SystemConfigHistoryRecord?> FindHistoryByKeyAsync(string key, CancellationToken cancellationToken);
}

// ===== USE CASE (đọc) =====
public interface IGetSystemConfigHistoryUseCase
{
    Task<PagedResult<SystemConfigChangeDto>> ExecuteAsync(string key, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-098 (UC-44): Lịch sử thay đổi của một tham số, mốc hiệu lực mới nhất trước – gồm cả thay đổi đã lên lịch và đã hủy.
/// Trạng thái và giá trị liền trước của từng dòng phụ thuộc các dòng khác nên được tính trên toàn bộ lịch sử rồi mới phân trang.
/// </summary>
public sealed class GetSystemConfigHistoryUseCase(ISystemConfigChangeQueries queries, TimeProvider timeProvider) : IGetSystemConfigHistoryUseCase
{
    public async Task<PagedResult<SystemConfigChangeDto>> ExecuteAsync(string key, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ValidationException("page phải ≥ 1.");
        if (pageSize is < 1 or > 100) throw new ValidationException("pageSize phải trong khoảng 1–100.");

        var normalizedKey = SystemConfigMapper.NormalizeKey(key);
        var history = await queries.FindHistoryByKeyAsync(normalizedKey, cancellationToken)
            ?? throw new NotFoundException("Tham số hệ thống", normalizedKey);

        var all = SystemConfigChangeTimeline.Build(history, timeProvider.GetUtcNow().UtcDateTime);
        var skip = (long)(page - 1) * pageSize;       // long: page rất lớn không làm tràn số rồi trả nhầm trang đầu
        return new PagedResult<SystemConfigChangeDto>(
            skip >= all.Count ? [] : [.. all.Skip((int)skip).Take(pageSize)], page, pageSize, all.Count);
    }
}

/// <summary>US-098: Dựng dòng thời gian thay đổi của một tham số. Hàm thuần – không đọc database, không đọc đồng hồ.</summary>
public static class SystemConfigChangeTimeline
{
    /// <summary>Mọi thay đổi, mốc hiệu lực mới nhất trước, kèm trạng thái tại nowUtc và giá trị hiệu lực ngay trước từng thay đổi.</summary>
    public static IReadOnlyList<SystemConfigChangeDto> Build(SystemConfigHistoryRecord history, DateTime nowUtc)
    {
        // Thay đổi còn giá trị, cũ → mới. Mỗi mốc hiệu lực chỉ có 1 dòng (unique index).
        var active = history.Changes.Where(c => c.CancelledAtUtc is null).OrderBy(c => c.EffectiveFromUtc).ThenBy(c => c.Id).ToList();
        var current = active.LastOrDefault(c => c.EffectiveFromUtc <= nowUtc);

        return
        [
            .. history.Changes
                .OrderByDescending(c => c.EffectiveFromUtc)
                .ThenByDescending(c => c.Id)
                .Select(c => new SystemConfigChangeDto(c.Id, history.Key, c.Value,
                    active.LastOrDefault(a => a.EffectiveFromUtc < c.EffectiveFromUtc)?.Value ?? history.DefaultValue,
                    c.EffectiveFromUtc, StatusOf(c, current, nowUtc).ToString(), c.Reason, c.CreatedByUserId, c.CreatedAtUtc,
                    c.CancelledAtUtc, c.CancelledByUserId, c.CancelReason)),
        ];
    }

    private static SystemConfigChangeStatus StatusOf(SystemConfigChangeRow change, SystemConfigChangeRow? current, DateTime nowUtc)
        => change switch
        {
            { CancelledAtUtc: not null } => SystemConfigChangeStatus.Cancelled,
            _ when change.EffectiveFromUtc > nowUtc => SystemConfigChangeStatus.Scheduled,
            _ when change.Id == current?.Id => SystemConfigChangeStatus.Effective,
            _ => SystemConfigChangeStatus.Superseded,
        };
}
