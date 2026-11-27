using System.Security.Cryptography;
using System.Text;
using AdminService.Application.Features.Notifications;
using AdminService.Application.Features.Users;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== DTO =====

/// <summary>Link bổ sung hồ sơ (T3): token chỉ trả về MỘT lần cho Admin để gửi đối tác; DB chỉ giữ SHA-256.</summary>
public sealed record PartnerEditLinkDto(string Code, string Token, DateTime ExpiresAtUtc);

/// <summary>Dữ liệu hồ sơ hiện ra cho đối tác khi mở link bổ sung (T4) – chỉ trường của form + nội dung Admin yêu cầu bổ sung.</summary>
public sealed record PartnerApplicationEditDto(
    string Code, string Status, string? InfoRequestNote, DateTime? EditLinkExpiresAtUtc,
    string FullName, string Email, string Phone, string? Position,
    string BusinessType, string BusinessName, string TaxCode, string BusinessAddress,
    string LegalRepresentativeName, string? LegalRepresentativePosition,
    int LotCount, string PrimaryLotAddress, int? EstimatedCarSlots, int? EstimatedMotorbikeSlots, string? OperatingHours,
    IReadOnlyList<string>? InfoRequestItems = null, DateTime? InfoRequestDueAtUtc = null);

/// <summary>Thời hạn link bổ sung (PARTNER_EDIT_TOKEN_TTL_HOURS của US-116), mặc định 7 ngày.</summary>
public sealed record PartnerEditLinkOptions(TimeSpan Ttl)
{
    public static readonly PartnerEditLinkOptions Default = new(TimeSpan.FromDays(7));
}

internal static class PartnerEditToken
{
    public const string InvalidLinkMessage =
        "Link bổ sung hồ sơ không hợp lệ, đã được dùng hoặc đã hết hạn. Liên hệ ParkMaster (1900 6868) để nhận link mới.";
    /// <summary>ProblemDetails.code của link bổ sung: FE chọn màn hình (không hợp lệ / đã dùng / đã đóng, hoặc hết hạn) mà thông báo vẫn chung.</summary>
    public const string LinkInvalid = "edit_link_invalid";
    public const string LinkExpired = "edit_link_expired";

    public static string New() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Mã + token sai / hết hạn / hồ sơ không còn chờ bổ sung → cùng một thông báo (không lộ mã hồ sơ nào tồn tại).</summary>
    public static async Task<PartnerApplication> LoadAsync(IPartnerApplicationIntakeRepository repository, string? code, string? token,
        DateTime nowUtc, CancellationToken ct)
    {
        code = code?.Trim().ToUpperInvariant();
        token = token?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > 20 || string.IsNullOrEmpty(token) || token.Length > 128)
            throw new ValidationException(InvalidLinkMessage, LinkInvalid);
        var app = await repository.FindByCodeTrackedAsync(code, ct);
        // Chỉ khi ĐÚNG token mới phân biệt hết hạn / đã đóng (người lạ đoán mã không biết được hồ sơ nào tồn tại).
        if (app is null || !app.MatchesEditToken(Hash(token))) throw new ValidationException(InvalidLinkMessage, LinkInvalid);
        if (app.Status != PartnerApplicationStatus.NeedsMoreInfo) throw new ValidationException(InvalidLinkMessage, LinkInvalid);
        if (app.EditTokenExpiresAtUtc <= nowUtc) throw new ValidationException(InvalidLinkMessage, LinkExpired);
        return app;
    }

    public static PartnerApplicationEditDto ToDto(PartnerApplication a) => new(
        a.Code, a.Status.ToString(), a.InfoRequestNote, a.EditTokenExpiresAtUtc, a.FullName, a.Email, a.Phone, a.Position,
        a.BusinessType.ToString(), a.BusinessName, a.TaxCode, a.BusinessAddress, a.LegalRepresentativeName, a.LegalRepresentativePosition,
        a.LotCount, a.PrimaryLotAddress, a.EstimatedCarSlots, a.EstimatedMotorbikeSlots, a.OperatingHours,
        a.InfoRequestItems, a.InfoRequestDueAtUtc);
}

// ===== T3: Admin cấp link bổ sung =====

