using System.Security.Cryptography;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Staff;

// ===== DTO =====
public sealed record CreateStaffRequest(string FullName, string Contact, int ParkingLotId);
/// <summary>LotStatus = null khi ParkingService không trả lời.</summary>
public sealed record StaffItem(int Id, int UserId, string FullName, int ParkingLotId, string Status, string? LotStatus = null,
    string? Email = null)
{
    public string ActivationStatus => Status == nameof(UserStatus.PendingVerification) ? "Invited" : "Active";
}
public sealed record OwnerLot(int Id, string? Name, string? Status = null);
public sealed record StaffActivationInfo(string FullName, string Email, string BusinessName, DateTime ExpiresAtUtc);
public sealed record ActivateStaffRequest(string? Token, string? Password);

// ===== PORT =====
/// <summary>Danh sách bãi của chủ bãi, lấy từ ParkingService. Lỗi kết nối → DependencyUnavailableException.</summary>
public interface IOwnerLotDirectory
{
    Task<IReadOnlyList<OwnerLot>> GetLotsAsync(int ownerProfileId, CancellationToken ct);
}

public interface IStaffStore
{
    Task<IReadOnlyList<StaffItem>> ListActiveAsync(int ownerProfileId, int? parkingLotId, int? staffUserId, int page, int pageSize,
        CancellationToken ct);
    /// <summary>Link kích hoạt nhân viên theo SHA-256 của token (tracking, kèm User).</summary>
    Task<OtpCode?> FindActivationAsync(string tokenHash, CancellationToken ct);
    /// <summary>Tên doanh nghiệp của một phân công còn hiệu lực; null = không còn phân công nào.</summary>
    Task<string?> ActiveOwnerNameAsync(int staffUserId, CancellationToken ct);
    /// <summary>Mọi phân công (còn hiệu lực hoặc đã gỡ) của một tài khoản.</summary>
    Task<IReadOnlyList<StaffAssignment>> AssignmentsOfUserAsync(int staffUserId, CancellationToken ct);
    /// <summary>Phân công thuộc đúng tenant, có tracking để cập nhật.</summary>
    Task<StaffAssignment?> FindAssignmentAsync(int id, int ownerProfileId, CancellationToken ct);
}

