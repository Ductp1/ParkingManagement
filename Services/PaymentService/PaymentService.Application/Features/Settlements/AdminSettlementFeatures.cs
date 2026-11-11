using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Settlements;

// ======================= ĐỌC (Admin) =======================

/// <summary>Tham số lọc danh sách kỳ nhận từ query string (chưa kiểm tra).</summary>
public sealed record SettlementListQuery(string? Status, int? OwnerProfileId, DateOnly? PeriodStart, string? Search, bool? Blocked)
{
    public SettlementListFilter ToFilter()
    {
        if (OwnerProfileId is <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        return new SettlementListFilter(SettlementReviewRules.ParseEnum<SettlementStatus>(Status, "status"), OwnerProfileId, PeriodStart,
            SettlementReviewRules.NormalizeSearch(Search), Blocked);
    }
}

public interface IListSettlementsUseCase
{
    Task<PagedResult<SettlementSummaryDto>> ExecuteAsync(SettlementListQuery query, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>US-080: danh sách kỳ quyết toán (lọc trạng thái / chủ bãi / kỳ / bị chặn, tìm theo mã kỳ hoặc số chủ bãi), mới nhất trước.</summary>
public sealed class ListSettlementsUseCase(ISettlementQueries queries) : IListSettlementsUseCase
{
    public Task<PagedResult<SettlementSummaryDto>> ExecuteAsync(SettlementListQuery query, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        SettlementReviewRules.EnsurePaging(page, pageSize);
        return queries.ListAsync(query.ToFilter(), page, pageSize, cancellationToken);
    }
}

public interface IGetSettlementStatsUseCase
{
    Task<SettlementStatsDto> ExecuteAsync(DateOnly? periodStart, CancellationToken cancellationToken = default);
}

/// <summary>US-080: KPI trang Quyết toán – số kỳ theo trạng thái, số kỳ bị chặn, tổng thực chi chờ duyệt / chờ chuyển.</summary>
public sealed class GetSettlementStatsUseCase(ISettlementQueries queries) : IGetSettlementStatsUseCase
{
    public Task<SettlementStatsDto> ExecuteAsync(DateOnly? periodStart, CancellationToken cancellationToken = default)
        => queries.GetStatsAsync(periodStart, cancellationToken);
}

public interface IGetSettlementUseCase
{
    Task<SettlementDetailDto> ExecuteAsync(int settlementId, CancellationToken cancellationToken = default);
}

/// <summary>US-080: chi tiết kỳ – công thức NetPayout, mốc trạng thái, tổng theo loại dòng, bút toán đã gắn, thao tác được phép.</summary>
public sealed class GetSettlementUseCase(ISettlementQueries queries) : IGetSettlementUseCase
{
    public async Task<SettlementDetailDto> ExecuteAsync(int settlementId, CancellationToken cancellationToken = default)
    {
        var settlement = await SettlementLookup.FindAsync(queries, settlementId, cancellationToken);
        return await SettlementLookup.DetailAsync(queries, settlement, cancellationToken);
    }
}

public interface IListSettlementLinesUseCase
{
    Task<PagedResult<SettlementLineDto>> ExecuteAsync(int settlementId, string? lineType, int page, int pageSize,
        CancellationToken cancellationToken = default);
}

/// <summary>US-080: dòng chi tiết của kỳ (lọc theo loại), sắp theo loại rồi thời điểm phát sinh.</summary>
public sealed class ListSettlementLinesUseCase(ISettlementQueries queries) : IListSettlementLinesUseCase
{
    public async Task<PagedResult<SettlementLineDto>> ExecuteAsync(int settlementId, string? lineType, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        SettlementReviewRules.EnsurePaging(page, pageSize);
        var type = SettlementReviewRules.ParseEnum<SettlementLineType>(lineType, "lineType");
        await SettlementLookup.FindAsync(queries, settlementId, cancellationToken);
        return await queries.ListLinesAsync(settlementId, type, page, pageSize, cancellationToken);
    }
}

public interface IListSettlementPeriodsUseCase
{
    Task<IReadOnlyList<SettlementPeriodDto>> ExecuteAsync(int take, CancellationToken cancellationToken = default);
}

/// <summary>US-080: lịch kỳ gần nhất (chọn kỳ khi Admin sinh kỳ / lọc danh sách).</summary>
public sealed class ListSettlementPeriodsUseCase(ISettlementQueries queries, TimeProvider timeProvider) : IListSettlementPeriodsUseCase
{
    public const int MaxTake = 104;

    public Task<IReadOnlyList<SettlementPeriodDto>> ExecuteAsync(int take, CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > MaxTake) throw new ValidationException($"take phải trong khoảng 1–{MaxTake}.");
        return queries.ListPeriodsAsync(take, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}

public interface IExportSettlementsUseCase
{
    Task<SettlementCsvFile> ExecuteAsync(SettlementListQuery query, CancellationToken cancellationToken = default);
}

/// <summary>US-080: xuất CSV danh sách kỳ (cùng bộ lọc), tối đa 10.000 dòng – nhiều hơn → 400 (lọc hẹp lại).</summary>
public sealed class ExportSettlementsUseCase(ISettlementQueries queries) : IExportSettlementsUseCase
{
    public async Task<SettlementCsvFile> ExecuteAsync(SettlementListQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await queries.ListAllAsync(query.ToFilter(), SettlementCsvFile.MaxRows + 1, cancellationToken);
        if (rows.Count > SettlementCsvFile.MaxRows)
            throw new ValidationException($"Quá {SettlementCsvFile.MaxRows:N0} kỳ – hãy lọc hẹp hơn trước khi xuất.");
        var suffix = query.PeriodStart is { } start ? start.ToString("yyyyMMdd") : "all";
        return new SettlementCsvFile($"settlements-{suffix}.csv", SettlementCsvWriter.Settlements(rows));
    }
}

public interface IExportSettlementLinesUseCase
{
    Task<SettlementCsvFile> ExecuteAsync(int settlementId, CancellationToken cancellationToken = default);
}

/// <summary>US-080: xuất CSV dòng chi tiết của một kỳ (bảng đối soát).</summary>
public sealed class ExportSettlementLinesUseCase(ISettlementQueries queries) : IExportSettlementLinesUseCase
{
    public async Task<SettlementCsvFile> ExecuteAsync(int settlementId, CancellationToken cancellationToken = default)
    {
        var settlement = await SettlementLookup.FindAsync(queries, settlementId, cancellationToken);
        return await SettlementLookup.LinesCsvAsync(queries, settlement, cancellationToken);
    }
}

/// <summary>Đọc kỳ + dựng chi tiết / CSV dùng chung cho Admin và chủ bãi.</summary>
internal static class SettlementLookup
{
    public static async Task<Settlement> FindAsync(ISettlementQueries queries, int settlementId, CancellationToken cancellationToken)
    {
        if (settlementId <= 0) throw new ValidationException("id phải là số nguyên dương.");
        return await queries.FindAsync(settlementId, cancellationToken) ?? throw new NotFoundException("Kỳ quyết toán", settlementId);
    }

    public static async Task<SettlementDetailDto> DetailAsync(ISettlementQueries queries, Settlement s, CancellationToken cancellationToken)
    {
        var totals = await queries.SumLinesByTypeAsync(s.Id, cancellationToken);
        var adjustments = await queries.ListAdjustmentsAsync(new AdjustmentListFilter(SettlementId: s.Id), 1, SettlementReviewRules.MaxPageSize,
            cancellationToken);
        return new SettlementDetailDto(SettlementReviewRules.Summary(s), SettlementReviewRules.Formula(s), SettlementReviewRules.Timeline(s), totals,
            adjustments.Items, s.AllowedActions());
    }

    public static async Task<SettlementCsvFile> LinesCsvAsync(ISettlementQueries queries, Settlement s, CancellationToken cancellationToken)
    {
        var lines = await queries.ListAllLinesAsync(s.Id, SettlementCsvFile.MaxRows + 1, cancellationToken);
        if (lines.Count > SettlementCsvFile.MaxRows)
            throw new ValidationException($"Kỳ {s.Code} có hơn {SettlementCsvFile.MaxRows:N0} dòng – hãy xem theo trang.");
        return new SettlementCsvFile($"{s.Code}.csv", SettlementCsvWriter.Lines(s.Code, lines));
    }
}

// ======================= CHUYỂN TRẠNG THÁI (Admin) =======================

/// <summary>Đọc kỳ (theo dõi) → chuyển trạng thái ở domain → lưu cùng audit, RowVersion phải khớp bản Admin đã đọc.</summary>
internal static class SettlementTransition
{
    public static async Task<SettlementSummaryDto> RunAsync(ISettlementReviewRepository repository, TimeProvider timeProvider, int settlementId,
        string? rowVersion, int? adminUserId, string action, Action<Settlement, int, DateTime> transition, CancellationToken cancellationToken)
    {
        if (adminUserId is not > 0) throw new AuthenticationException("Không xác định được Admin từ token.");
        if (settlementId <= 0) throw new ValidationException("id phải là số nguyên dương.");
        var expected = RegenerateSettlementUseCase.ParseRowVersion(rowVersion);

        var settlement = await repository.FindTrackedAsync(settlementId, cancellationToken)
            ?? throw new NotFoundException("Kỳ quyết toán", settlementId);
        if (!expected.AsSpan().SequenceEqual(settlement.RowVersion))
            throw new ConflictException("Dữ liệu kỳ quyết toán đã thay đổi, hãy tải lại.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var before = new { Status = settlement.Status.ToString() };
        transition(settlement, adminUserId.Value, now);
        await repository.SaveAsync(settlement, expected, SettlementBuilder.Audit(settlement, action, adminUserId, before, now), cancellationToken);
        return SettlementReviewRules.Summary(settlement);
    }
}

public interface ISubmitSettlementUseCase
{
    Task<SettlementSummaryDto> ExecuteAsync(int settlementId, string? rowVersion, int? adminUserId, CancellationToken cancellationToken = default);
}

/// <summary>US-080: Draft → PendingReview (kỳ bị chặn → 409).</summary>
public sealed class SubmitSettlementUseCase(ISettlementReviewRepository repository, TimeProvider timeProvider) : ISubmitSettlementUseCase
{
    public Task<SettlementSummaryDto> ExecuteAsync(int settlementId, string? rowVersion, int? adminUserId, CancellationToken cancellationToken = default)
        => SettlementTransition.RunAsync(repository, timeProvider, settlementId, rowVersion, adminUserId, "Submitted",
            (s, user, now) => s.Submit(user, now), cancellationToken);
}

public interface IApproveSettlementUseCase
{
    Task<SettlementSummaryDto> ExecuteAsync(int settlementId, string? rowVersion, int? adminUserId, CancellationToken cancellationToken = default);
}

/// <summary>US-080 (AC3): PendingReview → Approved – chốt kỳ, từ đây chỉ sửa qua FinancialAdjustment. Người gửi không tự duyệt (4 mắt) → 403.</summary>
public sealed class ApproveSettlementUseCase(ISettlementReviewRepository repository, SettlementReviewOptions options, TimeProvider timeProvider)
    : IApproveSettlementUseCase
{
    public Task<SettlementSummaryDto> ExecuteAsync(int settlementId, string? rowVersion, int? adminUserId, CancellationToken cancellationToken = default)
        => SettlementTransition.RunAsync(repository, timeProvider, settlementId, rowVersion, adminUserId, "Approved",
            (s, user, now) => s.Approve(user, now, options.RequireDifferentApprover), cancellationToken);
}

public interface IRejectSettlementUseCase
{
    Task<SettlementSummaryDto> ExecuteAsync(int settlementId, string? rowVersion, string? reason, int? adminUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-080: PendingReview → Draft kèm lý do (để tính lại / xử lý rồi gửi lại).</summary>
public sealed class RejectSettlementUseCase(ISettlementReviewRepository repository, TimeProvider timeProvider) : IRejectSettlementUseCase
{
    public Task<SettlementSummaryDto> ExecuteAsync(int settlementId, string? rowVersion, string? reason, int? adminUserId,
        CancellationToken cancellationToken = default)
        => SettlementTransition.RunAsync(repository, timeProvider, settlementId, rowVersion, adminUserId, "Rejected",
            (s, user, now) => s.Reject(user, reason, now), cancellationToken);
}

public interface IMarkSettlementPaidUseCase
{
    Task<SettlementSummaryDto> ExecuteAsync(int settlementId, string? rowVersion, string? payoutReference, DateTime? paidAtUtc, int? adminUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-080 (UC-43 "xuất bảng payout"): Approved → Paid sau khi chuyển khoản – mã giao dịch ngân hàng bắt buộc, ngày chuyển hợp lệ.</summary>
public sealed class MarkSettlementPaidUseCase(ISettlementReviewRepository repository, TimeProvider timeProvider) : IMarkSettlementPaidUseCase
{
    public Task<SettlementSummaryDto> ExecuteAsync(int settlementId, string? rowVersion, string? payoutReference, DateTime? paidAtUtc,
        int? adminUserId, CancellationToken cancellationToken = default)
        => SettlementTransition.RunAsync(repository, timeProvider, settlementId, rowVersion, adminUserId, "Paid",
            (s, user, now) => s.MarkPaid(user, payoutReference, ToUtc(paidAtUtc), now), cancellationToken);

    private static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Unspecified } v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
        { } v => v.ToUniversalTime(),
    };
}

// ======================= BÚT TOÁN ĐIỀU CHỈNH (Admin) =======================

public sealed record AdjustmentListQuery(int? OwnerProfileId, string? Status, bool? Attached, string? Search);

public interface IListAdjustmentsUseCase
{
    Task<PagedResult<FinancialAdjustmentDto>> ExecuteAsync(AdjustmentListQuery query, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>US-080: danh sách bút toán (lọc chủ bãi / trạng thái duyệt / đã gắn kỳ hay chưa, tìm theo lý do / mã nguồn), mới nhất trước.</summary>
public sealed class ListAdjustmentsUseCase(ISettlementQueries queries) : IListAdjustmentsUseCase
{
    public Task<PagedResult<FinancialAdjustmentDto>> ExecuteAsync(AdjustmentListQuery query, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        SettlementReviewRules.EnsurePaging(page, pageSize);
        if (query.OwnerProfileId is <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        return queries.ListAdjustmentsAsync(new AdjustmentListFilter(query.OwnerProfileId,
            SettlementReviewRules.ParseEnum<FinancialAdjustmentStatus>(query.Status, "status"), query.Attached,
            SettlementReviewRules.NormalizeSearch(query.Search)), page, pageSize, cancellationToken);
    }
}

/// <summary>ReversesAdjustmentId: bút toán đã duyệt cần đảo (Amount phải ngược dấu đúng bằng nó).</summary>
public sealed record CreateAdjustmentCommand(int? OwnerProfileId, decimal? Amount, string? Reason, int? SanctionId, int? ReversesAdjustmentId);

public interface ICreateAdjustmentUseCase
{
    Task<FinancialAdjustmentDto> ExecuteAsync(CreateAdjustmentCommand command, int? adminUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-080 (AC3): Admin tạo bút toán điều chỉnh (refund, chargeback, sửa hoa hồng, bồi thường, phạt…) – chờ một Admin khác duyệt.
/// Chỉ thêm: không sửa / xóa; đảo bằng bút toán ngược. Bút toán đã đảo rồi → 409.
/// </summary>
public sealed class CreateAdjustmentUseCase(ISettlementReviewRepository repository, TimeProvider timeProvider) : ICreateAdjustmentUseCase
{
    public async Task<FinancialAdjustmentDto> ExecuteAsync(CreateAdjustmentCommand command, int? adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (adminUserId is not > 0) throw new AuthenticationException("Không xác định được Admin từ token.");
        if (command.OwnerProfileId is not { } owner) throw new ValidationException("ownerProfileId không được để trống.");
        if (command.Amount is not { } amount) throw new ValidationException("amount không được để trống.");

        FinancialAdjustment? reverses = null;
        if (command.ReversesAdjustmentId is { } reversesId)
        {
            if (reversesId <= 0) throw new ValidationException("reversesAdjustmentId phải là số nguyên dương.");
            reverses = await repository.FindAdjustmentTrackedAsync(reversesId, cancellationToken)
                ?? throw new NotFoundException("Bút toán điều chỉnh", reversesId);
            if (await repository.IsReversedAsync(reversesId, cancellationToken))
                throw new ConflictException($"Bút toán #{reversesId} đã có bút toán đảo.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var adjustment = FinancialAdjustment.CreateManual(owner, amount, command.Reason, adminUserId.Value, command.SanctionId, reverses, now);
        await repository.AddAdjustmentAsync(adjustment,
            saved => SettlementReviewRules.AdjustmentAudit(saved, "Created", adminUserId.Value, null, now), cancellationToken);
        return SettlementReviewRules.Adjustment(adjustment);
    }
}

public interface IApproveAdjustmentUseCase
{
    Task<FinancialAdjustmentDto> ExecuteAsync(int adjustmentId, string? rowVersion, int? adminUserId, CancellationToken cancellationToken = default);
}

/// <summary>US-080 (AC3 "người duyệt"): PendingApproval → Approved; người tạo không tự duyệt (4 mắt) → 403. Gắn vào kỳ Draft lần sinh / tính lại kế tiếp.</summary>
public sealed class ApproveAdjustmentUseCase(ISettlementReviewRepository repository, SettlementReviewOptions options, TimeProvider timeProvider)
    : IApproveAdjustmentUseCase
{
    public Task<FinancialAdjustmentDto> ExecuteAsync(int adjustmentId, string? rowVersion, int? adminUserId,
        CancellationToken cancellationToken = default)
        => AdjustmentTransition.RunAsync(repository, timeProvider, adjustmentId, rowVersion, adminUserId, "Approved",
            (a, user, now) => a.Approve(user, now, options.RequireDifferentApprover), cancellationToken);
}

public interface IRejectAdjustmentUseCase
{
    Task<FinancialAdjustmentDto> ExecuteAsync(int adjustmentId, string? rowVersion, string? reason, int? adminUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-080: PendingApproval → Rejected kèm lý do (bút toán không bao giờ gắn vào kỳ).</summary>
public sealed class RejectAdjustmentUseCase(ISettlementReviewRepository repository, TimeProvider timeProvider) : IRejectAdjustmentUseCase
{
    public Task<FinancialAdjustmentDto> ExecuteAsync(int adjustmentId, string? rowVersion, string? reason, int? adminUserId,
        CancellationToken cancellationToken = default)
        => AdjustmentTransition.RunAsync(repository, timeProvider, adjustmentId, rowVersion, adminUserId, "Rejected",
            (a, user, now) => a.Reject(user, reason, now), cancellationToken);
}

internal static class AdjustmentTransition
{
    public static async Task<FinancialAdjustmentDto> RunAsync(ISettlementReviewRepository repository, TimeProvider timeProvider, int adjustmentId,
        string? rowVersion, int? adminUserId, string action, Action<FinancialAdjustment, int, DateTime> transition,
        CancellationToken cancellationToken)
    {
        if (adminUserId is not > 0) throw new AuthenticationException("Không xác định được Admin từ token.");
        if (adjustmentId <= 0) throw new ValidationException("id phải là số nguyên dương.");
        var expected = RegenerateSettlementUseCase.ParseRowVersion(rowVersion);

        var adjustment = await repository.FindAdjustmentTrackedAsync(adjustmentId, cancellationToken)
            ?? throw new NotFoundException("Bút toán điều chỉnh", adjustmentId);
        if (!expected.AsSpan().SequenceEqual(adjustment.RowVersion))
            throw new ConflictException("Dữ liệu bút toán đã thay đổi, hãy tải lại.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var before = new { Status = adjustment.Status.ToString() };
        transition(adjustment, adminUserId.Value, now);
        await repository.SaveAdjustmentAsync(adjustment, expected,
            SettlementReviewRules.AdjustmentAudit(adjustment, action, adminUserId.Value, before, now), cancellationToken);
        return SettlementReviewRules.Adjustment(adjustment);
    }
}
