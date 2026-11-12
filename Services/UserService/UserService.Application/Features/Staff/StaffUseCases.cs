using System.Security.Cryptography;
using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Staff;

// ===== DTO =====
public sealed record CreateStaffRequest(string FullName, string Contact, int ParkingLotId);
public sealed record StaffItem(int Id, int UserId, string FullName, int ParkingLotId, string Status);
public sealed record OwnerLot(int Id, string? Name);

// ===== PORT =====
/// <summary>Danh sách bãi của chủ bãi, lấy từ ParkingService. Lỗi kết nối → DependencyUnavailableException.</summary>
public interface IOwnerLotDirectory
{
    Task<IReadOnlyList<OwnerLot>> GetLotsAsync(int ownerProfileId, CancellationToken ct);
}

public interface IStaffStore
{
    Task<IReadOnlyList<StaffItem>> ListActiveAsync(int ownerProfileId, int page, int pageSize, CancellationToken ct);
    /// <summary>Mọi phân công (còn hiệu lực hoặc đã gỡ) của một tài khoản.</summary>
    Task<IReadOnlyList<StaffAssignment>> AssignmentsOfUserAsync(int staffUserId, CancellationToken ct);
    /// <summary>Phân công thuộc đúng tenant, có tracking để cập nhật.</summary>
    Task<StaffAssignment?> FindAssignmentAsync(int id, int ownerProfileId, CancellationToken ct);
}