public interface IIssuePartnerEditLinkUseCase
{
    Task<PartnerEditLinkDto> ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 T3: hồ sơ NeedsMoreInfo → Admin lấy link bổ sung (một lần, hết hạn sau 7 ngày) để gửi đối tác qua email / Zalo.
/// Cấp lại → link cũ hết hiệu lực. Ghi AuditLog (không ghi token).
/// </summary>
public sealed class IssuePartnerEditLinkUseCase(IPartnerApplicationRepository repository, PartnerEditLinkOptions options, TimeProvider clock)
    : IIssuePartnerEditLinkUseCase
{
    public async Task<PartnerEditLinkDto> ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        var before = PartnerApplicationAudit.Snapshot(app);
        var token = PartnerEditToken.New();
        var expires = clock.GetUtcNow().UtcDateTime.Add(options.Ttl);
        app.IssueEditToken(performedByUserId, PartnerEditToken.Hash(token), expires);
        await repository.SaveWithAuditAsync(PartnerApplicationAudit.Build(app, "EditLinkIssued", performedByUserId, before, null), cancellationToken);
        return new PartnerEditLinkDto(app.Code, token, expires);
    }
}

// ===== T4: đối tác mở link + gửi bổ sung (public, không đăng nhập) =====

public interface IGetPartnerApplicationForEditUseCase
{
    Task<PartnerApplicationEditDto> ExecuteAsync(string? code, string? token, CancellationToken cancellationToken = default);
}

/// <summary>US-116 P2: đọc hồ sơ để đối tác sửa (mã + token trong header X-Edit-Token).</summary>
public sealed class GetPartnerApplicationForEditUseCase(IPartnerApplicationIntakeRepository repository, TimeProvider clock)
    : IGetPartnerApplicationForEditUseCase
{
    public async Task<PartnerApplicationEditDto> ExecuteAsync(string? code, string? token, CancellationToken cancellationToken = default)
        => PartnerEditToken.ToDto(await PartnerEditToken.LoadAsync(repository, code, token, clock.GetUtcNow().UtcDateTime, cancellationToken));
}

