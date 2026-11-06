using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Kyc;

// ===== DTO =====
public sealed record KycStatusDto(string Status, string? DocumentType, string? MaskedNumber, DateTime? SubmittedAtUtc,
    DateTime? VerifiedAtUtc, string? RejectionReason, bool HasFrontImage, bool HasBackImage);
public sealed record KycImageUpload(Stream Content, long Length);
public sealed record SubmitKycCommand(KycDocumentType DocumentType, string DocumentNumber, KycImageUpload Front, KycImageUpload Back);
public sealed record AdminKycItem(int UserId, string FullName, string DocumentType, string MaskedNumber, DateTime? SubmittedAtUtc);
public sealed record ReviewKyc(string? Reason);
public enum KycImageSide { Front = 1, Back = 2 }

/// <summary>Hàng đợi chờ duyệt: số giấy tờ vẫn ở dạng mã hóa, Use Case mới giải mã để che bớt.</summary>
public sealed record KycQueueRow(int UserId, string FullName, KycDocumentType? DocumentType, string? NumberEncrypted, DateTime? SubmittedAtUtc);

// ===== PORT =====
/// <summary>Ảnh giấy tờ: lưu riêng tư, mã hóa khi lưu; không bao giờ có URL công khai.</summary>
public interface IKycDocumentStorage
{
    Task<string> SaveAsync(Stream image, CancellationToken ct);
    Task<byte[]?> ReadAsync(string? key, CancellationToken ct);
    Task DeleteAsync(string? key, CancellationToken ct);
}

public interface IKycStore
{
    Task<IReadOnlyList<KycQueueRow>> ListPendingAsync(int page, int pageSize, CancellationToken ct);
    Task<SecurityEvent?> LatestKycEventAsync(int userId, CancellationToken ct);
}

