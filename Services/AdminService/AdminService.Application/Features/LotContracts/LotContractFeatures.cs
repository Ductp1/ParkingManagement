using System.Globalization;
using System.Text.Json;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using AdminService.Domain.ValueObjects;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.LotContracts;

// ===== REQUEST (người thao tác lấy từ JWT) =====

/// <summary>L1 / L1p: lập hợp đồng cho bãi có KYB đã duyệt. CommissionRate theo % (7.5 = 7,5%).</summary>
public sealed record CreateLotContractRequest(int? KybApplicationId, decimal? CommissionRate, DateOnly? EffectiveFrom, int? TermMonths);

/// <summary>L2p: xem trước phiên bản sửa.</summary>
public sealed record LotContractTermsRequest(decimal? CommissionRate, DateOnly? EffectiveFrom, int? TermMonths);

/// <summary>L2: điều khoản mới + lý do sửa.</summary>
public sealed record ReviseLotContractRequest(decimal? CommissionRate, DateOnly? EffectiveFrom, int? TermMonths, string? Reason);

/// <summary>L3 (multipart): bản scan đã ký + phiên bản Admin xác nhận đã ký.</summary>
public sealed record SignLotContractRequest(int? ConfirmedVersion, UploadedFile? SignedFile);

/// <summary>L4.</summary>
public sealed record LotContractReasonRequest(string? Reason);

// ===== Dùng chung =====

/// <summary>Audit log thao tác Admin trên hợp đồng bãi. EntityId = số hợp đồng (có trước khi lưu, kể cả lúc lập mới).</summary>
public static class LotContractAudit
{
    public const string EntityName = nameof(LotContract);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static object Snapshot(LotContract c) => new
    {
        status = c.Status.ToString(),
        parkingLotId = c.ParkingLotId,
        currentVersion = c.CurrentVersion?.Version,
        commissionRate = c.CurrentVersion?.CommissionRate,
    };

    public static AuditLog Build(LotContract c, string action, int adminUserId, object? before, string? reason) => new()
    {
        SourceService = "AdminService",
        UserId = adminUserId,
        OwnerProfileId = c.OwnerProfileId,
        Action = $"{EntityName}.{action}",
        EntityName = EntityName,
        EntityId = c.ContractNumber,
        OldValuesJson = before is null ? null : JsonSerializer.Serialize(before, Json),
        NewValuesJson = JsonSerializer.Serialize(Snapshot(c), Json),
        Reason = reason,
    };
}

/// <summary>Các bước dùng chung: số hợp đồng, khóa file, giá trị placeholder, sinh + lưu file, lưu hoặc dọn file.</summary>
internal static class LotContractSupport
{
    private static readonly string[] TermWords = ["", "", "", "", "", "", "", "", "", "", "", "",
        "mười hai", "", "", "", "", "", "", "", "", "", "", "", "hai mươi bốn", "", "", "", "", "", "", "", "", "", "", "", "ba mươi sáu"];

    public static void EnsureIds(int id, int adminUserId)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        if (adminUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
    }

    /// <summary>HD-SP-2026-0003; lập lại cho cùng bãi sau khi hủy: HD-SP-2026-0003-2, -3…</summary>
    public static string ContractNumber(int year, int parkingLotId, int existingCount)
        => existingCount == 0 ? $"HD-SP-{year}-{parkingLotId:0000}" : $"HD-SP-{year}-{parkingLotId:0000}-{existingCount + 1}";

    public static string ContractKey(string contractNumber, int version, string extension)
        => $"lot-contracts/{contractNumber.ToLowerInvariant()}/contract-v{version}{extension}";

    public static string SignedScanKey(string contractNumber, int version, string extension)
        => $"lot-contracts/{contractNumber.ToLowerInvariant()}/signed-v{version}{extension}";

    /// <summary>"05:00 – 23:00"; mở cả ngày → "24/7".</summary>
    public static string OpeningHours(TimeOnly open, TimeOnly close)
        => open == close || (open == TimeOnly.MinValue && close >= new TimeOnly(23, 59))
            ? "24/7"
            : $"{open:HH\\:mm} – {close:HH\\:mm}";

