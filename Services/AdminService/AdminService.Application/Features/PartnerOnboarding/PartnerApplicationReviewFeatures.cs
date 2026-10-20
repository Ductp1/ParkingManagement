using System.Text.Json;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== REQUEST (người thao tác lấy từ JWT, không nằm trong body) =====

/// <summary>A4 request-info.</summary>
public sealed record PartnerRequestInfoRequest(string? Note);

/// <summary>A11 reject / A12 cancel.</summary>
public sealed record PartnerReasonRequest(string? Reason);

// ===== PORT (ghi) =====

public interface IPartnerApplicationRepository
{
    /// <summary>Hồ sơ có tracking.</summary>
    Task<PartnerApplication?> FindTrackedAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Ghi thay đổi của hồ sơ đang tracking + audit log trong CÙNG 1 lần SaveChanges.
    /// RowVersion lệch (Admin khác vừa sửa) → ConflictException.
    /// </summary>
    Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken);
}

/// <summary>Audit log của các thao tác Admin trên hồ sơ đối tác (cùng mẫu Owners - Lock của US-096).</summary>
public static class PartnerApplicationAudit
{
    public const string EntityName = nameof(PartnerApplication);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Ảnh chụp các trường được audit (gọi trước và sau thao tác).</summary>
    public static object Snapshot(PartnerApplication app) => new
    {
        status = app.Status.ToString(),
        reviewerAdminId = app.ReviewerAdminId,
        ownerProfileId = app.CreatedOwnerProfileId,
        payoutBankAccount = app.PayoutBankAccountNumber,
    };

    public static AuditLog Build(PartnerApplication app, string action, int adminUserId, object before, string? reason) => new()
    {
        SourceService = "AdminService",
        UserId = adminUserId,
        OwnerProfileId = app.CreatedOwnerProfileId,
        Action = $"{EntityName}.{action}",
        EntityName = EntityName,
        EntityId = app.Id.ToString(),
        OldValuesJson = JsonSerializer.Serialize(before, Json),
        NewValuesJson = JsonSerializer.Serialize(Snapshot(app), Json),
        Reason = reason,
    };
}

/// <summary>Các bước dùng chung: kiểm tra tham số, nạp hồ sơ, đọc lại chi tiết sau khi lưu.</summary>
internal static class PartnerApplicationSteps
{
    public static void EnsureIds(int id, int adminUserId)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        if (adminUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
    }

    public static async Task<PartnerApplication> LoadAsync(IPartnerApplicationRepository repository, int id, CancellationToken ct)
        => await repository.FindTrackedAsync(id, ct) ?? throw new NotFoundException("Hồ sơ đối tác", id);

    public static async Task<PartnerApplicationDetailDto> ReloadAsync(IPartnerApplicationQueries queries, int id, CancellationToken ct)
        => await queries.GetDetailAsync(id, ct) ?? throw new NotFoundException("Hồ sơ đối tác", id);
}

// ===== USE CASE (ghi) =====

public interface IStartPartnerReviewUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>US-116 (A3, T2): Submitted → UnderReview, người thẩm định = Admin trong token. Hồ sơ + AuditLog cùng 1 SaveChanges.</summary>
public sealed class StartPartnerReviewUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries)
    : IStartPartnerReviewUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        var before = PartnerApplicationAudit.Snapshot(app);
        app.StartReview(performedByUserId);
        await repository.SaveWithAuditAsync(PartnerApplicationAudit.Build(app, "ReviewStarted", performedByUserId, before, null),
            cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);
    }
}

public interface IRequestPartnerInfoUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerRequestInfoRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-116 (A4, T3): UnderReview → NeedsMoreInfo, ghi chú bắt buộc.</summary>
public sealed class RequestPartnerInfoUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries)
    : IRequestPartnerInfoUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerRequestInfoRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        var before = PartnerApplicationAudit.Snapshot(app);
        app.RequestInfo(performedByUserId, request.Note ?? string.Empty);
        await repository.SaveWithAuditAsync(
            PartnerApplicationAudit.Build(app, "InfoRequested", performedByUserId, before, app.InfoRequestNote), cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);
    }
}

public interface IRejectPartnerApplicationUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerReasonRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-116 (A11, T9): UnderReview → Rejected, lý do bắt buộc.</summary>
public sealed class RejectPartnerApplicationUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries)
    : IRejectPartnerApplicationUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerReasonRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        var before = PartnerApplicationAudit.Snapshot(app);
        app.Reject(performedByUserId, request.Reason ?? string.Empty);
        await repository.SaveWithAuditAsync(
            PartnerApplicationAudit.Build(app, "Rejected", performedByUserId, before, app.RejectReason), cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);
    }
}

public interface ICancelPartnerApplicationUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerReasonRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-116 (A12, T10): UnderReview / NeedsMoreInfo → Cancelled khi đối tác rút hồ sơ.</summary>
public sealed class CancelPartnerApplicationUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries)
    : ICancelPartnerApplicationUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerReasonRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        var before = PartnerApplicationAudit.Snapshot(app);
        app.Cancel(performedByUserId, request.Reason ?? string.Empty);
        await repository.SaveWithAuditAsync(
            PartnerApplicationAudit.Build(app, "Cancelled", performedByUserId, before, app.CancelReason), cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);
    }
}