public interface IResubmitPartnerApplicationUseCase
{
    Task<PartnerApplicationReceiptDto> ExecuteAsync(string? code, string? token, SubmitPartnerApplicationRequest request,
        IReadOnlyList<PartnerDocumentUpload>? documents = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 P3 / T4: đối tác gửi bổ sung → NeedsMoreInfo → UnderReview, link hết hiệu lực. Kiểm tra như form T1 (trừ trùng với chính hồ sơ này).
/// Chỉ được thay đổi các mục Admin yêu cầu (InfoRequestItems; rỗng = mọi mục): đổi mục khác → 400. Giấy tờ: chỉ các loại được yêu cầu,
/// bắt buộc tải lại đúng các loại đó; bản cũ cùng loại chuyển Superseded (giữ lịch sử, Admin chỉ thấy bản mới). Giấy tờ hợp lệ theo
/// <see cref="PartnerDocumentRules"/> (magic bytes). File lưu trước; lưu database lỗi thì xóa file vừa ghi. AuditLog không có người thao tác.
/// </summary>
public sealed class ResubmitPartnerApplicationUseCase(IPartnerApplicationIntakeRepository intake, IPartnerApplicationRepository repository,
    IFileStorage storage, TimeProvider clock, AdminAlerts alerts) : IResubmitPartnerApplicationUseCase
{
    private static readonly Dictionary<string, PartnerDocumentType> DocumentKeys = new()
    {
        ["doc:businessLicense"] = PartnerDocumentType.BusinessLicense,
        ["doc:lotLegalProof"] = PartnerDocumentType.LotLegalProof,
        ["doc:idCardFront"] = PartnerDocumentType.IdCardFront,
        ["doc:idCardBack"] = PartnerDocumentType.IdCardBack,
        ["doc:other"] = PartnerDocumentType.Other,
    };

    public async Task<PartnerApplicationReceiptDto> ExecuteAsync(string? code, string? token, SubmitPartnerApplicationRequest request,
        IReadOnlyList<PartnerDocumentUpload>? documents = null, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var app = await PartnerEditToken.LoadAsync(intake, code, token, now, cancellationToken);
        var input = PartnerApplicationInput.From(request ?? throw new ValidationException("Thiếu dữ liệu hồ sơ."));
        if (await intake.FindOpenDuplicateAsync(input.Email, input.Phone, input.TaxCode, cancellationToken, excludeId: app.Id) is { } match)
            throw new ConflictException(
                $"Đã có hồ sơ đối tác khác đang được xử lý với {PartnerIntake.Fields(match)} này. Vui lòng liên hệ hotline 1900 6868.");

        var requested = app.InfoRequestItems;
        EnsureOnlyRequestedChanged(app, input, requested);

        var uploads = documents ?? [];
        var allowedTypes = requested.Count == 0 ? DocumentKeys.Values.ToHashSet() : requested.Where(DocumentKeys.ContainsKey).Select(k => DocumentKeys[k]).ToHashSet();
        foreach (var upload in uploads)
        {
            if (!allowedTypes.Contains(upload.Type))
                throw new ValidationException($"{PartnerDocumentRules.Label(upload.Type)} không nằm trong yêu cầu bổ sung.");
        }
        // Mỗi loại giấy tờ được yêu cầu phải có file mới (trừ khi Admin không chỉ định mục nào).
        if (requested.Count > 0)
        {
            foreach (var type in allowedTypes.Where(t => uploads.All(u => u.Type != t)))
                throw new ValidationException($"Vui lòng tải lên {char.ToLowerInvariant(PartnerDocumentRules.Label(type)[0])}{PartnerDocumentRules.Label(type)[1..]}.");
        }
        foreach (var side in new[] { PartnerDocumentType.IdCardFront, PartnerDocumentType.IdCardBack })
        {
            if (uploads.Count(u => u.Type == side) > 1) throw new ValidationException($"{PartnerDocumentRules.Label(side)}: chỉ tải lên 1 ảnh.");
        }
        if (uploads.Count > PartnerDocumentRules.MaxFiles) throw new ValidationException($"Tối đa {PartnerDocumentRules.MaxFiles} file giấy tờ cho mỗi hồ sơ.");
        if (uploads.Sum(u => u.File.Length) > PartnerDocumentRules.MaxTotalBytes) throw new ValidationException("Tổng dung lượng giấy tờ tối đa 25 MB.");
        var detected = uploads.Select(u => (Upload: u, Kind: PartnerDocumentRules.Detect(u))).ToList();

        var saved = new List<string>();
        try
        {
            var newDocuments = new List<PartnerApplicationDocument>();
            var folder = $"partner-applications/{Guid.NewGuid():N}";
            var index = 0;
            foreach (var (upload, kind) in detected)
            {
                index++;
                var key = $"{folder}/{PartnerDocumentRules.Slug(upload.Type)}-{index}{kind.Extension}";
                await storage.SaveAsync(key, upload.File.Content, cancellationToken);
                saved.Add(key);
                var fileName = PartnerDocumentRules.SafeFileName(upload.File.FileName, $"{PartnerDocumentRules.Slug(upload.Type)}{kind.Extension}");
                newDocuments.Add(PartnerApplicationDocument.Create(app, upload.Type, key, kind.ContentType, fileName,
                    upload.File.Content.Length, ContractSupport.Sha256Hex(upload.File.Content)));
            }

            var before = PartnerApplicationAudit.Snapshot(app);
            input.ApplyTo(app);
            if (newDocuments.Count > 0)
            {
                var replacedTypes = newDocuments.Select(d => d.DocumentType).ToHashSet();
                // Loại có nhiều file (giấy đăng ký, giấy tờ khác): file mới thay toàn bộ file cũ cùng loại.
                foreach (var old in await repository.FindActiveDocumentsTrackedAsync(app.Id, cancellationToken))
                    if (replacedTypes.Contains(old.DocumentType)) old.Supersede(now);
                repository.AddDocuments(newDocuments);
            }
            app.Resubmit(now);
            await repository.SaveWithAuditAsync(PartnerApplicationAudit.Build(app, "InfoProvided", null, before,
                $"Đối tác gửi bổ sung qua link ({newDocuments.Count} giấy tờ mới)"), cancellationToken);
        }
        catch
        {
            foreach (var key in saved)
            {
                try { await storage.DeleteAsync(key, CancellationToken.None); }
                catch (IOException) { /* dọn file thất bại không che lỗi gốc */ }
            }
            throw;
        }

        await alerts.RaiseAsync("PARTNER_APPLICATION_RESUBMITTED", "Đối tác đã bổ sung hồ sơ",
            $"{app.BusinessName} ({app.Code}) vừa gửi bổ sung hồ sơ, chờ thẩm định lại.", $"/admin/operations/partner-onboarding/{app.Id}");
        return new PartnerApplicationReceiptDto(app.Code, app.Status.ToString(), now);
    }

    private static string Compact(string value) => new(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

    /// <summary>Cùng một số điện thoại dù lưu "+84901234567" hay "0901234567" (hai đường nộp hồ sơ chuẩn hóa khác nhau).</summary>
    private static bool SamePhone(string a, string b)
    {
        static string Norm(string v)
        {
            var d = new string(v.Where(c => char.IsDigit(c) || c == '+').ToArray());
            return d.StartsWith("+84", StringComparison.Ordinal) ? "0" + d[3..] : d;
        }
        return Norm(a) == Norm(b);
    }

    /// <summary>Có yêu cầu cụ thể: mục nào không nằm trong danh sách mà bị đổi giá trị → 400 (không cho sửa tuỳ tiện ngoài yêu cầu).</summary>
    private static void EnsureOnlyRequestedChanged(PartnerApplication app, PartnerApplicationInput input, IReadOnlyList<string> requested)
    {
        if (requested.Count == 0) return;
        void Guard(string key, bool changed)
        {
            if (changed && !requested.Contains(key))
                throw new ValidationException($"{PartnerInfoItems.Labels[key]} không nằm trong yêu cầu bổ sung nên không được thay đổi.");
        }
        Guard("fullName", app.FullName != input.FullName);
        Guard("phone", SamePhone(app.Phone, input.Phone) is false);
        Guard("position", (app.Position ?? "") != (input.Position ?? ""));
        Guard("businessName", app.BusinessName != input.BusinessName);
        Guard("taxCode", Compact(app.TaxCode) != Compact(input.TaxCode));
        Guard("businessAddress", app.BusinessAddress != input.BusinessAddress);
        Guard("legalRepresentativeName", app.LegalRepresentativeName != input.LegalRepresentativeName);
        Guard("legalRepresentativePosition", (app.LegalRepresentativePosition ?? "") != (input.LegalRepresentativePosition ?? ""));
        Guard("lotCount", app.LotCount != input.LotCount);
        Guard("primaryLotAddress", app.PrimaryLotAddress != input.PrimaryLotAddress);
        Guard("operatingHours", (app.OperatingHours ?? "") != (input.OperatingHours ?? ""));
        Guard("estimatedSlots", app.EstimatedCarSlots != input.EstimatedCarSlots || app.EstimatedMotorbikeSlots != input.EstimatedMotorbikeSlots);
        if (!string.Equals(app.Email, input.Email, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Email không được thay đổi qua link bổ sung. Liên hệ ParkMaster (1900 6868) nếu cần đổi.");
        if (app.BusinessType != input.BusinessType) Guard("businessName", true);
    }
}

// ===== T8: đồng bộ trạng thái kích hoạt từ UserService =====

public interface IOwnerActivationSync
{
    /// <summary>Hồ sơ AccountIssued mà tài khoản bên UserService đã Active → Activated + AuditLog. Trả true khi đã chuyển.</summary>
    Task<bool> SyncAsync(int applicationId, CancellationToken cancellationToken);
}

/// <summary>
/// US-116 T8: chủ bãi tự kích hoạt bên UserService (U5), AdminService không nhận sự kiện → đồng bộ khi Admin xem danh sách / chi tiết
/// (gọi UserService bằng JWT Admin đang xem). UserService lỗi / không có quyền → bỏ qua, hiển thị trạng thái hiện có.
/// </summary>
public sealed class OwnerActivationSync(IPartnerApplicationRepository repository, IUserServiceClient users) : IOwnerActivationSync
{
    public async Task<bool> SyncAsync(int applicationId, CancellationToken cancellationToken)
    {
        var app = await repository.FindTrackedAsync(applicationId, cancellationToken);
        if (app is not { Status: PartnerApplicationStatus.AccountIssued, CreatedUserId: > 0 and var userId }) return false;
        AdminUserDto? user;
        try
        {
            user = await users.FindUserByIdAsync(userId, cancellationToken);
        }
        catch (Exception ex) when (ex is DependencyUnavailableException or AuthenticationException or ForbiddenException)
        {
            return false;
        }
        if (!string.Equals(user?.Status, "Active", StringComparison.Ordinal)) return false;

        var before = PartnerApplicationAudit.Snapshot(app);
        app.MarkActivated();
        try
        {
            await repository.SaveWithAuditAsync(
                PartnerApplicationAudit.Build(app, "Activated", null, before, "Chủ bãi đã kích hoạt tài khoản (UserService)"), cancellationToken);
        }
        catch (ConflictException)
        {
            return false;   // Admin khác vừa đồng bộ cùng lúc
        }
        return true;
    }
}