    public static async Task<ContractTemplate> ActiveTemplateAsync(ILotContractRepository repository, CancellationToken ct)
        => await repository.GetActiveTemplateAsync(ct)
           ?? throw new ConflictException("Chưa có mẫu hợp đồng đang áp dụng, chưa thể sinh hợp đồng.");

    public static ContractTerms Terms(decimal? rate, DateOnly? from, int? months, DateTime nowUtc)
        => ContractTerms.Create(rate, from, months, ContractSupport.TodayInVietnam(nowUtc));

    /// <summary>
    /// Kiểm tra và dựng hợp đồng mới trong bộ nhớ (chưa lưu) cho L1p / L1:
    /// KYB tồn tại (404) và đã duyệt, bãi đang chờ duyệt, chưa có hợp đồng mở, chủ bãi có hồ sơ đối tác đã cấp tài khoản (409),
    /// rồi mới kiểm tra điều khoản (400).
    /// </summary>
    public static async Task<(LotContract Contract, ContractTerms Terms, ContractTemplate Template)> PrepareNewAsync(
        CreateLotContractRequest request, int adminUserId, ILotContractRepository repository, IParkingServiceClient parking,
        DateTime nowUtc, CancellationToken ct)
    {
        if (request.KybApplicationId is not { } kybId || kybId <= 0)
            throw new ValidationException("kybApplicationId phải là số nguyên dương.");
        var kyb = await parking.GetKybAsync(kybId, ct) ?? throw new NotFoundException("Hồ sơ KYB", kybId);
        if (kyb.KybStatus != nameof(ParkingManagement.SharedKernel.Enums.KybStatus.Approved))
            throw new ConflictException($"Hồ sơ KYB #{kybId} chưa được duyệt (trạng thái {kyb.KybStatus}), chưa thể lập hợp đồng.");
        if (kyb.LotStatus != nameof(ParkingManagement.SharedKernel.Enums.ParkingLotStatus.PendingApproval))
            throw new ConflictException($"Bãi {kyb.LotName} đang ở trạng thái {kyb.LotStatus}, không phải chờ duyệt – không lập hợp đồng kích hoạt.");
        if (await repository.HasOpenContractAsync(kyb.ParkingLotId, ct))
            throw new ConflictException($"Bãi {kyb.LotName} đã có hợp đồng đang chờ ký hoặc đã ký.");
        var partner = await repository.FindPartnerByOwnerProfileAsync(kyb.OwnerProfileId, ct)
                      ?? throw new ConflictException(
                          $"Chủ bãi (OwnerProfile #{kyb.OwnerProfileId}) chưa có hồ sơ đối tác đã cấp tài khoản, chưa thể lập hợp đồng.");

        var terms = Terms(request.CommissionRate, request.EffectiveFrom, request.TermMonths, nowUtc);
        var template = await ActiveTemplateAsync(repository, ct);
        var number = ContractNumber(nowUtc.Year, kyb.ParkingLotId, await repository.CountForLotAsync(kyb.ParkingLotId, ct));
        var lot = new LotSnapshot(kyb.ParkingLotId, kyb.KybApplicationId, kyb.OwnerProfileId, kyb.LotName, kyb.LotAddress, kyb.LotCity,
            kyb.LotDistrict, kyb.TotalSlots, OpeningHours(kyb.OpenTime, kyb.CloseTime));
        return (LotContract.Open(number, lot, partner, adminUserId), terms, template);
    }

