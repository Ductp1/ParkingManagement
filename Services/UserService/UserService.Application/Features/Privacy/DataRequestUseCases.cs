using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Application.Features.Kyc;
using UserService.Application.Features.Users;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Privacy;

// ===== DTO =====
public sealed record CreateDataRequest(DataRequestType Type, string Password, string? Reason = null);
public sealed record ConfirmDataRequest(string Password);
public sealed record DataRequestItem(int Id, string Type, string Status, DateTime CreatedAtUtc, DateTime DueAtUtc,
    string? Reason = null, string? ResultNote = null, DateTime? CompletedAtUtc = null);
public sealed record AdminDataRequestItem(int Id, int UserId, string Type, string Status, DateTime CreatedAtUtc,
    DateTime DueAtUtc, DateTime? CompletedAtUtc, int? HandledByUserId, string? ResultNote);
public sealed record HandleDataRequest(string? Note);
public sealed record SecurityActivity(string Type, DateTime OccurredAtUtc, string? IpAddress);
public sealed record PersonalDataView(int Id, string FullName, string? Email, bool EmailConfirmed, string? PhoneNumber, bool PhoneConfirmed,
    string? AvatarUrl, IReadOnlyList<string> Roles, string Status, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc,
    string KycStatus, string? KycDocumentType, string? KycMaskedNumber, DateTime? KycVerifiedAtUtc,
    string? BusinessName, int ActiveSessions, IReadOnlyList<SecurityActivity> RecentSecurityEvents);