// ===== USE CASE (US-005) =====
/// <summary>
/// Định danh điện tử KYC bằng CCCD hoặc GPLX. Số giấy tờ mã hóa AES-256-GCM; ảnh giấy tờ mã hóa khi lưu.
/// Bị từ chối → xóa ảnh và số giấy tờ (tối thiểu hóa dữ liệu), người dùng gửi lại hồ sơ mới.
/// </summary>
public sealed class KycUseCases(IIdentityStore store, IKycStore kyc, IKycDocumentStorage files, ISecretCipher cipher,
    ICurrentUser current, TimeProvider clock)
{
    public const int MaxImageBytes = 5 * 1024 * 1024;
    public const int PageSize = 20;
    public const string CipherPurpose = "kyc-number";

    public async Task<KycStatusDto> GetMineAsync(CancellationToken ct)
    {
        var user = await store.FindUserAsync(UserId(), false, ct) ?? throw new AuthenticationException("Tài khoản không hợp lệ.");
        var latest = await kyc.LatestKycEventAsync(user.Id, ct);
        return new KycStatusDto(user.KycStatus.ToString(), user.KycDocumentType?.ToString(), Mask(user.KycNumberEncrypted),
            SubmittedAt(latest), user.KycVerifiedAtUtc,
            user.KycStatus == KycStatus.Rejected && latest?.EventType == SecurityEventType.KycRejected ? latest.Detail : null,
            user.KycFrontImageUrl is not null, user.KycBackImageUrl is not null);
    }

    public async Task<KycStatusDto> SubmitAsync(SubmitKycCommand command, CancellationToken ct)
    {
        var userId = UserId();
        var number = DocumentNumber(command.DocumentType, command.DocumentNumber);
        Image(command.Front, "mặt trước");
        Image(command.Back, "mặt sau");

        // Kiểm tra trạng thái trước khi tốn công xử lý ảnh; kiểm tra lại dưới khóa trong transaction.
        var snapshot = await store.FindUserAsync(userId, false, ct) ?? throw new AuthenticationException("Tài khoản không hợp lệ.");
        EnsureCanSubmit(snapshot);

        var front = await files.SaveAsync(command.Front.Content, ct);
        string? back = null;
        string? oldFront, oldBack;
        try
        {
            back = await files.SaveAsync(command.Back.Content, ct);
            await using var tx = await store.BeginAsync(ct);
            var user = await store.FindUserAsync(userId, true, ct) ?? throw new AuthenticationException("Tài khoản không hợp lệ.");
            EnsureCanSubmit(user);
            oldFront = user.KycFrontImageUrl;
            oldBack = user.KycBackImageUrl;
            user.KycStatus = KycStatus.Pending;
            user.KycDocumentType = command.DocumentType;
            user.KycNumberEncrypted = cipher.Encrypt(number, CipherPurpose);
            user.KycFrontImageUrl = front;
            user.KycBackImageUrl = back;
            user.KycVerifiedAtUtc = null;
            store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.KycSubmitted, Detail = command.DocumentType.ToString() });
            await store.SaveAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await files.DeleteAsync(front, CancellationToken.None);
            await files.DeleteAsync(back, CancellationToken.None);
            throw;
        }
        await files.DeleteAsync(oldFront, CancellationToken.None);
        await files.DeleteAsync(oldBack, CancellationToken.None);
        return await GetMineAsync(ct);
    }

    /// <summary>
    /// Xác minh nhanh chỉ bằng số CCCD (không ảnh): số được mã hóa, hồ sơ chuyển Pending để Admin duyệt như luồng có ảnh.
    /// Ảnh cũ (nếu còn sót) bị xóa để hồ sơ chỉ còn đúng thông tin vừa gửi.
    /// </summary>
    public async Task<KycStatusDto> SubmitCitizenIdAsync(string? documentNumber, CancellationToken ct)
    {
        var userId = UserId();
        var number = DocumentNumber(KycDocumentType.CitizenId, documentNumber);
        string? oldFront, oldBack;
        await using (var tx = await store.BeginAsync(ct))
        {
            var user = await store.FindUserAsync(userId, true, ct) ?? throw new AuthenticationException("Tài khoản không hợp lệ.");
            EnsureCanSubmit(user);
            oldFront = user.KycFrontImageUrl;
            oldBack = user.KycBackImageUrl;
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
        await files.DeleteAsync(oldFront, CancellationToken.None);
        await files.DeleteAsync(oldBack, CancellationToken.None);
        return await GetMineAsync(ct);
    }

    public async Task<byte[]> ReadMyImageAsync(KycImageSide side, CancellationToken ct)
        => await ReadImageAsync(UserId(), side, ct);

    // ===== Admin =====
    public async Task<IReadOnlyList<AdminKycItem>> ListPendingAsync(int page, CancellationToken ct)
    {
        if (page < 1 || page > int.MaxValue / PageSize) throw new ValidationException("page không hợp lệ.");
        var rows = await kyc.ListPendingAsync(page, PageSize, ct);
        return rows.Select(r => new AdminKycItem(r.UserId, r.FullName, r.DocumentType?.ToString() ?? "", Mask(r.NumberEncrypted) ?? "", r.SubmittedAtUtc)).ToList();
    }

    public async Task<byte[]> ReadImageForReviewAsync(int userId, KycImageSide side, CancellationToken ct)
    {
        if (userId == UserId()) throw new ForbiddenException("Không thể tự duyệt hồ sơ định danh của chính mình.");
        return await ReadImageAsync(userId, side, ct);
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
        string? front, back;
        await using (var tx = await store.BeginAsync(ct))
        {
            var user = await PendingUserAsync(userId, ct);
            front = user.KycFrontImageUrl;
            back = user.KycBackImageUrl;
            user.KycStatus = KycStatus.Rejected;
            user.KycNumberEncrypted = null;
            user.KycFrontImageUrl = null;
            user.KycBackImageUrl = null;
            store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.KycRejected, Detail = reason });
            await store.SaveAsync(ct);
            await tx.CommitAsync(ct);
        }
        await files.DeleteAsync(front, CancellationToken.None);
        await files.DeleteAsync(back, CancellationToken.None);
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

    private static void Image(KycImageUpload image, string side)
    {
        if (image.Length <= 0 || image.Length > MaxImageBytes)
            throw new ValidationException($"Ảnh {side} phải có dung lượng từ 1 byte đến 5 MB.");
    }

    private async Task<User> PendingUserAsync(int userId, CancellationToken ct)
    {
        var user = await store.FindUserAsync(userId, true, ct) ?? throw new NotFoundException("Người dùng", userId);
        if (user.KycStatus != KycStatus.Pending) throw new ConflictException("Hồ sơ định danh không ở trạng thái chờ duyệt.");
        return user;
    }

    private async Task<byte[]> ReadImageAsync(int userId, KycImageSide side, CancellationToken ct)
    {
        var user = await store.FindUserAsync(userId, false, ct) ?? throw new NotFoundException("Người dùng", userId);
        var key = side == KycImageSide.Front ? user.KycFrontImageUrl : user.KycBackImageUrl;
        return await files.ReadAsync(key, ct) ?? throw new NotFoundException("Ảnh giấy tờ", side.ToString());
    }

    private int UserId() => current.UserId ?? throw new AuthenticationException("Chưa đăng nhập.");
}