    /// <summary>Giá trị placeholder của mẫu hợp đồng bãi (định dạng kiểu Việt Nam).</summary>
    public static IReadOnlyDictionary<string, string> Values(LotContract c, int version, ContractTerms terms, DateTime nowUtc)
    {
        static string Date(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var today = ContractSupport.TodayInVietnam(nowUtc);
        var previous = c.CurrentVersion;
        var versionNote = version == 1 || previous is null
            ? $"Phiên bản {version}"
            : $"Phiên bản {version} – thay thế phiên bản {previous.Version} ngày {Date(ContractSupport.TodayInVietnam(previous.CreatedAtUtc))}";
        var lotAddress = string.Join(", ", new[] { c.LotAddress, c.LotDistrict, c.LotCity }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
        var words = terms.TermMonths < TermWords.Length ? TermWords[terms.TermMonths] : string.Empty;

        return new Dictionary<string, string>
        {
            ["ContractNumber"] = c.ContractNumber,
            ["Version"] = version.ToString(CultureInfo.InvariantCulture),
            ["VersionNote"] = versionNote,
            ["IssuedDate"] = Date(today),
            ["IssuedDateLong"] = $"ngày {today.Day:00} tháng {today.Month:00} năm {today.Year}",
            ["ApplicationCode"] = c.ApplicationCode,
            ["BusinessName"] = c.BusinessName,
            ["TaxCode"] = c.TaxCode,
            ["BusinessAddress"] = c.BusinessAddress,
            ["LegalRepresentativeName"] = c.LegalRepresentativeName,
            ["LegalRepresentativePosition"] = c.LegalRepresentativePosition ?? "Người đại diện theo pháp luật",
            ["Email"] = c.Email,
            ["Phone"] = c.Phone,
            ["LotName"] = c.LotName,
            ["LotAddress"] = lotAddress,
            ["LotCapacity"] = $"{c.LotTotalSlots.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.')} chỗ",
            ["LotOpeningHours"] = c.LotOpeningHours,
            ["CommissionRate"] = terms.CommissionRate.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ','),
            ["TermMonths"] = terms.TermMonths.ToString(CultureInfo.InvariantCulture),
            ["TermMonthsText"] = words.Length > 0 ? $"{terms.TermMonths} ({words})" : terms.TermMonths.ToString(CultureInfo.InvariantCulture),
            ["EffectiveFrom"] = Date(terms.EffectiveFrom),
            ["EffectiveTo"] = Date(terms.EffectiveTo),
            ["BankBin"] = c.BankBin,
            ["BankName"] = c.BankName,
            ["BankAccountNumber"] = c.BankAccountNumber,
            ["BankAccountName"] = c.BankAccountName,
        };
    }

    public static RenderedContract Render(IContractRenderer renderer, ContractTemplate template, LotContract c, int version,
        ContractTerms terms, DateTime nowUtc, bool preview)
        => renderer.Render(template.Body, template.Version, Values(c, version, terms, nowUtc), preview);

    public static async Task<StoredFile> RenderAndStoreAsync(IContractRenderer renderer, IFileStorage storage, ContractTemplate template,
        LotContract c, ContractTerms terms, DateTime nowUtc, CancellationToken ct)
    {
        var version = c.NextVersion;
        var file = Render(renderer, template, c, version, terms, nowUtc, preview: false);
        var key = ContractKey(c.ContractNumber, version, file.FileExtension);
        await storage.SaveAsync(key, file.Content, ct);
        return new StoredFile(key, file.ContentType, ContractSupport.Sha256Hex(file.Content));
    }

    public static async Task SaveOrDeleteAsync(ILotContractRepository repository, IFileStorage storage, string fileKey, AuditLog auditLog,
        CancellationToken ct)
    {
        try
        {
            await repository.SaveWithAuditAsync(auditLog, ct);
        }
        catch
        {
            await storage.DeleteAsync(fileKey, CancellationToken.None);
            throw;
        }
    }

    public static async Task<LotContract> LoadAsync(ILotContractRepository repository, int id, CancellationToken ct)
        => await repository.FindTrackedAsync(id, ct) ?? throw new NotFoundException("Hợp đồng bãi", id);

    public static async Task<LotContractDetailDto> ReloadAsync(ILotContractQueries queries, int id, CancellationToken ct)
        => await queries.GetDetailAsync(id, ct) ?? throw new NotFoundException("Hợp đồng bãi", id);
}

// ===== USE CASE (đọc) =====

public interface IListLotContractsUseCase
{
    Task<PagedResult<LotContractSummaryDto>> ExecuteAsync(LotContractStatus? status, int? parkingLotId, int? ownerProfileId, string? search,
        int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>US-116 (L0): danh sách hợp đồng bãi.</summary>
public sealed class ListLotContractsUseCase(ILotContractQueries queries) : IListLotContractsUseCase
{
    public Task<PagedResult<LotContractSummaryDto>> ExecuteAsync(LotContractStatus? status, int? parkingLotId, int? ownerProfileId,
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ValidationException("page phải ≥ 1.");
        if (pageSize is < 1 or > 100) throw new ValidationException("pageSize phải trong khoảng 1–100.");
        if (parkingLotId is <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (ownerProfileId is <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (term is { Length: > 200 }) throw new ValidationException("Từ khóa tìm kiếm tối đa 200 ký tự.");
        return queries.ListAsync(status, parkingLotId, ownerProfileId, term, page, pageSize, cancellationToken);
    }
}

public interface IGetLotContractUseCase
{
    Task<LotContractDetailDto> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>US-116 (L0b): chi tiết hợp đồng bãi kèm các phiên bản.</summary>
public sealed class GetLotContractUseCase(ILotContractQueries queries) : IGetLotContractUseCase
{
    public async Task<LotContractDetailDto> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        return await LotContractSupport.ReloadAsync(queries, id, cancellationToken);
    }
}

public interface IGetLotContractFileUseCase
{
    Task<ContractFileResult> ExecuteAsync(int id, int version, CancellationToken cancellationToken = default);
}

/// <summary>US-116 (L2f): tải file một phiên bản; SHA-256 tính lại phải khớp giá trị lưu lúc sinh.</summary>
public sealed class GetLotContractFileUseCase(ILotContractQueries queries, IFileStorage storage) : IGetLotContractFileUseCase
{
    public async Task<ContractFileResult> ExecuteAsync(int id, int version, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        if (version <= 0) throw new ValidationException("version phải là số nguyên dương.");
        var info = await queries.GetVersionFileAsync(id, version, cancellationToken)
                   ?? throw new NotFoundException("Phiên bản hợp đồng", $"{id}/v{version}");
        var content = await storage.ReadAsync(info.FileKey, cancellationToken)
                      ?? throw new NotFoundException("File hợp đồng", $"{info.ContractNumber}/v{version}");
        if (!string.Equals(ContractSupport.Sha256Hex(content), info.FileSha256, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(
                $"File hợp đồng {info.ContractNumber} phiên bản v{version} không còn toàn vẹn (SHA-256 không khớp), vui lòng sinh phiên bản mới.");
        return new ContractFileResult(content, info.FileContentType, $"{info.ContractNumber}-v{version}{Path.GetExtension(info.FileKey)}");
    }
}

// ===== USE CASE (xem trước – không lưu) =====

public interface IPreviewNewLotContractUseCase
{
    Task<ContractFileResult> ExecuteAsync(CreateLotContractRequest request, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>US-116 (L1p): sinh thử hợp đồng v1 cho bãi (có dấu "BẢN XEM TRƯỚC"), không lưu file, không đổi dữ liệu.</summary>
public sealed class PreviewNewLotContractUseCase(ILotContractRepository repository, IParkingServiceClient parking, IContractRenderer renderer,
    TimeProvider timeProvider) : IPreviewNewLotContractUseCase
{
    public async Task<ContractFileResult> ExecuteAsync(CreateLotContractRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var (contract, terms, template) =
            await LotContractSupport.PrepareNewAsync(request, performedByUserId, repository, parking, now, cancellationToken);
        var file = LotContractSupport.Render(renderer, template, contract, 1, terms, now, preview: true);
        return new ContractFileResult(file.Content, file.ContentType, $"{contract.ContractNumber}-v1-preview{file.FileExtension}");
    }
}

public interface IPreviewLotContractRevisionUseCase
{
    Task<ContractFileResult> ExecuteAsync(int id, LotContractTermsRequest request, CancellationToken cancellationToken = default);
}

/// <summary>US-116 (L2p): sinh thử phiên bản v+1 với điều khoản mới, không lưu.</summary>
public sealed class PreviewLotContractRevisionUseCase(ILotContractRepository repository, IContractRenderer renderer, TimeProvider timeProvider)
    : IPreviewLotContractRevisionUseCase
{
    public async Task<ContractFileResult> ExecuteAsync(int id, LotContractTermsRequest request, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        var contract = await LotContractSupport.LoadAsync(repository, id, cancellationToken);
        if (contract.Status != LotContractStatus.Pending)
            throw new ConflictException($"Không thể sửa hợp đồng {contract.ContractNumber} đang ở trạng thái {contract.Status}.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var terms = LotContractSupport.Terms(request.CommissionRate, request.EffectiveFrom, request.TermMonths, now);
        var template = await LotContractSupport.ActiveTemplateAsync(repository, cancellationToken);
        var version = contract.NextVersion;
        var file = LotContractSupport.Render(renderer, template, contract, version, terms, now, preview: true);
        return new ContractFileResult(file.Content, file.ContentType, $"{contract.ContractNumber}-v{version}-preview{file.FileExtension}");
    }
}

// ===== USE CASE (ghi) =====

public interface ICreateLotContractUseCase
{
    Task<LotContractDetailDto> ExecuteAsync(CreateLotContractRequest request, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 (L1): lập hợp đồng v1 cho bãi có KYB đã duyệt. Đọc KYB + bãi từ ParkingService, Bên B từ hồ sơ đối tác,
/// sinh file từ mẫu đang áp dụng + SHA-256. Hợp đồng + phiên bản + AuditLog cùng 1 SaveChanges; lưu lỗi thì xóa file.
/// </summary>
public sealed class CreateLotContractUseCase(ILotContractRepository repository, ILotContractQueries queries, IParkingServiceClient parking,
    IContractRenderer renderer, IFileStorage storage, TimeProvider timeProvider) : ICreateLotContractUseCase
{
    public async Task<LotContractDetailDto> ExecuteAsync(CreateLotContractRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (performedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var (contract, terms, template) =
            await LotContractSupport.PrepareNewAsync(request, performedByUserId, repository, parking, now, cancellationToken);

        var stored = await LotContractSupport.RenderAndStoreAsync(renderer, storage, template, contract, terms, now, cancellationToken);
        contract.AddFirstVersion(performedByUserId, terms, template.Version, stored);
        repository.Add(contract);
        await LotContractSupport.SaveOrDeleteAsync(repository, storage, stored.Key,
            LotContractAudit.Build(contract, "Created", performedByUserId, null, null), cancellationToken);
        return await LotContractSupport.ReloadAsync(queries, contract.Id, cancellationToken);
    }
}

public interface IReviseLotContractUseCase
{
    Task<LotContractDetailDto> ExecuteAsync(int id, ReviseLotContractRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-116 (L2): phiên bản v+1 cùng số hợp đồng, bản đang chờ ký → Superseded; lý do bắt buộc.</summary>
public sealed class ReviseLotContractUseCase(ILotContractRepository repository, ILotContractQueries queries, IContractRenderer renderer,
    IFileStorage storage, TimeProvider timeProvider) : IReviseLotContractUseCase
{
    public async Task<LotContractDetailDto> ExecuteAsync(int id, ReviseLotContractRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        LotContractSupport.EnsureIds(id, performedByUserId);
        var contract = await LotContractSupport.LoadAsync(repository, id, cancellationToken);
        var before = LotContractAudit.Snapshot(contract);
        if (contract.Status != LotContractStatus.Pending)
            throw new ConflictException($"Không thể sửa hợp đồng {contract.ContractNumber} đang ở trạng thái {contract.Status}.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ValidationException("Lý do sửa hợp đồng không được để trống.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var terms = LotContractSupport.Terms(request.CommissionRate, request.EffectiveFrom, request.TermMonths, now);
        var template = await LotContractSupport.ActiveTemplateAsync(repository, cancellationToken);

        var stored = await LotContractSupport.RenderAndStoreAsync(renderer, storage, template, contract, terms, now, cancellationToken);
        var version = contract.Revise(performedByUserId, terms, template.Version, stored, request.Reason);
        await LotContractSupport.SaveOrDeleteAsync(repository, storage, stored.Key,
            LotContractAudit.Build(contract, "Revised", performedByUserId, before, version.RevisionReason), cancellationToken);
        return await LotContractSupport.ReloadAsync(queries, id, cancellationToken);
    }
}

public interface ISignLotContractUseCase
{
    Task<LotContractDetailDto> ExecuteAsync(int id, SignLotContractRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 (L3): Pending → Signed và bãi Active.
/// 1. Kiểm tra trạng thái, phiên bản xác nhận == phiên bản chờ ký, bản scan hợp lệ.
/// 2. Lưu bản scan, gọi ParkingService activate-lot (idempotent). Lỗi → xóa bản scan, hợp đồng giữ Pending (503 / 409).
/// 3. Hợp đồng Signed + AuditLog trong 1 SaveChanges; lưu lỗi → xóa bản scan, thử lại an toàn vì activate-lot idempotent.
/// </summary>
public sealed class SignLotContractUseCase(ILotContractRepository repository, ILotContractQueries queries, IParkingServiceClient parking,
    IFileStorage storage, TimeProvider timeProvider) : ISignLotContractUseCase
{
    public async Task<LotContractDetailDto> ExecuteAsync(int id, SignLotContractRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        LotContractSupport.EnsureIds(id, performedByUserId);
        var contract = await LotContractSupport.LoadAsync(repository, id, cancellationToken);
        var before = LotContractAudit.Snapshot(contract);
        var current = contract.EnsureCanSign(request.ConfirmedVersion);
        var (contentType, extension) = SignedScanRules.Detect(request.SignedFile);
        var scan = request.SignedFile!.Content;

        var key = LotContractSupport.SignedScanKey(contract.ContractNumber, current.Version, extension);
        await storage.SaveAsync(key, scan, cancellationToken);
        try
        {
            await parking.ActivateLotAsync(contract.KybApplicationId, contract.ContractNumber, cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        contract.Sign(performedByUserId, current.Version, new StoredFile(key, contentType, ContractSupport.Sha256Hex(scan)),
            timeProvider.GetUtcNow().UtcDateTime);
        await LotContractSupport.SaveOrDeleteAsync(repository, storage, key,
            LotContractAudit.Build(contract, "Signed", performedByUserId, before, null), cancellationToken);
        return await LotContractSupport.ReloadAsync(queries, id, cancellationToken);
    }
}

public interface ICancelLotContractUseCase
{
    Task<LotContractDetailDto> ExecuteAsync(int id, LotContractReasonRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-116 (L4): Pending → Cancelled (bãi vẫn chờ duyệt, có thể lập hợp đồng mới), lý do bắt buộc.</summary>
public sealed class CancelLotContractUseCase(ILotContractRepository repository, ILotContractQueries queries) : ICancelLotContractUseCase
{
    public async Task<LotContractDetailDto> ExecuteAsync(int id, LotContractReasonRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        LotContractSupport.EnsureIds(id, performedByUserId);
        var contract = await LotContractSupport.LoadAsync(repository, id, cancellationToken);
        var before = LotContractAudit.Snapshot(contract);
        contract.Cancel(performedByUserId, request.Reason ?? string.Empty);
        await repository.SaveWithAuditAsync(LotContractAudit.Build(contract, "Cancelled", performedByUserId, before, contract.CancelReason),
            cancellationToken);
        return await LotContractSupport.ReloadAsync(queries, id, cancellationToken);
    }
}
