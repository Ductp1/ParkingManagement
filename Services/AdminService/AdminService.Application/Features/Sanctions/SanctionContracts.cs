using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Application.Features.Sanctions;

// ===== COMMAND =====
/// <summary>
/// US-097: Admin áp chế tài. OwnerUserId = user sở hữu hồ sơ chủ bãi (xác minh qua UserService, cũng là người nhận thông báo).
/// ParkingLotIds rỗng = áp lên toàn bộ chủ bãi; có bãi = mỗi bãi 1 chế tài. Admin thao tác lấy từ JWT, không nằm trong body.
/// EndsAtUtc: hạ hạng (≤ 30 ngày) / tạm ngừng – bắt buộc; cảnh cáo – dùng CorrectionDueAtUtc; cấm vĩnh viễn – để trống.
/// RiskFlagId: cảnh báo rủi ro được xử lý bằng chế tài này (tùy chọn).
/// </summary>
public sealed record IssueSanctionRequest(int OwnerProfileId, int OwnerUserId, SanctionLevel? Level, IReadOnlyList<int>? ParkingLotIds,
    string? Reason, string? EvidenceJson = null, DateTime? EndsAtUtc = null, DateTime? CorrectionDueAtUtc = null,
    decimal? PenaltyAmount = null, int? RiskFlagId = null);

/// <summary>US-097: gỡ chế tài – bắt buộc có lý do.</summary>
public sealed record RevokeSanctionRequest(string? Reason);