// ===== USE CASE (US-075) =====
public sealed class StaffUseCases(IIdentityStore store, IStaffStore staff, IOwnerLotDirectory lots, ICurrentUser current,
    IPasswordHasher passwords, IOtpDeliveryConfiguration delivery, ISecretCipher cipher, TimeProvider clock)
{
    public const int PageSize = 20;
    public const string CredentialsCipherPurpose = "staff-credentials";
    public const string CredentialsEvent = "StaffCredentialsDeliveryRequested";
    /// <summary>Bỏ ký tự dễ nhầm (0/O, 1/l/I) để nhân viên gõ lại mật khẩu tạm không bị sai.</summary>
    private const string PasswordLetters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";
    private const string PasswordDigits = "23456789";

    public Task<IReadOnlyList<StaffItem>> ListAsync(int page, CancellationToken ct)
    {
        var (ownerId, _) = Owner();
        if (page < 1 || page > int.MaxValue / PageSize) throw new ValidationException("page không hợp lệ.");
        return staff.ListActiveAsync(ownerId, page, PageSize, ct);
    }

    public Task<IReadOnlyList<OwnerLot>> LotsAsync(CancellationToken ct) => lots.GetLotsAsync(Owner().OwnerProfileId, ct);

    /// <summary>
    /// Tạo tài khoản Staff mới, hoặc gán thêm bãi cho nhân viên đã thuộc chính chủ bãi này.
    /// Một tài khoản Staff chỉ thuộc một tenant để token có đúng một OwnerProfileId.
    /// </summary>
    public async Task<StaffItem> AssignAsync(CreateStaffRequest input, CancellationToken ct)
    {
        var (ownerId, ownerUserId) = Owner();
        var contact = IdentityRules.Contact(input.Contact);
        var name = IdentityRules.Name(input.FullName);
        if (input.ParkingLotId <= 0) throw new ValidationException("Mã bãi xe không hợp lệ.");

        // Gọi ParkingService trước khi mở transaction để không giữ khóa DB trong lúc chờ mạng.
        var ownedLots = await lots.GetLotsAsync(ownerId, ct);
        if (ownedLots.All(lot => lot.Id != input.ParkingLotId))
            throw new ForbiddenException("Bãi xe không thuộc quyền quản lý của bạn.");

        await using var tx = await store.BeginAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var owner = await store.FindUserAsync(ownerUserId, true, ct);
        if (owner?.OwnerProfile is not { IsLocked: false, Status: OwnerStatus.Active } profile || profile.Id != ownerId)
            throw new ForbiddenException("Hồ sơ chủ bãi không còn hoạt động.");
        IdentityRules.CanAuthenticate(owner, now);

        var existing = await store.FindContactAsync(contact, ct);
        StaffAssignment assignment;
        User user;
        if (existing is null)
        {
            // Tài khoản mới: gửi email gồm tên đăng nhập + mật khẩu tạm, nên bắt buộc là email.
            if (!contact.Contains('@'))
                throw new ValidationException("Nhập email của nhân viên để hệ thống gửi tài khoản đăng nhập.");
            delivery.EnsureDestination(contact);
            delivery.EnsureAvailable();
            // Email do chủ bãi cung cấp; nhân viên nhận được mật khẩu tạm qua chính hộp thư đó nên coi như đã xác minh.
            var temporaryPassword = TemporaryPassword();
            user = new User
            {
                FullName = name,
                Email = contact,
                EmailConfirmed = true,
                Status = UserStatus.Active,
                PasswordHash = passwords.Hash(temporaryPassword)
            };
            user.Roles.Add(new UserRole { Role = UserRoleType.Staff });
            assignment = new StaffAssignment { StaffUser = user, OwnerProfileId = ownerId, ParkingLotId = input.ParkingLotId };
            store.Add(assignment);
            await store.SaveAsync(ct);
            store.Event(new UserRegistered(user.Id, user.FullName, user.Email, user.PhoneNumber));
            // Outbox cùng transaction; mật khẩu tạm được mã hóa, không ghi bản rõ vào DB/log.
            store.Add(new OutboxMessage { EventType = CredentialsEvent, PayloadJson = JsonSerializer.Serialize(new
            {
                UserId = user.Id, Destination = contact, FullName = name, OwnerName = profile.BusinessName,
                LotName = ownedLots.First(lot => lot.Id == input.ParkingLotId).Name ?? $"Bãi #{input.ParkingLotId}",
                EncryptedPassword = cipher.Encrypt(temporaryPassword, CredentialsCipherPurpose)
            }) });
            await store.SaveAsync(ct);
        }
        else
        {
            user = await store.FindUserAsync(existing.Id, true, ct)
                ?? throw new ConflictException("Email/SĐT đã được sử dụng.");
            var history = await staff.AssignmentsOfUserAsync(user.Id, ct);
            // Không được "chiếm" tài khoản tài xế/chủ bãi hoặc nhân viên của chủ bãi khác.
            if (user.Roles.Any(r => r.Role != UserRoleType.Staff) || history.Count == 0
                || history.Any(a => a.OwnerProfileId != ownerId))
                throw new ConflictException("Email/SĐT đã được sử dụng.");
            if (history.Any(a => a.IsActive && a.ParkingLotId == input.ParkingLotId))
                throw new ConflictException("Nhân viên đã được phân công tại bãi này.");
            assignment = new StaffAssignment { StaffUserId = user.Id, OwnerProfileId = ownerId, ParkingLotId = input.ParkingLotId };
            store.Add(assignment);
            await store.SaveAsync(ct);
        }

        await tx.CommitAsync(ct);
        return new StaffItem(assignment.Id, user.Id, user.FullName, assignment.ParkingLotId, user.Status.ToString());
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
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Mật khẩu tạm 10 ký tự, luôn có chữ và số (đúng quy tắc mật khẩu của hệ thống).</summary>
    public static string TemporaryPassword()
    {
        while (true)
        {
            var all = PasswordLetters + PasswordDigits;
            var chars = Enumerable.Range(0, 10).Select(_ => all[RandomNumberGenerator.GetInt32(all.Length)]).ToArray();
            if (chars.Any(char.IsLetter) && chars.Any(char.IsDigit)) return new string(chars);
        }
    }

    private (int OwnerProfileId, int UserId) Owner()
        => current.OwnerProfileId is { } ownerId && current.UserId is { } userId
            ? (ownerId, userId)
            : throw new ForbiddenException("Chỉ chủ bãi được quản lý nhân viên.");
}
