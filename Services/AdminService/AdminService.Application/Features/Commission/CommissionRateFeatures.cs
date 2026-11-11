using System.Globalization;
using AdminService.Application.Features.Dashboard;
using AdminService.Application.Features.Settings;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Commission;

// ===== DTO – endpoint nội bộ (PaymentService gọi) =====

/// <summary>Một yêu cầu giải tỷ lệ: bãi + thời điểm giao dịch (UTC, thường là Payment.PaidAtUtc).</summary>
public sealed record ResolveCommissionRateItem(int? ParkingLotId, DateTime? AtUtc);

/// <summary>POST /internal/v1/commission-rates/resolve – 1 đến <see cref="ResolveCommissionRatesUseCase.MaxItems"/> mục.</summary>
public sealed record ResolveCommissionRatesRequest(IReadOnlyList<ResolveCommissionRateItem>? Items);

/// <summary>
/// Tỷ lệ áp dụng cho một mục, cùng thứ tự với request. Rate là PHÂN SỐ 0–1 (hợp đồng 7,5% → 0.075).
/// Source = LotContract | DefaultConfig; Reason = null | BeforeEffective | AfterExpiry | NoSignedContract.
/// ContractNumber / ContractVersion / EffectiveFrom / EffectiveTo = hợp đồng đã ký của bãi (có cả khi ngoài hiệu lực), null nếu không có.
/// </summary>
public sealed record ResolvedCommissionRateDto(int ParkingLotId, DateTime AtUtc, decimal Rate, string Source, string? Reason,
    string? ContractNumber, int? ContractVersion, DateOnly? EffectiveFrom, DateOnly? EffectiveTo);

public sealed record ResolveCommissionRatesResponse(IReadOnlyList<ResolvedCommissionRateDto> Items);

// ===== DTO – danh sách tỷ lệ cho Admin portal =====

/// <summary>
/// Tỷ lệ hiện tại của một bãi. CurrentRate là PHÂN SỐ. Bãi chưa có hợp đồng đã ký: OwnerProfileId / OwnerName = null
/// (ParkingService lots-summary không có chủ bãi). LotStatus = null khi ParkingService không trả về bãi này.
/// </summary>
public sealed record CommissionLotRateDto(int ParkingLotId, string LotName, string? District, string? City, string? LotStatus,
    int? OwnerProfileId, string? OwnerName, decimal CurrentRate, string Source, string? Reason, int? ContractId, string? ContractNumber,
    int? ContractVersion, DateOnly? EffectiveFrom, DateOnly? EffectiveTo);

/// <summary>Thay đổi DEFAULT_COMMISSION_RATE đã lên lịch (sớm nhất, chưa hủy, chưa hiệu lực). Rate là PHÂN SỐ.</summary>
public sealed record CommissionDefaultChangeDto(decimal Rate, DateTime EffectiveFromUtc);

/// <summary>
/// GET /api/v1/admin/commission-rates. DefaultRate = tỷ lệ mặc định đang hiệu lực (PHÂN SỐ), DefaultEffectiveFromUtc = null khi vẫn là
/// giá trị gốc. Partial = true khi không đọc được ParkingService: chỉ còn các bãi có hợp đồng đã ký (tên / quận lấy từ hợp đồng).
/// </summary>
public sealed record CommissionRateListDto(DateTime AsOfUtc, decimal DefaultRate, DateTime? DefaultEffectiveFromUtc,
    CommissionDefaultChangeDto? DefaultNextChange, bool Partial, string? PartialReason, PagedResult<CommissionLotRateDto> Lots);

/// <summary>Tham số nhận từ query string (chưa kiểm tra). Source = LotContract | DefaultConfig, bỏ trống = tất cả.</summary>
public sealed record CommissionRateListQuery(string? Search = null, string? Source = null, int Page = 1, int PageSize = 20);

// ===== PORT =====

/// <summary>PORT đọc (CQRS-lite): hợp đồng bãi đã ký (AsNoTracking + Select ở database).</summary>
public interface ICommissionRateQueries
{
    /// <summary>Phiên bản đã ký của các hợp đồng Signed; null = mọi bãi.</summary>
    Task<IReadOnlyList<SignedLotContractRate>> ListSignedContractsAsync(IReadOnlyCollection<int>? parkingLotIds,
        CancellationToken cancellationToken);
}

