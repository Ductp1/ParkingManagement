using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Sanctions;

// ===== USE CASE (đọc) =====
public interface IListSanctionsUseCase
{
    Task<PagedResult<SanctionSummaryDto>> ExecuteAsync(SanctionListQuery query, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>US-097: danh sách chế tài toàn sàn – lọc trạng thái hiệu lực, cấp, chủ bãi, bãi, từ khóa, khoảng ngày áp, đồng bộ lỗi.</summary>
public sealed class ListSanctionsUseCase(ISanctionAdminQueries queries, TimeProvider timeProvider) : IListSanctionsUseCase
{
    public Task<PagedResult<SanctionSummaryDto>> ExecuteAsync(SanctionListQuery query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ValidationException("page phải ≥ 1.");
        if (pageSize is < 1 or > 100) throw new ValidationException("pageSize phải trong khoảng 1–100.");
        var filter = SanctionRules.ToFilter(query);
        return queries.ListAsync(filter, timeProvider.GetUtcNow().UtcDateTime, page, pageSize, cancellationToken);
    }
}

public interface IGetSanctionUseCase
{
    Task<SanctionDetailDto> ExecuteAsync(int sanctionId, CancellationToken cancellationToken = default);
}

/// <summary>US-097: chi tiết chế tài – thông tin, trạng thái đồng bộ từng service, cảnh báo rủi ro liên quan, timeline từ audit log.</summary>
public sealed class GetSanctionUseCase(ISanctionAdminQueries queries, TimeProvider timeProvider) : IGetSanctionUseCase
{
    public async Task<SanctionDetailDto> ExecuteAsync(int sanctionId, CancellationToken cancellationToken = default)
    {
        if (sanctionId <= 0) throw new ValidationException("sanctionId phải là số nguyên dương.");
        return await queries.GetDetailAsync(sanctionId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken)
            ?? throw new NotFoundException("Chế tài", sanctionId);
    }
}

public interface IGetSanctionStatsUseCase
{
    Task<SanctionStatsDto> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>US-097: KPI trang Sanctions (đếm ở database).</summary>
public sealed class GetSanctionStatsUseCase(ISanctionAdminQueries queries, TimeProvider timeProvider) : IGetSanctionStatsUseCase
{
    public Task<SanctionStatsDto> ExecuteAsync(CancellationToken cancellationToken = default)
        => queries.GetStatsAsync(timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
}

public interface IGetSanctionSuggestionUseCase
{
    Task<SanctionSuggestionDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-097: gợi ý cấp kế tiếp cho chủ bãi – chỉ tham khảo, Admin quyết định. Xét các chế tài chưa bị gỡ áp trong
/// SuggestionWindowDays ngày gần nhất: chưa có → Cảnh cáo; có → cấp cao nhất + 1 (tối đa Chấm dứt hợp tác).
/// </summary>
public sealed class GetSanctionSuggestionUseCase(ISanctionAdminQueries queries, SanctionSettings settings, TimeProvider timeProvider)
    : IGetSanctionSuggestionUseCase
{
    public async Task<SanctionSuggestionDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default)
    {
        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var window = settings.SuggestionWindowDays;
        var basis = await queries.ListNotRevokedForOwnerSinceAsync(ownerProfileId, now.AddDays(-window), now, cancellationToken);

        if (basis.Count == 0)
            return new SanctionSuggestionDto(ownerProfileId, nameof(SanctionLevel.Warning), window,
                $"Chưa có chế tài nào trong {window} ngày gần nhất – bắt đầu từ Cảnh cáo.", basis);

        var highest = basis.Select(s => Enum.Parse<SanctionLevel>(s.Level)).Max();
        var suggested = highest == SanctionLevel.PermanentBan ? highest : highest + 1;
        var rationale = highest == SanctionLevel.PermanentBan
            ? $"Chủ bãi đã ở cấp cao nhất ({SanctionRules.LevelLabel(highest)}) trong {window} ngày gần nhất."
            : $"{basis.Count} chế tài trong {window} ngày gần nhất, cao nhất là {SanctionRules.LevelLabel(highest)} – đề xuất " +
              $"{SanctionRules.LevelLabel(suggested)}.";
        return new SanctionSuggestionDto(ownerProfileId, suggested.ToString(), window, rationale, basis);
    }
}
