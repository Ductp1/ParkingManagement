using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Privacy;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Identity;

// ===== DTO =====
/// <summary>LockedUntilUtc = null là đình chỉ vô thời hạn. RowVersion (base64) lấy từ chi tiết user lúc Admin mở trang.</summary>
public sealed record SuspendUserRequest(string? Reason, DateTime? LockedUntilUtc, string? RowVersion);
public sealed record ReactivateUserRequest(string? Reason, string? RowVersion);
public sealed record EraseUserRequest(string? Reason, string? RowVersion);
public sealed record DiscardOwnerAccountRequest(string? Reason);
public sealed record UserLifecycleResult(int UserId, string Status, DateTime? LockedUntilUtc, string? RowVersion, bool Changed);

// ===== PORT =====
public interface IUserLifecycleStore
{
    /// <summary>Số Admin còn hoạt động (Active, chưa xóa, không bị khóa có hạn) ngoài user này – chặn khóa / xóa Admin cuối.</summary>
    Task<int> CountOtherActiveAdminsAsync(int excludeUserId, DateTime nowUtc, CancellationToken ct);

    /// <summary>Tài khoản đã từng đăng nhập (có refresh token) – tài khoản đã dùng thì Admin không "hủy hồ sơ" được.</summary>
    Task<bool> HasEverSignedInAsync(int userId, CancellationToken ct);
}

