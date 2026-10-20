using AdminService.Domain.Entities;
using AdminService.Domain.ValueObjects;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== REQUEST =====

/// <summary>A5 contract-preview / A6 send-contract. CommissionRate theo % (8.5 = 8,5%).</summary>
public sealed record PartnerContractTermsRequest(decimal? CommissionRate, DateOnly? EffectiveFrom, int? TermMonths,
    PartnerBankAccountRequest? Bank);

/// <summary>A7 revise-contract: điều khoản mới + lý do sửa.</summary>
public sealed record PartnerReviseContractRequest(decimal? CommissionRate, DateOnly? EffectiveFrom, int? TermMonths,
    PartnerBankAccountRequest? Bank, string? Reason);

// ===== USE CASE =====

public interface IPreviewPartnerContractUseCase
{
    Task<ContractFileResult> ExecuteAsync(int id, PartnerContractTermsRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 (A5): sinh thử hợp đồng với điều khoản Admin nhập – không lưu file, không đổi trạng thái.
/// Hồ sơ UnderReview xem trước v1, ContractPending xem trước phiên bản kế tiếp.
/// </summary>
public sealed class PreviewPartnerContractUseCase(IPartnerApplicationRepository repository, IContractRenderer renderer,
    TimeProvider timeProvider) : IPreviewPartnerContractUseCase
{
    public async Task<ContractFileResult> ExecuteAsync(int id, PartnerContractTermsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        app.EnsureCanGenerateContract();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var terms = PartnerContractSupport.Terms(request.CommissionRate, request.EffectiveFrom, request.TermMonths, request.Bank, now);
        var template = await PartnerContractSupport.ActiveTemplateAsync(repository, cancellationToken);

        var number = app.ContractNumberFor(now.Year);
        var version = app.NextDraftVersion;
        var file = renderer.Render(template.Body, template.Version, PartnerContractSupport.Values(app, number, version, terms, now));
        return new ContractFileResult(file.Content, file.ContentType, $"{number}-v{version}-preview{file.FileExtension}");
    }
}

public interface ISendPartnerContractUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerContractTermsRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 (A6, T5): UnderReview → ContractPending. Cấp số hợp đồng, sinh file v1 từ mẫu đang áp dụng, lưu file + SHA-256.
/// Hồ sơ + phiên bản hợp đồng + AuditLog cùng 1 SaveChanges; lưu database lỗi thì xóa file vừa ghi.
/// </summary>
public sealed class SendPartnerContractUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries,
    IContractRenderer renderer, IFileStorage storage, TimeProvider timeProvider) : ISendPartnerContractUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerContractTermsRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        var before = PartnerApplicationAudit.Snapshot(app);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        app.EnsureCanSendContract();   // sai trạng thái → 409 trước khi kiểm tra điều khoản (400)
        var terms = PartnerContractSupport.Terms(request.CommissionRate, request.EffectiveFrom, request.TermMonths, request.Bank, now);
        var template = await PartnerContractSupport.ActiveTemplateAsync(repository, cancellationToken);

        var number = app.ContractNumberFor(now.Year);
        var stored = await PartnerContractFiles.RenderAndStoreAsync(app, number, terms, template, renderer, storage, now, cancellationToken);
        app.SendContract(performedByUserId, terms, template.Version, number, stored, now);

        await PartnerContractFiles.SaveOrDeleteAsync(repository, storage, stored.Key,
            PartnerApplicationAudit.Build(app, "ContractSent", performedByUserId, before, null), cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);
    }
}

public interface IRevisePartnerContractUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerReviseContractRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 (A7, T5b): ContractPending giữ nguyên, sinh phiên bản v+1 cùng số hợp đồng; bản đang gửi → Superseded; lý do bắt buộc.
/// </summary>
public sealed class RevisePartnerContractUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries,
    IContractRenderer renderer, IFileStorage storage, TimeProvider timeProvider) : IRevisePartnerContractUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerReviseContractRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        var before = PartnerApplicationAudit.Snapshot(app);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        app.EnsureCanReviseContract();
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ValidationException("Lý do sửa hợp đồng không được để trống.");
        var terms = PartnerContractSupport.Terms(request.CommissionRate, request.EffectiveFrom, request.TermMonths, request.Bank, now);
        var template = await PartnerContractSupport.ActiveTemplateAsync(repository, cancellationToken);

        var number = app.ContractNumberFor(now.Year);
        var stored = await PartnerContractFiles.RenderAndStoreAsync(app, number, terms, template, renderer, storage, now, cancellationToken);
        var draft = app.ReviseContract(performedByUserId, terms, template.Version, stored, request.Reason, now);

        await PartnerContractFiles.SaveOrDeleteAsync(repository, storage, stored.Key,
            PartnerApplicationAudit.Build(app, "ContractRevised", performedByUserId, before, draft.RevisionReason), cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);
    }
}

public interface IGetPartnerContractFileUseCase
{
    Task<ContractFileResult> ExecuteAsync(int id, int version, CancellationToken cancellationToken = default);
}

/// <summary>US-116 (A8): tải file hợp đồng một phiên bản; SHA-256 tính lại phải khớp giá trị lưu lúc sinh.</summary>
public sealed class GetPartnerContractFileUseCase(IPartnerApplicationQueries queries, IFileStorage storage)
    : IGetPartnerContractFileUseCase
{
    public async Task<ContractFileResult> ExecuteAsync(int id, int version, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        if (version <= 0) throw new ValidationException("version phải là số nguyên dương.");
        var info = await queries.GetContractFileAsync(id, version, cancellationToken)
                   ?? throw new NotFoundException("Phiên bản hợp đồng", $"{id}/v{version}");
        var content = await storage.ReadAsync(info.FileKey, cancellationToken)
                      ?? throw new NotFoundException("File hợp đồng", $"{info.ContractNumber}/v{version}");
        if (!string.Equals(PartnerContractSupport.Sha256Hex(content), info.FileSha256, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(
                $"File hợp đồng {info.ContractNumber} phiên bản v{version} không còn toàn vẹn (SHA-256 không khớp), vui lòng sinh phiên bản mới.");

        var extension = Path.GetExtension(info.FileKey);
        return new ContractFileResult(content, info.FileContentType, $"{info.ContractNumber}-v{version}{extension}");
    }
}

/// <summary>Sinh + lưu file hợp đồng, và dọn file khi lưu database thất bại.</summary>
internal static class PartnerContractFiles
{
    public static async Task<StoredFile> RenderAndStoreAsync(PartnerApplication app, string contractNumber, ContractTerms terms,
        ContractTemplate template, IContractRenderer renderer, IFileStorage storage, DateTime nowUtc, CancellationToken ct)
    {
        var version = app.NextDraftVersion;
        var file = renderer.Render(template.Body, template.Version,
            PartnerContractSupport.Values(app, contractNumber, version, terms, nowUtc));
        var key = PartnerContractSupport.ContractKey(app.Id, version, file.FileExtension);
        await storage.SaveAsync(key, file.Content, ct);
        return new StoredFile(key, file.ContentType, PartnerContractSupport.Sha256Hex(file.Content));
    }

    public static async Task SaveOrDeleteAsync(IPartnerApplicationRepository repository, IFileStorage storage, string fileKey,
        AuditLog auditLog, CancellationToken ct)
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
}
