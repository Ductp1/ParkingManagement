using System.Security.Cryptography;
using System.Text;
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
    int LotCount, string PrimaryLotAddress, int? EstimatedCarSlots, int? EstimatedMotorbikeSlots, string? OperatingHours);

/// <summary>Thời hạn link bổ sung (PARTNER_EDIT_TOKEN_TTL_HOURS của US-116), mặc định 7 ngày.</summary>
public sealed record PartnerEditLinkOptions(TimeSpan Ttl)
{
    public static readonly PartnerEditLinkOptions Default = new(TimeSpan.FromDays(7));
}

internal static class PartnerEditToken
{
    public const string InvalidLinkMessage =
        "Link bổ sung hồ sơ không hợp lệ, đã được dùng hoặc đã hết hạn. Liên hệ ParkMaster (1900 6868) để nhận link mới.";

    public static string New() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Mã + token sai / hết hạn / hồ sơ không còn chờ bổ sung → cùng một thông báo (không lộ mã hồ sơ nào tồn tại).</summary>
    public static async Task<PartnerApplication> LoadAsync(IPartnerApplicationIntakeRepository repository, string? code, string? token,
        DateTime nowUtc, CancellationToken ct)
    {
        code = code?.Trim().ToUpperInvariant();
        token = token?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > 20 || string.IsNullOrEmpty(token) || token.Length > 128)
            throw new ValidationException(InvalidLinkMessage);
        var app = await repository.FindByCodeTrackedAsync(code, ct);
        if (app is null || !app.AcceptsEditToken(Hash(token), nowUtc)) throw new ValidationException(InvalidLinkMessage);
        return app;
    }

    public static PartnerApplicationEditDto ToDto(PartnerApplication a) => new(
        a.Code, a.Status.ToString(), a.InfoRequestNote, a.EditTokenExpiresAtUtc, a.FullName, a.Email, a.Phone, a.Position,
        a.BusinessType.ToString(), a.BusinessName, a.TaxCode, a.BusinessAddress, a.LegalRepresentativeName, a.LegalRepresentativePosition,
        a.LotCount, a.PrimaryLotAddress, a.EstimatedCarSlots, a.EstimatedMotorbikeSlots, a.OperatingHours);
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
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 P3 / T4: đối tác gửi lại hồ sơ đã sửa → NeedsMoreInfo → UnderReview (Admin thẩm định tiếp), link hết hiệu lực.
/// Kiểm tra như form T1 (trừ trùng với chính hồ sơ này). AuditLog không có người thao tác (đối tác chưa có tài khoản).
/// </summary>
public sealed class ResubmitPartnerApplicationUseCase(IPartnerApplicationIntakeRepository intake, IPartnerApplicationRepository repository,
    TimeProvider clock) : IResubmitPartnerApplicationUseCase
{
    public async Task<PartnerApplicationReceiptDto> ExecuteAsync(string? code, string? token, SubmitPartnerApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var app = await PartnerEditToken.LoadAsync(intake, code, token, now, cancellationToken);
        var input = PartnerApplicationInput.From(request ?? throw new ValidationException("Thiếu dữ liệu hồ sơ."));
        if (await intake.FindOpenDuplicateAsync(input.Email, input.Phone, input.TaxCode, cancellationToken, excludeId: app.Id) is { } match)
            throw new ConflictException(
                $"Đã có hồ sơ đối tác khác đang được xử lý với {PartnerIntake.Fields(match)} này. Vui lòng liên hệ hotline 1900 6868.");

        var before = PartnerApplicationAudit.Snapshot(app);
        input.ApplyTo(app);
        app.Resubmit(now);
        await repository.SaveWithAuditAsync(PartnerApplicationAudit.Build(app, "Resubmitted", null, before, "Đối tác gửi bổ sung qua link"),
            cancellationToken);
        return new PartnerApplicationReceiptDto(app.Code, app.Status.ToString(), now);
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