// ===== USE CASE =====
/// <summary>
/// US-096 / US-116 – Admin đình chỉ, mở lại, xóa (ẩn danh hóa) user và gỡ tài khoản chủ bãi chưa kích hoạt khi hủy hồ sơ đối tác.
/// AdminService là cửa vào và ghi AuditLog; ở đây ghi SecurityEvent cùng transaction với thay đổi user.
/// Đình chỉ có hạn dùng LockedUntilUtc (tự hết hạn, các luồng đăng nhập / refresh / validator đã kiểm tra sẵn), vô thời hạn dùng Status = Locked.
/// </summary>
public sealed class UserLifecycleUseCases(IIdentityStore store, IDataRequestStore requests, IOwnerAccountStore owners,
    IUserLifecycleStore lifecycle, ICurrentUser current, IPasswordHasher passwords, TimeProvider clock)
{
    public const int MaxReasonLength = 1000;

    public async Task<UserLifecycleResult> SuspendAsync(int userId, SuspendUserRequest request, CancellationToken ct)
    {
        var adminId = AdminId();
        var reason = Reason(request?.Reason);
        var now = clock.GetUtcNow().UtcDateTime;
        var until = request!.LockedUntilUtc;
        if (until is { } u && u <= now) throw new ValidationException("Thời hạn đình chỉ phải ở tương lai.");
        if (userId == adminId) throw new ConflictException("Bạn không thể đình chỉ chính tài khoản của mình.");

        await using var tx = await store.BeginAsync(ct);
        var user = await store.FindUserAsync(userId, true, ct) ?? throw new NotFoundException("Người dùng", userId);
        CheckVersion(user, request.RowVersion);
        if (user.Status is UserStatus.Banned or UserStatus.PendingVerification)
            throw new ConflictException("Chỉ đình chỉ được tài khoản đang hoạt động. Tài khoản chưa kích hoạt: hãy hủy hồ sơ đối tác hoặc xóa user.");
        if (user.Roles.Any(r => r.Role == UserRoleType.Admin) && await lifecycle.CountOtherActiveAdminsAsync(userId, now, ct) == 0)
            throw new ConflictException("Không thể đình chỉ quản trị viên cuối cùng.");

        // Gọi lại cùng tham số: không đổi gì (idempotent).
        var alreadySuspended = user.IsSuspendedAt(now) && user.LockReason == reason
            && ((until is null && user.Status == UserStatus.Locked) || (until is not null && user.Status == UserStatus.Active && user.LockedUntilUtc == until));
        if (alreadySuspended)
        {
            await tx.CommitAsync(ct);
            return Result(user, changed: false);
        }

        user.Status = until is null ? UserStatus.Locked : UserStatus.Active;
        user.LockedUntilUtc = until;
        user.LockReason = reason;
        await store.RevokeSessionsAsync(user.Id, now, ct);
        store.Add(new SecurityEvent
        {
            UserId = user.Id, EventType = SecurityEventType.AccountLocked,
            Detail = $"Admin #{adminId} đình chỉ {(until is null ? "vô thời hạn" : $"đến {until:O}")}: {reason}",
        });
        store.Event(new UserSuspended(user.Id, reason, until));
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
        return Result(user, changed: true);
    }

    public async Task<UserLifecycleResult> ReactivateAsync(int userId, ReactivateUserRequest request, CancellationToken ct)
    {
        var adminId = AdminId();
        var reason = Reason(request?.Reason);
        var now = clock.GetUtcNow().UtcDateTime;

        await using var tx = await store.BeginAsync(ct);
        var user = await store.FindUserAsync(userId, true, ct) ?? throw new NotFoundException("Người dùng", userId);
        CheckVersion(user, request!.RowVersion);
        if (user.Status is UserStatus.Banned or UserStatus.PendingVerification)
            throw new ConflictException("Chỉ mở lại được tài khoản đang bị đình chỉ.");
        if (!user.IsSuspendedAt(now))
        {
            await tx.CommitAsync(ct);
            return Result(user, changed: false);       // đã Active
        }

        user.Status = UserStatus.Active;
        user.LockedUntilUtc = null;
        user.LockReason = null;
        user.FailedLoginCount = 0;
        store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.AccountUnlocked, Detail = $"Admin #{adminId} mở lại tài khoản: {reason}" });
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
        return Result(user, changed: true);
    }

    /// <summary>
    /// Xóa user = ẩn danh hóa (giữ Id cho lịch sử). Người gọi (AdminService) đã kiểm tra các nghĩa vụ ở service khác; ở đây chỉ chặn
    /// chính mình và Admin cuối cùng. Chủ bãi: hồ sơ chuyển Terminated (khóa, gỡ nhân viên) và nhả mã số thuế để đăng ký lại được.
    /// </summary>
    public async Task<UserLifecycleResult> EraseAsync(int userId, EraseUserRequest request, CancellationToken ct)
    {
        var adminId = AdminId();
        var reason = Reason(request?.Reason);
        var now = clock.GetUtcNow().UtcDateTime;
        if (userId == adminId) throw new ConflictException("Bạn không thể xóa chính tài khoản của mình.");

        await using var tx = await store.BeginAsync(ct);
        var user = await store.FindUserAsync(userId, true, ct) ?? throw new NotFoundException("Người dùng", userId);
        CheckVersion(user, request!.RowVersion);
        if (user.Roles.Any(r => r.Role == UserRoleType.Admin) && await lifecycle.CountOtherActiveAdminsAsync(userId, now, ct) == 0)
            throw new ConflictException("Không thể xóa quản trị viên cuối cùng.");

        if (user.OwnerProfile is { } profile)
        {
            Terminate(profile, now, reason);
            profile.TaxCode = null;
        }
        await UserAnonymizer.AnonymizeAsync(user, now, $"Admin #{adminId} xóa tài khoản: {reason}", store, requests, passwords, ct);
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
        return Result(user, changed: true);
    }

    /// <summary>
    /// Hủy hồ sơ đối tác đã cấp tài khoản (US-116 U6): chỉ gỡ được tài khoản do U2 tạo mà chủ bãi CHƯA kích hoạt và chưa từng đăng nhập.
    /// Thu hồi link kích hoạt, nhả email / SĐT / mã số thuế. Gọi lại khi đã gỡ → không đổi gì (idempotent). Đã kích hoạt → 409.
    /// </summary>
    public async Task<UserLifecycleResult> DiscardOwnerAccountAsync(int ownerProfileId, DiscardOwnerAccountRequest request, CancellationToken ct)
    {
        var adminId = AdminId();
        var reason = Reason(request?.Reason);
        var now = clock.GetUtcNow().UtcDateTime;

        await using var tx = await store.BeginAsync(ct);
        var profile = await owners.FindProfileAsync(ownerProfileId, ct) ?? throw new NotFoundException("Hồ sơ chủ bãi", ownerProfileId);
        var user = profile.User;
        if (user.IsDeleted)
        {
            await tx.CommitAsync(ct);
            return Result(user, changed: false);
        }
        if (user.Status != UserStatus.PendingVerification || user.EmailConfirmed || await lifecycle.HasEverSignedInAsync(user.Id, ct))
            throw new ConflictException("Chủ bãi đã kích hoạt tài khoản. Hãy dùng Đình chỉ hoặc Xóa user thay vì hủy hồ sơ.");

        foreach (var open in await owners.OpenActivationsAsync(user.Id, ct)) open.ConsumedAtUtc = now;
        Terminate(profile, now, reason);
        profile.TaxCode = null;
        await UserAnonymizer.AnonymizeAsync(user, now, $"Admin #{adminId} hủy hồ sơ đối tác, gỡ tài khoản chưa kích hoạt: {reason}", store, requests, passwords, ct);
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
        return Result(user, changed: true);
    }

    private void Terminate(OwnerProfile profile, DateTime now, string reason)
    {
        if (profile.Status != OwnerStatus.Terminated) store.Event(new OwnerProfileLocked(profile.Id, reason));
        profile.Terminate(now);
    }

    private int AdminId() => current.UserId ?? throw new AuthenticationException("Chưa đăng nhập.");

    private static string Reason(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0) throw new ValidationException("Cần nhập lý do.");
        if (text.Length > MaxReasonLength) throw new ValidationException($"Lý do tối đa {MaxReasonLength} ký tự.");
        return text;
    }

    /// <summary>RowVersion Admin gửi lên khác bản hiện tại → có người khác vừa sửa user này → 409.</summary>
    private static void CheckVersion(User user, string? rowVersion)
    {
        if (rowVersion is null) return;
        if (rowVersion != Convert.ToBase64String(user.RowVersion))
            throw new ConflictException("Tài khoản vừa được người khác thay đổi. Hãy tải lại trang rồi thử lại.");
    }

    private static UserLifecycleResult Result(User user, bool changed)
        => new(user.Id, user.IsDeleted ? "Deleted" : user.Status.ToString(), user.LockedUntilUtc, Convert.ToBase64String(user.RowVersion), changed);
}
