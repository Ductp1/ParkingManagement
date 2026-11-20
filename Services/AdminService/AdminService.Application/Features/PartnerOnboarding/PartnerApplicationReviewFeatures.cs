using System.Text.Json;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== REQUEST (người thao tác lấy từ JWT, không nằm trong body) =====

/// <summary>A4 request-info.</summary>
/// <remarks>
/// Items = khóa các mục cần bổ sung (xem <see cref="PartnerInfoItems"/>), rỗng = không chỉ định. DueAtUtc = hạn bổ sung, mặc định 23:59 ICT sau
/// 7 ngày. RowVersion sai → 409.
/// </remarks>
public sealed record PartnerRequestInfoRequest(string? Note, IReadOnlyList<string>? Items = null, DateTime? DueAtUtc = null, string? RowVersion = null);

/// <summary>A11 reject / A12 cancel.</summary>
/// <remarks>RowVersion (base64, từ chi tiết hồ sơ) sai → 409; ConfirmCode = mã hồ sơ gõ lại, bắt buộc khi hủy hồ sơ đã cấp tài khoản.</remarks>
public sealed record PartnerReasonRequest(string? Reason, string? RowVersion = null, string? ConfirmCode = null);

// ===== PORT (ghi) =====

public interface IPartnerApplicationRepository
{
    /// <summary>Hồ sơ có tracking.</summary>
    Task<PartnerApplication?> FindTrackedAsync(int id, CancellationToken cancellationToken);

    /// <summary>Giấy tờ hiện hành (chưa bị thay) của hồ sơ, có tracking – để đánh dấu Superseded khi đối tác bổ sung.</summary>
    Task<IReadOnlyList<PartnerApplicationDocument>> FindActiveDocumentsTrackedAsync(int applicationId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PartnerApplicationDocument>>([]);

    /// <summary>Thêm giấy tờ mới vào cùng lần SaveChanges với hồ sơ + audit.</summary>
    void AddDocuments(IEnumerable<PartnerApplicationDocument> documents) { }

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

    /// <summary>adminUserId = null khi thao tác không do Admin (đối tác gửi bổ sung, đồng bộ trạng thái kích hoạt từ UserService).</summary>
    public static AuditLog Build(PartnerApplication app, string action, int? adminUserId, object before, string? reason) => new()
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

    /// <summary>RowVersion Admin đọc lúc mở trang khác bản hiện tại → có người khác vừa sửa hồ sơ → 409 (null = không kiểm).</summary>
    public static void EnsureVersion(PartnerApplication app, string? rowVersion)
    {
        if (rowVersion is not null && rowVersion != Convert.ToBase64String(app.RowVersion))
            throw new ConflictException("Hồ sơ đối tác vừa được Admin khác cập nhật, vui lòng tải lại và thử lại.");
    }

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

/// <summary>Khóa và nhãn tiếng Việt của các mục Admin có thể yêu cầu bổ sung; trang sửa hồ sơ chỉ mở đúng các mục này.</summary>
public static class PartnerInfoItems
{
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        ["fullName"] = "Họ và tên",
        ["position"] = "Chức vụ",
        ["phone"] = "Số điện thoại",
        ["businessName"] = "Tên doanh nghiệp / hộ kinh doanh",
        ["taxCode"] = "Mã số thuế",
        ["businessAddress"] = "Địa chỉ trụ sở",
        ["legalRepresentativeName"] = "Người đại diện pháp luật",
        ["legalRepresentativePosition"] = "Chức vụ người đại diện",
        ["lotCount"] = "Số bãi",
        ["primaryLotAddress"] = "Địa chỉ bãi chính",
        ["operatingHours"] = "Giờ hoạt động",
        ["estimatedSlots"] = "Sức chứa ước tính",
        ["doc:businessLicense"] = "Giấy đăng ký kinh doanh",
        ["doc:lotLegalProof"] = "Giấy tờ pháp lý mặt bằng",
        ["doc:idCardFront"] = "CCCD người đại diện – mặt trước",
        ["doc:idCardBack"] = "CCCD người đại diện – mặt sau",
        ["doc:other"] = "Giấy tờ khác",
    };

    public static IReadOnlyList<string> Validate(IReadOnlyList<string>? items)
    {
        var clean = (items ?? []).Select(i => i?.Trim() ?? "").Where(i => i.Length > 0).Distinct().ToList();
        if (clean.Any(i => !Labels.ContainsKey(i))) throw new ValidationException("Mục cần bổ sung không hợp lệ.");
        return clean;
    }

    public static IReadOnlyList<string> ToLabels(IEnumerable<string> keys) => keys.Select(k => Labels.GetValueOrDefault(k, k)).ToList();

    /// <summary>Hạn mặc định: 23:59:59 ICT của ngày thứ 7 kể từ hôm nay.</summary>
    public static DateTime DefaultDueUtc(DateTime nowUtc)
    {
        var ict = nowUtc.AddHours(7).Date.AddDays(7).AddDays(1).AddSeconds(-1);
        return DateTime.SpecifyKind(ict.AddHours(-7), DateTimeKind.Utc);
    }
}

/// <summary>Dựng link bổ sung hồ sơ: {trang bổ sung}#code=PA-…&amp;token=… (token sau dấu # nên không vào log máy chủ / Referer).</summary>
public interface IPartnerEditLinkBuilder
{
    string Build(string applicationCode, string token);
}

/// <summary>
/// US-116 (A4, T3): UnderReview → NeedsMoreInfo, ghi chú bắt buộc, chọn các mục cần bổ sung và hạn; sinh link bổ sung mới
/// (SHA-256, hết hạn đúng hạn bổ sung) và gửi email cho người nộp qua UserService. Hồ sơ + audit lưu trước; gửi email lỗi thì
/// InfoRequestEmailStatus = Failed và Admin bấm "Resend request email".
/// </summary>
public sealed class RequestPartnerInfoUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries,
    IOwnerAccountClient mailer, IPartnerEditLinkBuilder links, TimeProvider clock) : IRequestPartnerInfoUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerRequestInfoRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        PartnerApplicationSteps.EnsureVersion(app, request.RowVersion);
        var items = PartnerInfoItems.Validate(request.Items);
        var now = clock.GetUtcNow().UtcDateTime;
        var token = PartnerEditToken.New();
        var before = PartnerApplicationAudit.Snapshot(app);
        app.RequestInfo(performedByUserId, request.Note ?? string.Empty, items, request.DueAtUtc ?? PartnerInfoItems.DefaultDueUtc(now),
            PartnerEditToken.Hash(token), now);
        await repository.SaveWithAuditAsync(
            PartnerApplicationAudit.Build(app, "InfoRequested", performedByUserId, before, app.InfoRequestNote), cancellationToken);
        await PartnerInfoMail.SendAsync(app, token, performedByUserId, repository, mailer, links, cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);
    }
}

public interface IResendPartnerInfoRequestUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerResendInfoRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>DueAtUtc = null giữ hạn cũ.</summary>
public sealed record PartnerResendInfoRequest(DateTime? DueAtUtc = null, string? RowVersion = null);

/// <summary>US-116: gửi lại email yêu cầu bổ sung – xoay link bổ sung (link cũ hết hiệu lực) và gửi lại email.</summary>
public sealed class ResendPartnerInfoRequestUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries,
    IOwnerAccountClient mailer, IPartnerEditLinkBuilder links, TimeProvider clock) : IResendPartnerInfoRequestUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerResendInfoRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        PartnerApplicationSteps.EnsureVersion(app, request.RowVersion);
        var now = clock.GetUtcNow().UtcDateTime;
        var token = PartnerEditToken.New();
        var before = PartnerApplicationAudit.Snapshot(app);
        app.ReissueInfoRequest(performedByUserId, PartnerEditToken.Hash(token), request.DueAtUtc, now);
        await repository.SaveWithAuditAsync(PartnerApplicationAudit.Build(app, "InfoRequestResent", performedByUserId, before, null), cancellationToken);
        await PartnerInfoMail.SendAsync(app, token, performedByUserId, repository, mailer, links, cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);
    }
}

internal static class PartnerInfoMail
{
    public static async Task SendAsync(PartnerApplication app, string token, int adminUserId, IPartnerApplicationRepository repository,
        IOwnerAccountClient mailer, IPartnerEditLinkBuilder links, CancellationToken ct)
    {
        var before = PartnerApplicationAudit.Snapshot(app);
        string action;
        try
        {
            await mailer.SendPartnerEmailAsync(new PartnerEmailCommand("PartnerInfoRequested", app.Email, app.FullName, app.Code,
                app.InfoRequestNote, PartnerInfoItems.ToLabels(app.InfoRequestItems), app.InfoRequestDueAtUtc, links.Build(app.Code, token)), ct);
            app.MarkInfoEmailSent();
            action = "InfoRequestEmailQueued";
        }
        catch (Exception ex) when (ex is DependencyUnavailableException or ConflictException or ValidationException
            or AuthenticationException or ForbiddenException)
        {
            app.MarkInfoEmailFailed(ex.Message);
            action = "InfoRequestEmailFailed";
        }
        await repository.SaveWithAuditAsync(PartnerApplicationAudit.Build(app, action, adminUserId, before, app.InfoRequestEmailError), ct);
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

/// <summary>
/// US-116 (A12, T10) – Admin hủy hồ sơ ở bất kỳ trạng thái chưa kết thúc: Submitted / UnderReview / NeedsMoreInfo / AccountIssued.
/// Hồ sơ đã cấp tài khoản mà chủ bãi chưa kích hoạt: ghi Cancelled trước (+ audit cùng SaveChanges), rồi nhờ UserService gỡ tài khoản để
/// email / SĐT / MST dùng lại được. UserService lỗi → hồ sơ vẫn Cancelled, CleanupSyncStatus = Failed và thử lại qua retry-cleanup.
/// Chủ bãi đã kích hoạt → 409 (dùng Đình chỉ / Xóa user). Gọi lại hồ sơ đã hủy → trả trạng thái hiện tại (idempotent).
/// </summary>
public sealed class CancelPartnerApplicationUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries,
    IOwnerAccountClient ownerAccounts, IOwnerActivationSync activationSync, TimeProvider clock) : ICancelPartnerApplicationUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, PartnerReasonRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        PartnerApplicationSteps.EnsureVersion(app, request.RowVersion);
        if (app.Status == PartnerApplicationStatus.Cancelled) return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);

        if (app.Status == PartnerApplicationStatus.AccountIssued)
        {
            // Chủ bãi có thể vừa kích hoạt: đồng bộ trước khi quyết định (cùng DbContext nên `app` đã được cập nhật).
            await activationSync.SyncAsync(id, cancellationToken);
            if (app.Status == PartnerApplicationStatus.Activated)
                throw new ConflictException("Chủ bãi đã kích hoạt tài khoản – hãy dùng Đình chỉ hoặc Xóa user thay vì hủy hồ sơ.");
            if (!string.Equals(request.ConfirmCode?.Trim(), app.Code, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Gõ lại mã hồ sơ để xác nhận hủy hồ sơ đã cấp tài khoản.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var before = PartnerApplicationAudit.Snapshot(app);
        app.Cancel(performedByUserId, request.Reason ?? string.Empty, now);
        await repository.SaveWithAuditAsync(
            PartnerApplicationAudit.Build(app, "Cancelled", performedByUserId, before, app.CancelReason), cancellationToken);

        if (app.CleanupSyncStatus == PartnerSyncStatus.Pending)
            await PartnerApplicationCleanup.RunAsync(app, performedByUserId, repository, ownerAccounts, clock, cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);
    }
}

public interface IRetryPartnerCleanupUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>US-116: thử lại việc gỡ tài khoản chủ bãi bên UserService sau khi hủy hồ sơ (CleanupSyncStatus Pending / Failed).</summary>
public sealed class RetryPartnerCleanupUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries,
    IOwnerAccountClient ownerAccounts, TimeProvider clock) : IRetryPartnerCleanupUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        app.EnsureCanRetryCleanup();
        await PartnerApplicationCleanup.RunAsync(app, performedByUserId, repository, ownerAccounts, clock, cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);
    }
}

/// <summary>Gọi UserService gỡ tài khoản chủ bãi chưa kích hoạt rồi ghi kết quả + audit. Không ném lỗi của UserService ra ngoài.</summary>
internal static class PartnerApplicationCleanup
{
    public static async Task RunAsync(PartnerApplication app, int adminUserId, IPartnerApplicationRepository repository,
        IOwnerAccountClient ownerAccounts, TimeProvider clock, CancellationToken ct)
    {
        var before = PartnerApplicationAudit.Snapshot(app);
        string action;
        try
        {
            await ownerAccounts.DiscardOwnerAccountAsync(app.EnsureCanRetryCleanup(), app.CancelReason ?? "Hủy hồ sơ đối tác", ct);
            app.MarkCleanupSynced(clock.GetUtcNow().UtcDateTime);
            action = "OwnerAccountDiscarded";
        }
        catch (Exception ex) when (ex is DependencyUnavailableException or ConflictException or ValidationException
            or AuthenticationException or ForbiddenException)
        {
            app.MarkCleanupFailed(ex.Message);
            action = "OwnerCleanupFailed";
        }
        await repository.SaveWithAuditAsync(PartnerApplicationAudit.Build(app, action, adminUserId, before, app.CleanupError), ct);
    }
}