internal static class CommissionDefaultRate
{
    /// <summary>Đọc DEFAULT_COMMISSION_RATE kèm lịch sử thay đổi; thiếu tham số → 503 (không đoán tỷ lệ).</summary>
    public static async Task<SystemConfigRecord> LoadAsync(ISettingsQueries settings, DateTime changesUpToUtc,
        CancellationToken cancellationToken)
        => await settings.FindConfigByKeyAsync(CommissionRateResolver.DefaultRateKey, changesUpToUtc, cancellationToken)
           ?? throw new DependencyUnavailableException($"Chưa có tham số {CommissionRateResolver.DefaultRateKey}, chưa thể tính hoa hồng.");

    public static decimal At(SystemConfigRecord config, DateTime atUtc)
        => CommissionRateResolver.ParseDefaultRate(SystemConfigValueResolver.Resolve(config.DefaultValue, config.Changes, atUtc).Value);
}

// ===== USE CASE: giải tỷ lệ cho PaymentService =====

public interface IResolveCommissionRatesUseCase
{
    Task<ResolveCommissionRatesResponse> ExecuteAsync(string callerService, ResolveCommissionRatesRequest? request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-079: PaymentService (job tính hoa hồng) hỏi tỷ lệ cho từng (bãi, thời điểm giao dịch). Chỉ service PaymentService được gọi.
/// Mặc định giải đúng thời điểm (thay đổi tham số có thể hiệu lực giữa ngày), hợp đồng giải theo ngày giờ Việt Nam.
/// Thời điểm ở tương lai bị từ chối: kết quả cho thời điểm đã qua không bao giờ đổi nên phía gọi lưu một lần là đủ.
/// Giao dịch ngoài thời gian hiệu lực của hợp đồng → tỷ lệ mặc định + log cảnh báo.
/// </summary>
public sealed class ResolveCommissionRatesUseCase(ICommissionRateQueries queries, ISettingsQueries settings, TimeProvider timeProvider,
    ILogger<ResolveCommissionRatesUseCase> logger) : IResolveCommissionRatesUseCase
{
    public const string AllowedCaller = "PaymentService";
    public const int MaxItems = 200;
    /// <summary>Cho phép lệch đồng hồ giữa hai service.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public async Task<ResolveCommissionRatesResponse> ExecuteAsync(string callerService, ResolveCommissionRatesRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(callerService, AllowedCaller, StringComparison.Ordinal))
            throw new ForbiddenException($"Service {callerService} không được tra cứu tỷ lệ hoa hồng.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var items = Validate(request, now);

        var contracts = await queries.ListSignedContractsAsync(items.Select(i => i.ParkingLotId).Distinct().ToList(), cancellationToken);
        var byLot = contracts.ToLookup(c => c.ParkingLotId);
        var defaultConfig = await CommissionDefaultRate.LoadAsync(settings, now, cancellationToken);

        var warned = new HashSet<(int, string)>();
        var results = new List<ResolvedCommissionRateDto>(items.Count);
        foreach (var (lotId, atUtc) in items)
        {
            var resolved = CommissionRateResolver.Resolve(byLot[lotId], CommissionDefaultRate.At(defaultConfig, atUtc), atUtc);
            LogAnomaly(lotId, atUtc, resolved, warned);
            results.Add(new ResolvedCommissionRateDto(lotId, atUtc, resolved.Rate, resolved.Source, resolved.Reason,
                resolved.Contract?.ContractNumber, resolved.Contract?.Version, resolved.EffectiveFrom, resolved.EffectiveTo));
        }
        return new ResolveCommissionRatesResponse(results);
    }

    private static List<(int ParkingLotId, DateTime AtUtc)> Validate(ResolveCommissionRatesRequest? request, DateTime nowUtc)
    {
        if (request?.Items is not { Count: > 0 } items) throw new ValidationException("items không được để trống.");
        if (items.Count > MaxItems) throw new ValidationException($"Tối đa {MaxItems} mục mỗi lần.");

        var result = new List<(int, DateTime)>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i] ?? throw new ValidationException($"items[{i}] không được để trống.");
            if (item.ParkingLotId is not > 0) throw new ValidationException($"items[{i}].parkingLotId phải là số nguyên dương.");
            if (UtcDateTime.ToUtc(item.AtUtc) is not { } atUtc || atUtc == default)
                throw new ValidationException($"items[{i}].atUtc không được để trống.");
            if (atUtc > nowUtc + ClockSkew) throw new ValidationException($"items[{i}].atUtc không được ở tương lai.");
            result.Add((item.ParkingLotId.Value, atUtc));
        }
        return result;
    }