// ===== USE CASE (US-075) =====
/// <summary>
/// Chủ bãi mời / gán / gỡ nhân viên. Nhân viên mới nhận link một lần và tự đặt mật khẩu (email không chứa mật khẩu).
/// </summary>
public sealed class StaffUseCases(IIdentityStore store, IStaffStore staff, IOwnerLotDirectory lots, ICurrentUser current,
    IPasswordHasher passwords, IOtpDeliveryConfiguration delivery, ISecretCipher cipher, OwnerActivationOptions options,
    TimeProvider clock)
{
    public const int PageSize = 20;
    public const string ActivationEvent = "StaffActivationDeliveryRequested";
    public const string ActivationCipherPurpose = "staff-activation";

    /// <summary>ProblemDetails.code – FE phân biệt các trường hợp cùng HTTP status.</summary>
    public const string EmailHasAccount = "STAFF_EMAIL_HAS_ACCOUNT";
    public const string EmailOtherOwner = "STAFF_EMAIL_OTHER_OWNER";
    public const string AlreadyAssigned = "STAFF_ALREADY_ASSIGNED";
    public const string EmailRequired = "STAFF_EMAIL_REQUIRED";
    public const string AlreadyActive = "STAFF_ALREADY_ACTIVE";
    public const string ActivationInvalid = "ACTIVATION_TOKEN_INVALID";

    private const string HasAccountMessage = "Email/SĐT này thuộc tài khoản khách hàng hoặc chủ bãi. Hãy dùng email riêng cho nhân viên.";
    private const string InvalidLinkMessage = "Link kích hoạt không hợp lệ, đã được dùng hoặc đã hết hạn. Nhờ chủ bãi gửi lại lời mời.";

    public async Task<IReadOnlyList<StaffItem>> ListAsync(int page, int? parkingLotId, int? staffUserId, CancellationToken ct)
    {
        var (ownerId, _) = Owner();
        if (page < 1 || page > int.MaxValue / PageSize) throw new ValidationException("page không hợp lệ.");
        if (parkingLotId <= 0) throw new ValidationException("Mã bãi xe không hợp lệ.");
        if (staffUserId <= 0) throw new ValidationException("Mã nhân viên không hợp lệ.");
        var items = await staff.ListActiveAsync(ownerId, parkingLotId, staffUserId, page, PageSize, ct);
        if (items.Count == 0) return items;
        try
        {
            var statuses = (await lots.GetLotsAsync(ownerId, ct)).ToDictionary(lot => lot.Id, lot => lot.Status);
            return items.Select(i => i with { LotStatus = statuses.GetValueOrDefault(i.ParkingLotId) }).ToList();
        }
        catch (DependencyUnavailableException) { return items; } // Trạng thái bãi chỉ để hiển thị, không chặn danh sách.
    }

    public Task<IReadOnlyList<OwnerLot>> LotsAsync(CancellationToken ct) => lots.GetLotsAsync(Owner().OwnerProfileId, ct);

    /// <summary>
    /// Mời nhân viên mới, hoặc gán thêm bãi cho nhân viên. Tài khoản Staff đã bị gỡ hết phân công được chủ bãi khác mời lại;
    /// tại một thời điểm chỉ thuộc một tenant để token có đúng một OwnerProfileId.
    /// </summary>
    public async Task<StaffItem> AssignAsync(CreateStaffRequest input, CancellationToken ct)
    {
        var (ownerId, ownerUserId) = Owner();
        var contact = IdentityRules.Contact(input.Contact);
        var name = IdentityRules.Name(input.FullName);
        if (input.ParkingLotId <= 0) throw new ValidationException("Mã bãi xe không hợp lệ.");

        // Gọi ParkingService trước khi mở transaction để không giữ khóa DB trong lúc chờ mạng.
        var lot = (await lots.GetLotsAsync(ownerId, ct)).FirstOrDefault(l => l.Id == input.ParkingLotId)
            ?? throw new ForbiddenException("Bãi xe không thuộc quyền quản lý của bạn.");
        var lotName = lot.Name ?? $"Bãi #{lot.Id}";

        await using var tx = await store.BeginAsync(ct);
        var profile = await ActiveOwnerAsync(ownerId, ownerUserId, ct);

        var existing = await store.FindContactAsync(contact, ct);
        StaffAssignment assignment;
        User user;
        if (existing is null)
        {
            if (!contact.Contains('@'))
                throw new ValidationException("Nhập email của nhân viên để hệ thống gửi link kích hoạt.", EmailRequired);
            EnsureDelivery(contact);
            user = new User
            {
                FullName = name,
                Email = contact,
                Status = UserStatus.PendingVerification,
                // Mật khẩu ngẫu nhiên không ai biết: chỉ đăng nhập được sau khi nhân viên tự đặt qua link.
                PasswordHash = passwords.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
            };
            user.Roles.Add(new UserRole { Role = UserRoleType.Staff });
            assignment = new StaffAssignment { StaffUser = user, OwnerProfileId = ownerId, ParkingLotId = lot.Id };
            store.Add(assignment);
            await store.SaveAsync(ct);
            store.Event(new UserRegistered(user.Id, user.FullName, user.Email, user.PhoneNumber));
            await InviteAsync(user, profile.BusinessName, lotName, ct);
        }
        else
        {
            user = await store.FindUserAsync(existing.Id, true, ct)
                ?? throw new ConflictException(HasAccountMessage, EmailHasAccount);
            var history = await staff.AssignmentsOfUserAsync(user.Id, ct);
            // Không được "chiếm" tài khoản tài xế/chủ bãi hoặc nhân viên đang làm cho chủ bãi khác.
            if (user.Roles.Any(r => r.Role != UserRoleType.Staff) || history.Count == 0)
                throw new ConflictException(HasAccountMessage, EmailHasAccount);
            if (history.Any(a => a.IsActive && a.OwnerProfileId != ownerId))
                throw new ConflictException("Nhân viên này đang được chủ bãi khác phân công.", EmailOtherOwner);
            if (history.Any(a => a.IsActive && a.ParkingLotId == lot.Id))
                throw new ConflictException("Nhân viên đã được phân công tại bãi này.", AlreadyAssigned);
            // Chưa kích hoạt và được mời lại sau khi gỡ hết: link cũ thuộc lời mời trước, gửi link mới.
            var reinvite = user.Status == UserStatus.PendingVerification && history.All(a => !a.IsActive);
            if (reinvite) EnsureDelivery(user.Email!);
            assignment = new StaffAssignment { StaffUserId = user.Id, OwnerProfileId = ownerId, ParkingLotId = lot.Id };
            store.Add(assignment);
            await store.SaveAsync(ct);
            if (reinvite) await InviteAsync(user, profile.BusinessName, lotName, ct);
        }

        await tx.CommitAsync(ct);
        return new StaffItem(assignment.Id, user.Id, user.FullName, assignment.ParkingLotId, user.Status.ToString(), lot.Status, user.Email);
    }

    /// <summary>Thu hồi link cũ và gửi link mới cho nhân viên chưa kích hoạt.</summary>
    public async Task ResendInviteAsync(int assignmentId, CancellationToken ct)
    {
        var (ownerId, ownerUserId) = Owner();
        var ownedLots = await lots.GetLotsAsync(ownerId, ct);
        await using var tx = await store.BeginAsync(ct);
        var profile = await ActiveOwnerAsync(ownerId, ownerUserId, ct);
        var assignment = await staff.FindAssignmentAsync(assignmentId, ownerId, ct);
        if (assignment is not { IsActive: true }) throw new NotFoundException("Phân công nhân viên", assignmentId);
        var user = await store.FindUserAsync(assignment.StaffUserId, true, ct)
            ?? throw new NotFoundException("Phân công nhân viên", assignmentId);
        if (user.Status != UserStatus.PendingVerification)
            throw new ConflictException("Nhân viên đã kích hoạt tài khoản.", AlreadyActive);
        EnsureDelivery(user.Email!);
        await InviteAsync(user, profile.BusinessName,
            ownedLots.FirstOrDefault(l => l.Id == assignment.ParkingLotId)?.Name ?? $"Bãi #{assignment.ParkingLotId}", ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Gỡ một phân công; gọi lại vẫn thành công. Thu hồi phiên để token mới có đúng danh sách bãi.</summary>
    public async Task RevokeAsync(int assignmentId, CancellationToken ct)
    {
        var (ownerId, _) = Owner();
        await using var tx = await store.BeginAsync(ct);
        var assignment = await staff.FindAssignmentAsync(assignmentId, ownerId, ct)
            ?? throw new NotFoundException("Phân công nhân viên", assignmentId);
        if (!assignment.IsActive) { await tx.CommitAsync(ct); return; }

        // Khóa hàng User như các luồng xác thực khác để không chen giữa login/refresh.
        _ = await store.FindUserAsync(assignment.StaffUserId, true, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        assignment.IsActive = false;
        assignment.RevokedAtUtc = now;
        await store.RevokeSessionsAsync(assignment.StaffUserId, now, ct);
        store.Event(new StaffAssignmentRevoked(assignment.StaffUserId, assignment.OwnerProfileId, assignment.ParkingLotId));
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Kiểm tra link trước khi hiện form đặt mật khẩu.</summary>
    public async Task<StaffActivationInfo> ValidateActivationAsync(string? token, CancellationToken ct)
    {
        var (activation, businessName) = await OpenActivationAsync(token, ct);
        var user = activation.User!;
        return new StaffActivationInfo(user.FullName, user.Email!, businessName, activation.ExpiresAtUtc);
    }

    /// <summary>Đặt mật khẩu, tài khoản Active, mọi link kích hoạt hết hiệu lực.</summary>
    public async Task ActivateAsync(ActivateStaffRequest request, CancellationToken ct)
    {
        IdentityRules.Password(request?.Password ?? "");
        await using var tx = await store.BeginAsync(ct);
        var (activation, _) = await OpenActivationAsync(request!.Token, ct);
        var user = await store.FindUserAsync(activation.UserId!.Value, true, ct) ?? throw InvalidLink();
        // Kiểm lại sau khi khóa hàng User: gửi lại lời mời / gỡ phân công / kích hoạt song song có thể vừa chạy xong.
        var open = await store.PendingOtpsAsync(user.Id, OtpPurpose.StaffActivation, ct);
        if (user.Status != UserStatus.PendingVerification || open.All(o => o.Id != activation.Id)
            || await staff.ActiveOwnerNameAsync(user.Id, ct) is null) throw InvalidLink();

        var now = clock.GetUtcNow().UtcDateTime;
        user.PasswordHash = passwords.Hash(request.Password!);
        user.Status = UserStatus.Active;
        user.EmailConfirmed = true;
        foreach (var o in open) o.ConsumedAtUtc = now;
        store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.AccountActivated, Detail = "Nhân viên kích hoạt tài khoản qua link" });
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<(OtpCode Activation, string BusinessName)> OpenActivationAsync(string? token, CancellationToken ct)
    {
        token = token?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length > 128) throw InvalidLink();
        var activation = await staff.FindActivationAsync(OwnerAccountUseCases.HashToken(token), ct);
        if (activation is not { Purpose: OtpPurpose.StaffActivation, ConsumedAtUtc: null,
                User: { IsDeleted: false, Status: UserStatus.PendingVerification } }
            || activation.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime) throw InvalidLink();
        return (activation, await staff.ActiveOwnerNameAsync(activation.User.Id, ct) ?? throw InvalidLink());
    }

    private static ValidationException InvalidLink() => new(InvalidLinkMessage, ActivationInvalid);

    private async Task<OwnerProfile> ActiveOwnerAsync(int ownerId, int ownerUserId, CancellationToken ct)
    {
        var owner = await store.FindUserAsync(ownerUserId, true, ct);
        if (owner?.OwnerProfile is not { IsLocked: false, Status: OwnerStatus.Active } profile || profile.Id != ownerId)
            throw new ForbiddenException("Hồ sơ chủ bãi không còn hoạt động.");
        IdentityRules.CanAuthenticate(owner, clock.GetUtcNow().UtcDateTime);
        return profile;
    }

    private void EnsureDelivery(string email)
    {
        delivery.EnsureDestination(email);
        delivery.EnsureAvailable();
    }

    private async Task InviteAsync(User user, string ownerName, string lotName, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var old in await store.PendingOtpsAsync(user.Id, OtpPurpose.StaffActivation, ct)) old.ConsumedAtUtc = now;
        var expires = now.Add(options.Ttl);
        await ActivationLinks.IssueAsync(store, user, OtpPurpose.StaffActivation, expires, ActivationEvent, (activationId, token) => new
        {
            ActivationId = activationId, UserId = user.Id, Destination = user.Email, user.FullName, OwnerName = ownerName,
            LotName = lotName, ExpiresAtUtc = expires, EncryptedToken = cipher.Encrypt(token, ActivationCipherPurpose),
        }, ct);
    }

    private (int OwnerProfileId, int UserId) Owner()
        => current.OwnerProfileId is { } ownerId && current.UserId is { } userId
            ? (ownerId, userId)
            : throw new ForbiddenException("Chỉ chủ bãi được quản lý nhân viên.");
}