// ===== PORT =====
public interface IDataRequestStore
{
    Task<IReadOnlyList<DataRequestItem>> ListForUserAsync(int userId, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<AdminDataRequestItem>> ListAsync(DataRequestStatus? status, DataRequestType? type, int page, int pageSize, CancellationToken ct);
    /// <summary>Yêu cầu cùng loại đang chờ xử lý (Submitted/InProgress) của user, có tracking.</summary>
    Task<DataSubjectRequest?> FindOpenAsync(int userId, DataRequestType type, CancellationToken ct);
    Task<DataSubjectRequest?> FindAsync(int id, CancellationToken ct);
    Task DeactivateStaffAssignmentsAsync(int userId, DateTime now, CancellationToken ct);
    Task<int> ActiveSessionCountAsync(int userId, DateTime now, CancellationToken ct);
    Task<IReadOnlyList<SecurityActivity>> RecentSecurityEventsAsync(int userId, int take, CancellationToken ct);
}

// ===== USE CASE (US-007) =====
/// <summary>
/// Quyền dữ liệu cá nhân (Nghị định 13/2023): xuất hồ sơ và yêu cầu xóa.
/// Xóa = ẩn danh hóa tài khoản trong UserService; dữ liệu giao dịch ở service khác vẫn giữ theo quy định kế toán.
/// </summary>
public sealed class DataRequestUseCases(IIdentityStore store, IDataRequestStore requests, ICurrentUser current,
    IPasswordHasher passwords, IAvatarStorage avatars, ISecretCipher cipher, TimeProvider clock)
{
    public const int PageSize = 20;
    public const int MaxReauthFailures = 5;
    public const string ErasedName = "Người dùng đã xóa";

    public Task<IReadOnlyList<DataRequestItem>> ListMineAsync(int page, CancellationToken ct)
    {
        var userId = UserId();
        Page(page);
        return requests.ListForUserAsync(userId, page, PageSize, ct);
    }

    /// <summary>Quyền được xem: liệt kê toàn bộ dữ liệu UserService đang lưu về chính mình (số giấy tờ đã che).</summary>
    public async Task<PersonalDataView> ViewMineAsync(CancellationToken ct)
    {
        var userId = UserId();
        var user = await store.FindUserAsync(userId, false, ct) ?? throw new AuthenticationException("Tài khoản không hợp lệ.");
        var now = clock.GetUtcNow().UtcDateTime;
        return new PersonalDataView(user.Id, user.FullName, user.Email, user.EmailConfirmed, user.PhoneNumber, user.PhoneConfirmed,
            user.AvatarUrl, user.Roles.Select(r => r.Role.ToString()).ToList(), user.Status.ToString(), user.CreatedAtUtc,
            user.LastLoginAtUtc, user.KycStatus.ToString(), user.KycDocumentType?.ToString(), MaskKyc(user.KycNumberEncrypted),
            user.KycVerifiedAtUtc,
            user.OwnerProfile?.BusinessName, await requests.ActiveSessionCountAsync(userId, now, ct),
            await requests.RecentSecurityEventsAsync(userId, 10, ct));
    }

    public async Task<DataRequestItem> SubmitAsync(CreateDataRequest input, CancellationToken ct)
    {
        var userId = UserId();
        if (input.Type is not (DataRequestType.Export or DataRequestType.Deletion or DataRequestType.Correction))
            throw new ValidationException("Chỉ hỗ trợ yêu cầu xuất, sửa hoặc xóa dữ liệu.");
        var reason = Note(input.Reason);
        if (input.Type == DataRequestType.Correction && reason is null)
            throw new ValidationException("Hãy mô tả thông tin cần sửa và giá trị đúng.");
        await using var tx = await store.BeginAsync(ct);
        await ReauthenticateAsync(userId, input.Password, tx, ct);

        // Gửi lặp lại trả về yêu cầu đang chờ, không tạo trùng.
        var request = await requests.FindOpenAsync(userId, input.Type, ct);
        if (request is null)
        {
            request = new DataSubjectRequest
            {
                UserId = userId, RequestType = input.Type, Reason = reason, DueAtUtc = clock.GetUtcNow().UtcDateTime.AddHours(72)
            };
            store.Add(request);
        }
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
        return Item(request);
    }

    /// <summary>Tải bản xuất hồ sơ của chính mình; JSON không chứa hash mật khẩu hay số giấy tờ.</summary>
    public async Task<byte[]> ExportAsync(int requestId, ConfirmDataRequest input, CancellationToken ct)
    {
        var userId = UserId();
        await using var tx = await store.BeginAsync(ct);
        var user = await ReauthenticateAsync(userId, input.Password, tx, ct);
        var request = await requests.FindAsync(requestId, ct);
        if (request is null || request.UserId != userId || request.RequestType != DataRequestType.Export
            || request.Status is not (DataRequestStatus.Submitted or DataRequestStatus.Completed))
            throw new NotFoundException("Yêu cầu dữ liệu", requestId);

        var content = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Scope = "UserService profile",
            user.Id, user.FullName, user.Email, user.EmailConfirmed, user.PhoneNumber, user.PhoneConfirmed, user.AvatarUrl,
            Status = user.Status.ToString(), user.CreatedAtUtc, user.LastLoginAtUtc,
            Roles = user.Roles.Select(r => r.Role.ToString()).ToArray(),
            Kyc = new
            {
                Status = user.KycStatus.ToString(), DocumentType = user.KycDocumentType?.ToString(),
                // Bản xuất không chứa số giấy tờ đầy đủ hay ảnh: tránh lộ khi file bị chia sẻ.
                MaskedNumber = MaskKyc(user.KycNumberEncrypted), VerifiedAtUtc = user.KycVerifiedAtUtc
            },
            OwnerProfile = user.OwnerProfile is null ? null : new { user.OwnerProfile.BusinessName, user.OwnerProfile.TaxCode },
            SecurityEvents = await requests.RecentSecurityEventsAsync(userId, 50, ct)
        });
        if (request.Status == DataRequestStatus.Submitted)
        {
            request.Status = DataRequestStatus.Completed;
            request.CompletedAtUtc = clock.GetUtcNow().UtcDateTime;
        }
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
        return content;
    }

    // ===== Admin =====
    /// <summary>Admin đã sửa dữ liệu theo yêu cầu Correction → đánh dấu hoàn tất kèm ghi chú.</summary>
    public async Task<AdminDataRequestItem> CompleteCorrectionAsync(int requestId, HandleDataRequest input, CancellationToken ct)
    {
        var adminId = UserId();
        var note = Note(input.Note) ?? throw new ValidationException("Cần ghi chú nội dung đã sửa.");
        await using var tx = await store.BeginAsync(ct);
        var request = await OpenRequestAsync(requestId, ct);
        if (request.RequestType != DataRequestType.Correction) throw new ValidationException("Chỉ hoàn tất được yêu cầu sửa dữ liệu.");
        if (request.UserId == adminId) throw new ConflictException("Không thể tự xử lý yêu cầu của chính mình.");
        request.Status = DataRequestStatus.Completed;
        request.CompletedAtUtc = clock.GetUtcNow().UtcDateTime;
        request.HandledByUserId = adminId;
        request.ResultNote = note;
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
        return AdminItem(request);
    }

    public Task<IReadOnlyList<AdminDataRequestItem>> ListAllAsync(DataRequestStatus? status, DataRequestType? type, int page, CancellationToken ct)
    {
        Page(page);
        return requests.ListAsync(status, type, page, PageSize, ct);
    }

    /// <summary>Admin duyệt yêu cầu xóa → ẩn danh hóa tài khoản, thu hồi phiên, gỡ phân công Staff.</summary>
    public async Task<AdminDataRequestItem> ApproveDeletionAsync(int requestId, HandleDataRequest input, CancellationToken ct)
    {
        var adminId = UserId();
        string? avatar;
        DataSubjectRequest request;
        await using (var tx = await store.BeginAsync(ct))
        {
            request = await OpenRequestAsync(requestId, ct);
            if (request.RequestType != DataRequestType.Deletion)
                throw new ValidationException("Chỉ duyệt được yêu cầu xóa dữ liệu.");
            if (request.UserId == adminId) throw new ConflictException("Không thể tự duyệt yêu cầu xóa của chính mình.");

            var now = clock.GetUtcNow().UtcDateTime;
            var user = await store.FindUserAsync(request.UserId, true, ct);
            avatar = null;
            if (user is not null)
            {
                if (user.Roles.Any(r => r.Role == UserRoleType.Admin))
                    throw new ConflictException("Không thể ẩn danh hóa tài khoản quản trị viên.");
                if (user.OwnerProfile is { Status: not OwnerStatus.Terminated })
                    throw new ConflictException("Chủ bãi phải hoàn tất thủ tục ngừng hợp tác trước khi xóa dữ liệu.");
                avatar = user.AvatarUrl;
                await AnonymizeAsync(user, now, ct);
            }

            request.Status = DataRequestStatus.Completed;
            request.CompletedAtUtc = now;
            request.HandledByUserId = adminId;
            request.ResultNote = Note(input.Note) ?? "Đã ẩn danh hóa tài khoản.";
            await store.SaveAsync(ct);
            await tx.CommitAsync(ct);
        }
        // Chỉ dọn file sau khi commit; lỗi dọn file không làm hỏng thao tác đã thành công.
        await avatars.DeleteAsync(avatar, CancellationToken.None);
        return AdminItem(request);
    }

    public async Task<AdminDataRequestItem> RejectAsync(int requestId, HandleDataRequest input, CancellationToken ct)
    {
        var adminId = UserId();
        var reason = Note(input.Note) ?? throw new ValidationException("Cần nhập lý do từ chối.");
        await using var tx = await store.BeginAsync(ct);
        var request = await OpenRequestAsync(requestId, ct);
        request.Status = DataRequestStatus.Rejected;
        request.CompletedAtUtc = clock.GetUtcNow().UtcDateTime;
        request.HandledByUserId = adminId;
        request.ResultNote = reason;
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
        return AdminItem(request);
    }

    private async Task AnonymizeAsync(User user, DateTime now, CancellationToken ct)
    {
        user.FullName = ErasedName;
        // Bảng Users bắt buộc có Email hoặc SĐT: dùng địa chỉ .invalid (RFC 2606), không gửi được và không trùng.
        user.Email = $"deleted-{user.Id}@deleted.invalid";
        user.PhoneNumber = null;
        user.EmailConfirmed = false;
        user.PhoneConfirmed = false;
        user.PasswordHash = passwords.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        user.AvatarUrl = null;
        user.KycStatus = KycStatus.NotSubmitted;
        user.KycDocumentType = null;
        user.KycNumberEncrypted = null;
        user.KycFrontImageUrl = null;
        user.KycBackImageUrl = null;
        user.KycVerifiedAtUtc = null;
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.LockReason = null;
        user.Status = UserStatus.Locked;
        user.IsDeleted = true;
        user.DeletedAtUtc = now;

        foreach (var purpose in Enum.GetValues<OtpPurpose>())
            foreach (var otp in await store.PendingOtpsAsync(user.Id, purpose, ct))
                otp.ConsumedAtUtc = now;
        await store.RevokeSessionsAsync(user.Id, now, ct);
        await requests.DeactivateStaffAssignmentsAsync(user.Id, now, ct);
        store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.AccountLocked, Detail = "Ẩn danh hóa theo yêu cầu xóa dữ liệu." });
        // Chưa phát UserPersonalDataErased: RabbitMQ chưa có binding cho sự kiện này (publish mandatory sẽ lỗi).
        // Cần thống nhất với các service nhận trước khi bật.
    }

    /// <summary>
    /// Xác thực lại bằng mật khẩu. Sai 5 lần liên tiếp (dùng chung bộ đếm với đăng nhập) → khóa 15 phút.
    /// Bộ đếm được commit trước khi trả lỗi để không bị rollback.
    /// </summary>
    private async Task<User> ReauthenticateAsync(int userId, string? password, IIdentityTransaction tx, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var user = await store.FindUserAsync(userId, true, ct);
        if (user is not { Status: UserStatus.Active, IsDeleted: false } || user.LockedUntilUtc > now)
            throw new ForbiddenException("Xác thực lại thất bại.");
        if (string.IsNullOrEmpty(password) || Encoding.UTF8.GetByteCount(password) > 72
            || !passwords.Verify(password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxReauthFailures)
            {
                user.FailedLoginCount = 0;
                user.LockedUntilUtc = now.AddMinutes(15);
                user.LockReason = "Xác thực lại sai mật khẩu quá 5 lần.";
                await store.RevokeSessionsAsync(user.Id, now, ct);
                store.Event(new UserLocked(user.Id, user.LockReason));
                store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.AccountLocked, Detail = user.LockReason });
            }
            await store.SaveAsync(ct);
            await tx.CommitAsync(ct);
            throw new ForbiddenException("Xác thực lại thất bại.");
        }
        user.FailedLoginCount = 0;
        return user;
    }

    private async Task<DataSubjectRequest> OpenRequestAsync(int id, CancellationToken ct)
    {
        var request = await requests.FindAsync(id, ct) ?? throw new NotFoundException("Yêu cầu dữ liệu", id);
        if (request.Status is DataRequestStatus.Completed or DataRequestStatus.Rejected)
            throw new ConflictException("Yêu cầu đã được xử lý.");
        return request;
    }

    private string? MaskKyc(string? encrypted)
    {
        if (encrypted is null) return null;
        var plain = cipher.Decrypt(encrypted, KycUseCases.CipherPurpose);
        return new string('•', Math.Max(0, plain.Length - 4)) + plain[^Math.Min(4, plain.Length)..];
    }

    private int UserId() => current.UserId ?? throw new AuthenticationException("Chưa đăng nhập.");
    private static void Page(int page)
    {
        if (page < 1 || page > int.MaxValue / PageSize) throw new ValidationException("page không hợp lệ.");
    }
    private static string? Note(string? value)
    {
        var note = value?.Trim();
        if (string.IsNullOrEmpty(note)) return null;
        if (note.Length > 1000) throw new ValidationException("Ghi chú tối đa 1000 ký tự.");
        return note;
    }
    private static DataRequestItem Item(DataSubjectRequest r)
        => new(r.Id, r.RequestType.ToString(), r.Status.ToString(), r.CreatedAtUtc, r.DueAtUtc, r.Reason, r.ResultNote, r.CompletedAtUtc);
    private static AdminDataRequestItem AdminItem(DataSubjectRequest r)
        => new(r.Id, r.UserId, r.RequestType.ToString(), r.Status.ToString(), r.CreatedAtUtc, r.DueAtUtc,
            r.CompletedAtUtc, r.HandledByUserId, r.ResultNote);
}