    private void LogAnomaly(int lotId, DateTime atUtc, ResolvedCommissionRate resolved, HashSet<(int, string)> warned)
    {
        if (resolved.OverlappingContracts && warned.Add((lotId, "Overlap")))
            logger.LogWarning("US-079: bãi {ParkingLotId} có nhiều hợp đồng đã ký cùng hiệu lực tại {AtUtc:o}; dùng hợp đồng {ContractNumber} (ký sau cùng).",
                lotId, atUtc, resolved.Contract?.ContractNumber);
        if (resolved.Reason is CommissionRateReasons.BeforeEffective or CommissionRateReasons.AfterExpiry && warned.Add((lotId, resolved.Reason)))
            logger.LogWarning("US-079: giao dịch lúc {AtUtc:o} của bãi {ParkingLotId} nằm ngoài hiệu lực hợp đồng {ContractNumber} ({Reason}); dùng tỷ lệ mặc định {Rate}.",
                atUtc, lotId, resolved.Contract?.ContractNumber, resolved.Reason, resolved.Rate);
    }
}

// ===== USE CASE: danh sách tỷ lệ cho Admin portal =====

public interface IListCommissionRatesUseCase
{
    Task<CommissionRateListDto> ExecuteAsync(CommissionRateListQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-079: tỷ lệ hoa hồng hiện tại của từng bãi cho trang Hoa hồng nền tảng. Danh sách bãi = bãi của ParkingService (lots-summary, trừ bãi
/// chờ duyệt KYB) ∪ bãi có hợp đồng đã ký. ParkingService lỗi / timeout → vẫn 200 với các bãi có hợp đồng, Partial = true.
/// Tìm kiếm không phân biệt hoa thường / dấu: tên bãi, quận, thành phố, chủ bãi, số hợp đồng, mã bãi.
/// </summary>
public sealed class ListCommissionRatesUseCase(ICommissionRateQueries queries, ISettingsQueries settings,
    IParkingDashboardClient parkingLots, TimeProvider timeProvider, ILogger<ListCommissionRatesUseCase> logger,
    DashboardSourceTimeouts? timeouts = null) : IListCommissionRatesUseCase
{
    private const string Parking = "ParkingService";
    /// <summary>Mốc xa để lấy cả thay đổi đã lên lịch (UTC – Npgsql không nhận DateTime.MaxValue không ghi múi giờ).</summary>
    private static readonly DateTime FarFutureUtc = new(9000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly DashboardSourceTimeouts sourceTimeouts = timeouts ?? new DashboardSourceTimeouts();

    public async Task<CommissionRateListDto> ExecuteAsync(CommissionRateListQuery query, CancellationToken cancellationToken = default)
    {
        var (term, source) = Validate(query);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var lotsTask = FetchLotsAsync(cancellationToken);
        var contracts = await queries.ListSignedContractsAsync(null, cancellationToken);
        var defaultConfig = await CommissionDefaultRate.LoadAsync(settings, FarFutureUtc, cancellationToken);
        var lots = await lotsTask;

        var defaultEffective = SystemConfigValueResolver.Resolve(defaultConfig.DefaultValue, defaultConfig.Changes, now);
        var defaultRate = CommissionRateResolver.ParseDefaultRate(defaultEffective.Value);
        var nextChange = defaultConfig.Changes
            .Where(c => c.CancelledAtUtc is null && c.EffectiveFromUtc > now)
            .OrderBy(c => c.EffectiveFromUtc).ThenBy(c => c.Id)
            .Select(c => new CommissionDefaultChangeDto(CommissionRateResolver.ParseDefaultRate(c.Value), c.EffectiveFromUtc))
            .FirstOrDefault();

        var byLot = contracts.ToLookup(c => c.ParkingLotId);
        var lotInfo = new Dictionary<int, ParkingLotSource>();
        foreach (var lot in lots.Value?.Lots ?? [])
            if (lot.Status != "PendingApproval") lotInfo.TryAdd(lot.ParkingLotId, lot);

        var rows = lotInfo.Keys.Union(contracts.Select(c => c.ParkingLotId))
            .Select(lotId => Row(lotId, lotInfo.GetValueOrDefault(lotId), byLot[lotId], defaultRate, now))
            .Where(r => source is null || r.Source == source)
            .Where(r => term is null || Matches(r, term))
            .OrderBy(r => r.LotName, StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: true))
            .ThenBy(r => r.ParkingLotId)
            .ToList();

        var page = rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        var partial = lots.Outcome != SourceOutcome.Ok;
        return new CommissionRateListDto(now, defaultRate, defaultEffective.EffectiveFromUtc, nextChange, partial,
            partial ? lots.Detail ?? $"Không đọc được danh sách bãi từ {Parking}." : null,
            new PagedResult<CommissionLotRateDto>(page, query.Page, query.PageSize, rows.Count));
    }

    private static (string? Term, string? Source) Validate(CommissionRateListQuery query)
    {
        if (query.Page < 1) throw new ValidationException("page phải ≥ 1.");
        if (query.PageSize is < 1 or > 100) throw new ValidationException("pageSize phải trong khoảng 1–100.");
        var term = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        if (term is { Length: > 200 }) throw new ValidationException("Từ khóa tìm kiếm tối đa 200 ký tự.");

        string? source = null;
        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            source = CommissionRateSources.All.FirstOrDefault(s => string.Equals(s, query.Source.Trim(), StringComparison.OrdinalIgnoreCase))
                     ?? throw new ValidationException($"source phải là một trong: {string.Join(", ", CommissionRateSources.All)}.");
        }
        return (term, source);
    }

    private static CommissionLotRateDto Row(int lotId, ParkingLotSource? lot, IEnumerable<SignedLotContractRate> contracts, decimal defaultRate,
        DateTime nowUtc)
    {
        var resolved = CommissionRateResolver.Resolve(contracts, defaultRate, nowUtc);
        var contract = resolved.Contract;
        return new CommissionLotRateDto(lotId,
            lot?.Name ?? contract?.LotName ?? $"Bãi #{lotId}",
            lot is not null ? lot.District : contract?.LotDistrict,
            lot?.City,
            lot?.Status,
            contract?.OwnerProfileId, contract?.BusinessName,
            resolved.Rate, resolved.Source, resolved.Reason,
            contract?.ContractId, contract?.ContractNumber, contract?.Version, resolved.EffectiveFrom, resolved.EffectiveTo);
    }

    private static bool Matches(CommissionLotRateDto row, string term)
    {
        var compare = CultureInfo.InvariantCulture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        string?[] fields = [row.LotName, row.District, row.City, row.OwnerName, row.ContractNumber, row.ParkingLotId.ToString(CultureInfo.InvariantCulture)];
        var plain = Fold(term.TrimStart('#'));
        return plain.Length > 0 && fields.Any(f => f is not null && compare.IndexOf(Fold(f), plain, options) >= 0);
    }

    /// <summary>"đ" không tách dấu được (IgnoreNonSpace không bỏ) → đổi thành "d" để "ba dinh" khớp "Ba Đình".</summary>
    private static string Fold(string text) => text.Replace('đ', 'd').Replace('Đ', 'D');

    private async Task<SourceResult<ParkingLotsSource>> FetchLotsAsync(CancellationToken cancellationToken)
    {
        var timeout = sourceTimeouts.For(Parking);
        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            var result = await parkingLots.GetLotsSummaryAsync(linked.Token);
            if (result.Outcome != SourceOutcome.Ok)
                logger.LogWarning("US-079: danh sách bãi từ {Service} không dùng được ({Outcome}: {Detail}).", Parking, result.Outcome, result.Detail);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("US-079: {Service} không phản hồi trong {Seconds} giây.", Parking, timeout.TotalSeconds);
            return SourceResult<ParkingLotsSource>.Error($"{Parking} không phản hồi kịp ({timeout.TotalSeconds:0.#} giây).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "US-079: lỗi khi gọi {Service}.", Parking);
            return SourceResult<ParkingLotsSource>.Error($"Không gọi được {Parking}.");
        }
    }
}
