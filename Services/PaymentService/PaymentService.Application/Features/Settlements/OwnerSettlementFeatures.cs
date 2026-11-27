using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Settlements;

/// <summary>
/// US-080 (UC-35 bước 3): chủ bãi xem các kỳ quyết toán ĐÃ CHỐT (Approved / Paid) của chính mình – OwnerProfileId luôn lấy từ token.
/// Kỳ Draft / PendingReview còn thay đổi nên không hiển thị; kỳ của chủ bãi khác → 404 (không lộ là có tồn tại).
/// </summary>
internal static class OwnerSettlementAccess
{
    public static int RequireOwner(int? ownerProfileId)
        => ownerProfileId is > 0 ? ownerProfileId.Value : throw new ForbiddenException("Tài khoản chưa gắn với một hồ sơ chủ bãi.");

    public static async Task<Settlement> FindVisibleAsync(ISettlementQueries queries, int ownerProfileId, int settlementId,
        CancellationToken cancellationToken)
    {
        if (settlementId <= 0) throw new ValidationException("id phải là số nguyên dương.");
        var settlement = await queries.FindAsync(settlementId, cancellationToken);
        return settlement is not null && settlement.OwnerProfileId == ownerProfileId
                                      && SettlementReviewRules.OwnerVisibleStatuses.Contains(settlement.Status)
            ? settlement
            : throw new NotFoundException("Kỳ quyết toán", settlementId);
    }
}

public interface IListOwnerSettlementsUseCase
{
    Task<PagedResult<OwnerSettlementDto>> ExecuteAsync(int? ownerProfileId, int page, int pageSize, CancellationToken cancellationToken = default);
}

public sealed class ListOwnerSettlementsUseCase(ISettlementQueries queries) : IListOwnerSettlementsUseCase
{
    public async Task<PagedResult<OwnerSettlementDto>> ExecuteAsync(int? ownerProfileId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var owner = OwnerSettlementAccess.RequireOwner(ownerProfileId);
        SettlementReviewRules.EnsurePaging(page, pageSize);
        var result = await queries.ListAsync(new SettlementListFilter(OwnerProfileId: owner, Statuses: SettlementReviewRules.OwnerVisibleStatuses),
            page, pageSize, cancellationToken);
        return new PagedResult<OwnerSettlementDto>([.. result.Items.Select(SettlementReviewRules.ForOwner)], result.Page, result.PageSize,
            result.TotalCount);
    }
}

public interface IGetOwnerSettlementUseCase
{
    Task<OwnerSettlementDetailDto> ExecuteAsync(int? ownerProfileId, int settlementId, CancellationToken cancellationToken = default);
}

public sealed class GetOwnerSettlementUseCase(ISettlementQueries queries) : IGetOwnerSettlementUseCase
{
    public async Task<OwnerSettlementDetailDto> ExecuteAsync(int? ownerProfileId, int settlementId, CancellationToken cancellationToken = default)
    {
        var owner = OwnerSettlementAccess.RequireOwner(ownerProfileId);
        var s = await OwnerSettlementAccess.FindVisibleAsync(queries, owner, settlementId, cancellationToken);
        var totals = await queries.SumLinesByTypeAsync(s.Id, cancellationToken);
        var adjustments = await queries.ListAdjustmentsAsync(new AdjustmentListFilter(SettlementId: s.Id), 1, SettlementReviewRules.MaxPageSize,
            cancellationToken);
        return new OwnerSettlementDetailDto(SettlementReviewRules.ForOwner(s), SettlementReviewRules.Formula(s), totals,
            [.. adjustments.Items.Select(a => new OwnerAdjustmentDto(a.Id, a.Amount, a.Reason, a.SourceType, a.CreatedAtUtc))]);
    }
}

public interface IListOwnerSettlementLinesUseCase
{
    Task<PagedResult<SettlementLineDto>> ExecuteAsync(int? ownerProfileId, int settlementId, string? lineType, int page, int pageSize,
        CancellationToken cancellationToken = default);
}

public sealed class ListOwnerSettlementLinesUseCase(ISettlementQueries queries) : IListOwnerSettlementLinesUseCase
{
    public async Task<PagedResult<SettlementLineDto>> ExecuteAsync(int? ownerProfileId, int settlementId, string? lineType, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var owner = OwnerSettlementAccess.RequireOwner(ownerProfileId);
        SettlementReviewRules.EnsurePaging(page, pageSize);
        var type = SettlementReviewRules.ParseEnum<SettlementLineType>(lineType, "lineType");
        await OwnerSettlementAccess.FindVisibleAsync(queries, owner, settlementId, cancellationToken);
        return await queries.ListLinesAsync(settlementId, type, page, pageSize, cancellationToken);
    }
}

public interface IExportOwnerSettlementLinesUseCase
{
    Task<SettlementCsvFile> ExecuteAsync(int? ownerProfileId, int settlementId, CancellationToken cancellationToken = default);
}

public sealed class ExportOwnerSettlementLinesUseCase(ISettlementQueries queries) : IExportOwnerSettlementLinesUseCase
{
    public async Task<SettlementCsvFile> ExecuteAsync(int? ownerProfileId, int settlementId, CancellationToken cancellationToken = default)
    {
        var owner = OwnerSettlementAccess.RequireOwner(ownerProfileId);
        var s = await OwnerSettlementAccess.FindVisibleAsync(queries, owner, settlementId, cancellationToken);
        return await SettlementLookup.LinesCsvAsync(queries, s, cancellationToken);
    }
}
