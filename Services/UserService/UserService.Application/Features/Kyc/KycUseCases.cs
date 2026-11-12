using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Kyc;

// ===== DTO =====
public sealed record KycStatusDto(string Status, string? DocumentType, string? MaskedNumber, DateTime? SubmittedAtUtc,
    DateTime? VerifiedAtUtc, string? RejectionReason);
public sealed record AdminKycItem(int UserId, string FullName, string DocumentType, string MaskedNumber, DateTime? SubmittedAtUtc);
public sealed record ReviewKyc(string? Reason);

/// <summary>Hàng đợi chờ duyệt: số giấy tờ vẫn ở dạng mã hóa, Use Case mới giải mã để che bớt.</summary>
public sealed record KycQueueRow(int UserId, string FullName, KycDocumentType? DocumentType, string? NumberEncrypted, DateTime? SubmittedAtUtc);

// ===== PORT =====
public interface IKycStore
{
    Task<IReadOnlyList<KycQueueRow>> ListPendingAsync(int page, int pageSize, CancellationToken ct);
    Task<SecurityEvent?> LatestKycEventAsync(int userId, CancellationToken ct);
}

// ===== USE CASE (US-005) =====
/// <summary>
/// Định danh điện tử KYC bằng số CCCD (giả lập liên kết VNeID). Số CCCD mã hóa AES-256-GCM, không lưu ảnh giấy tờ.
/// Bị từ chối → xóa số giấy tờ (tối thiểu hóa dữ liệu), người dùng gửi lại hồ sơ mới.
/// </summary>
public sealed class KycUseCases(IIdentityStore store, IKycStore kyc, ISecretCipher cipher,
    ICurrentUser current, TimeProvider clock)
{
    public const int PageSize = 20;
    public const string CipherPurpose = "kyc-number";

    public async Task<KycStatusDto> GetMineAsync(CancellationToken ct)
    {
        var user = await store.FindUserAsync(UserId(), false, ct) ?? throw new AuthenticationException("Tài khoản không hợp lệ.");
        var latest = await kyc.LatestKycEventAsync(user.Id, ct);
        return new KycStatusDto(user.KycStatus.ToString(), user.KycDocumentType?.ToString(), Mask(user.KycNumberEncrypted),
            SubmittedAt(latest), user.KycVerifiedAtUtc,
            user.KycStatus == KycStatus.Rejected && latest?.EventType == SecurityEventType.KycRejected ? latest.Detail : null);
    }

    /// <summary>
    /// Xác minh bằng số CCCD 12 chữ số: số được mã hóa, hồ sơ chuyển Pending để Admin duyệt.
    /// </summary>
    public async Task<KycStatusDto> SubmitCitizenIdAsync(string? documentNumber, CancellationToken ct)
    {
        var userId = UserId();
        var number = DocumentNumber(KycDocumentType.CitizenId, documentNumber);
        await using (var tx = await store.BeginAsync(ct))
        {
            var user = await store.FindUserAsync(userId, true, ct) ?? throw new AuthenticationException("Tài khoản không hợp lệ.");
            EnsureCanSubmit(user);
            user.KycStatus = KycStatus.Pending;
            user.KycDocumentType = KycDocumentType.CitizenId;
            user.KycNumberEncrypted = cipher.Encrypt(number, CipherPurpose);
            user.KycFrontImageUrl = null;
            user.KycBackImageUrl = null;
            user.KycVerifiedAtUtc = null;
            store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.KycSubmitted, Detail = nameof(KycDocumentType.CitizenId) });
            await store.SaveAsync(ct);
            await tx.CommitAsync(ct);
        }
        return await GetMineAsync(ct);
    }

    // ===== Admin =====
    public async Task<IReadOnlyList<AdminKycItem>> ListPendingAsync(int page, CancellationToken ct)
    {
        if (page < 1 || page > int.MaxValue / PageSize) throw new ValidationException("page không hợp lệ.");
        var rows = await kyc.ListPendingAsync(page, PageSize, ct);
        return rows.Select(r => new AdminKycItem(r.UserId, r.FullName, r.DocumentType?.ToString() ?? "", Mask(r.NumberEncrypted) ?? "", r.SubmittedAtUtc)).ToList();
    }

    public async Task ApproveAsync(int userId, CancellationToken ct)
    {
        var adminId = UserId();
        if (userId == adminId) throw new ForbiddenException("Không thể tự duyệt hồ sơ định danh của chính mình.");
        await using var tx = await store.BeginAsync(ct);
        var user = await PendingUserAsync(userId, ct);
        user.KycStatus = KycStatus.Verified;
        user.KycVerifiedAtUtc = clock.GetUtcNow().UtcDateTime;
        store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.KycApproved, Detail = $"Duyệt bởi #{adminId}" });
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task RejectAsync(int userId, ReviewKyc input, CancellationToken ct)
    {
        var adminId = UserId();
        if (userId == adminId) throw new ForbiddenException("Không thể tự duyệt hồ sơ định danh của chính mình.");
        var reason = input.Reason?.Trim();
        if (string.IsNullOrEmpty(reason)) throw new ValidationException("Cần nhập lý do từ chối để người dùng biết cần sửa gì.");
        if (reason.Length > 500) throw new ValidationException("Lý do từ chối tối đa 500 ký tự.");
        await using (var tx = await store.BeginAsync(ct))
        {
            var user = await PendingUserAsync(userId, ct);
            user.KycStatus = KycStatus.Rejected;
            user.KycNumberEncrypted = null;
            user.KycFrontImageUrl = null;
            user.KycBackImageUrl = null;
            store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.KycRejected, Detail = reason });
            await store.SaveAsync(ct);
            await tx.CommitAsync(ct);
        }
    }

    // ===== Quy tắc =====
    /// <summary>CCCD gắn chip và GPLX mẫu mới (PET) đều gồm đúng 12 chữ số.</summary>
    public static string DocumentNumber(KycDocumentType type, string? value)
    {
        if (type is not (KycDocumentType.CitizenId or KycDocumentType.DriverLicense))
            throw new ValidationException("Chỉ nhận CCCD hoặc giấy phép lái xe.");
        var number = new string((value ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (number.Length != 12 || !number.All(c => c is >= '0' and <= '9'))
            throw new ValidationException(type == KycDocumentType.CitizenId
                ? "Số CCCD gồm đúng 12 chữ số."
                : "Số giấy phép lái xe gồm đúng 12 chữ số.");
        return number;
    }

    private string? Mask(string? encrypted)
    {
        if (encrypted is null) return null;
        var plain = cipher.Decrypt(encrypted, CipherPurpose);
        return new string('•', Math.Max(0, plain.Length - 4)) + plain[^Math.Min(4, plain.Length)..];
    }

    private static DateTime? SubmittedAt(SecurityEvent? latest) => latest?.EventType == SecurityEventType.KycSubmitted ? latest.CreatedAtUtc : null;

    private void EnsureCanSubmit(User user)
    {
        IdentityRules.CanAuthenticate(user, clock.GetUtcNow().UtcDateTime);
        if (user.KycStatus == KycStatus.Pending) throw new ConflictException("Hồ sơ định danh đang chờ duyệt, chưa thể gửi lại.");
        if (user.KycStatus == KycStatus.Verified) throw new ConflictException("Tài khoản đã được xác minh danh tính.");
    }

    private async Task<User> PendingUserAsync(int userId, CancellationToken ct)
    {
        var user = await store.FindUserAsync(userId, true, ct) ?? throw new NotFoundException("Người dùng", userId);
        if (user.KycStatus != KycStatus.Pending) throw new ConflictException("Hồ sơ định danh không ở trạng thái chờ duyệt.");
        return user;
    }

    private int UserId() => current.UserId ?? throw new AuthenticationException("Chưa đăng nhập.");
}