// ===== DTO =====
/// <summary>
/// Một chế tài trong danh sách. Status là trạng thái hiệu lực: chế tài Active đã qua EndsAtUtc (job chưa kịp đóng) trả "Expired",
/// cùng quy tắc Sanction.IsInEffect / widget dashboard US-081.
/// </summary>
public sealed record SanctionSummaryDto(int Id, int OwnerProfileId, int? OwnerUserId, int? ParkingLotId, string Level, string Reason,
    DateTime StartsAtUtc, DateTime? EndsAtUtc, DateTime? CorrectionDueAtUtc, DateTime? AppealDeadlineUtc, string Status,
    decimal? PenaltyAmount, int IssuedByUserId, DateTime? RevokedAtUtc, string UserServiceSyncStatus, string ParkingSyncStatus,
    string NotificationSyncStatus, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

/// <summary>Kết quả áp chế tài: 1 chế tài cho toàn chủ bãi hoặc 1 chế tài / bãi.</summary>
public sealed record IssueSanctionResultDto(IReadOnlyList<SanctionSummaryDto> Sanctions);

/// <summary>Một mốc trong lịch sử chế tài – lấy từ audit log (áp, gỡ, khóa / mở khóa chủ bãi).</summary>
public sealed record SanctionTimelineEntryDto(int AuditLogId, DateTime AtUtc, string Action, int? UserId, string? Reason);

public sealed record SanctionDetailDto(SanctionSummaryDto Sanction, string? EvidenceJson, int? RevokedByUserId, string? RevokeReason,
    DateTime? UserServiceSyncedAtUtc, DateTime? ParkingSyncedAtUtc, DateTime? NotificationSentAtUtc, IReadOnlyList<int> RiskFlagIds,
    IReadOnlyList<SanctionTimelineEntryDto> Timeline);

/// <summary>KPI trang Sanctions: đang hiệu lực theo cấp, số chế tài có service đồng bộ lỗi / còn chờ, sắp hết hạn trong 7 ngày.</summary>
public sealed record SanctionStatsDto(int ActiveTotal, int Warning, int RankingPenalty, int TemporarySuspension, int PermanentBan,
    int SyncFailed, int SyncPending, int EndingWithin7Days);

/// <summary>Gợi ý cấp kế tiếp (chỉ tham khảo – Admin quyết). Basis = các chế tài chưa bị gỡ trong cửa sổ xét.</summary>
public sealed record SanctionSuggestionDto(int OwnerProfileId, string SuggestedLevel, int WindowDays, string Rationale,
    IReadOnlyList<SanctionSummaryDto> Basis);

/// <summary>Tham số lọc danh sách nhận từ query string (chưa kiểm tra). from/to (UTC) lọc theo StartsAtUtc.</summary>
public sealed record SanctionListQuery(SanctionStatus? Status = null, SanctionLevel? Level = null, int? OwnerProfileId = null,
    int? ParkingLotId = null, string? Search = null, DateTime? From = null, DateTime? To = null, bool? SyncFailed = null);

/// <summary>Bộ lọc đã kiểm tra: chuỗi đã trim (rỗng = không lọc), mốc thời gian đã về UTC.</summary>
public sealed record SanctionFilter(SanctionStatus? Status, SanctionLevel? Level, int? OwnerProfileId, int? ParkingLotId, string? Search,
    DateTime? FromUtc, DateTime? ToUtc, bool SyncFailedOnly);

/// <summary>
/// Cấu hình "Sanctions" (appsettings): SuggestionWindowDays – số ngày xét lịch sử để gợi ý cấp; AppealBusinessDays – Điều 7.3.
/// </summary>
public sealed record SanctionSettings(int SuggestionWindowDays = 90, int AppealBusinessDays = 5);

/// <summary>Bãi của chủ bãi theo ParkingService (dùng xác minh bãi thuộc chủ bãi).</summary>
public sealed record OwnerLotInfo(int Id, string Name, string Status);

// ===== PORT (đọc) =====
public interface ISanctionAdminQueries
{
    Task<PagedResult<SanctionSummaryDto>> ListAsync(SanctionFilter filter, DateTime nowUtc, int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Null nếu không có chế tài.</summary>
    Task<SanctionDetailDto?> GetDetailAsync(int id, DateTime nowUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<SanctionSummaryDto>> ListByIdsAsync(IReadOnlyCollection<int> ids, DateTime nowUtc, CancellationToken cancellationToken);
    Task<SanctionStatsDto> GetStatsAsync(DateTime nowUtc, CancellationToken cancellationToken);
    /// <summary>Chế tài chưa bị gỡ của chủ bãi có StartsAtUtc ≥ sinceUtc, mới nhất trước.</summary>
    Task<IReadOnlyList<SanctionSummaryDto>> ListNotRevokedForOwnerSinceAsync(int ownerProfileId, DateTime sinceUtc, DateTime nowUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// US-097: danh sách bãi của chủ bãi bên ParkingService (GET /api/v1/parking-lots/owner/{ownerProfileId}). Lỗi: 401 / 403 →
/// AuthenticationException / ForbiddenException; không gọi được → DependencyUnavailableException (503).
/// </summary>
public interface IParkingLotDirectory
{
    Task<IReadOnlyList<OwnerLotInfo>> ListOwnerLotsAsync(int ownerProfileId, CancellationToken cancellationToken);
}

// ===== PORT (ghi) =====
public interface ISanctionRepository
{
    /// <summary>Chế tài có tracking; null nếu không có.</summary>
    Task<Sanction?> FindTrackedAsync(int id, CancellationToken cancellationToken);
    /// <summary>Chế tài cấp bãi đang Active (kể cả đã quá hạn nhưng chưa đóng) của các bãi ở cấp này, có tracking.</summary>
    Task<IReadOnlyList<Sanction>> FindActiveLotSanctionsTrackedAsync(IReadOnlyCollection<int> parkingLotIds, SanctionLevel level,
        CancellationToken cancellationToken);
    /// <summary>Chế tài hạ hạng toàn chủ bãi đang Active (tối đa 1 – unique index), có tracking.</summary>
    Task<Sanction?> FindActiveOwnerRankingPenaltyTrackedAsync(int ownerProfileId, CancellationToken cancellationToken);
    Task<RiskFlag?> FindRiskFlagTrackedAsync(int id, CancellationToken cancellationToken);
    /// <summary>
    /// Ghi các chế tài mới + audit log của từng chế tài trong 1 transaction: thay đổi đang tracking (chế tài cũ vừa đóng) ghi trước,
    /// rồi chế tài mới, rồi audit log (EntityId = Id vừa sinh) + afterInsert (gắn RiskFlag); lỗi ở bước nào cũng rollback hết.
    /// Hai request cùng lúc: unique index chặn request sau → ConflictException.
    /// </summary>
    Task AddIssuedAsync(IReadOnlyList<Sanction> sanctions, Func<Sanction, AuditLog> buildAuditLog, Action<IReadOnlyList<Sanction>>? afterInsert,
        CancellationToken cancellationToken);
    /// <summary>Ghi thay đổi của chế tài đang tracking + audit log trong CÙNG 1 lần SaveChanges.</summary>
    Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken);
}
