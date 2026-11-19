using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Domain.Entities;

namespace UserService.Application.Features.OwnerOnboarding;

/// <summary>
/// Body U2 (AdminService gọi khi Admin duyệt hồ sơ đối tác – US-053 / US-116). Khớp
/// AdminService.Application.Features.PartnerOnboarding.IssueOwnerAccountCommand (JSON camelCase).
/// </summary>
public sealed record GrantLotOwnerRoleCommand(
    int PartnerApplicationId, string? FullName, string? Email, string? Phone,
    string? BusinessName, string? TaxCode, string? BusinessAddress,
    string? BankBin, string? BankName, string? BankAccountNumber, string? BankAccountName,
    int? ApplicantUserId);

public sealed record GrantedLotOwnerDto(int UserId, int OwnerProfileId);

public interface IGrantLotOwnerRoleUseCase
{
    Task<GrantedLotOwnerDto> ExecuteAsync(GrantLotOwnerRoleCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-053: Admin duyệt hồ sơ đối tác → CHÍNH tài khoản đã đăng nhập nộp hồ sơ được thêm role LotOwner (giữ role Driver sẵn có)
/// và có OwnerProfile (tenant) với thông tin pháp nhân + tài khoản nhận tiền. Không tạo tài khoản mới.
/// - Khóa dòng User (FOR UPDATE) trong transaction: hai lần duyệt cùng lúc không tạo 2 hồ sơ.
/// - Idempotent: tài khoản đã là chủ bãi đang hoạt động → trả lại Id cũ (AdminService thử lại sau lỗi an toàn).
/// - Mọi lý do không cấp được trả 409 (không dùng 404: AdminService hiểu 404 là "UserService chưa có endpoint").
/// - Role mới có trong token khi người dùng làm mới phiên (refresh) hoặc đăng nhập lại.
/// </summary>
public sealed class GrantLotOwnerRoleUseCase(IIdentityStore store) : IGrantLotOwnerRoleUseCase
{
    public async Task<GrantedLotOwnerDto> ExecuteAsync(GrantLotOwnerRoleCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ApplicantUserId is not { } userId || userId <= 0)
            throw new ConflictException("Hồ sơ đối tác không gắn với tài khoản người nộp (hồ sơ cũ) – không thể cấp quyền chủ bãi.");
        var businessName = Required(command.BusinessName, "Tên doanh nghiệp", 200);
        var bankBin = Required(command.BankBin, "Mã ngân hàng", 10);
        var bankName = Required(command.BankName, "Tên ngân hàng", 100);
        var bankAccountNumber = Required(command.BankAccountNumber, "Số tài khoản", 30);
        var bankAccountName = Required(command.BankAccountName, "Tên chủ tài khoản", 100);

        await using var tx = await store.BeginAsync(cancellationToken);
        var user = await store.FindUserAsync(userId, forUpdate: true, cancellationToken);
        if (user is null || user.IsDeleted)
            throw new ConflictException($"Không tìm thấy tài khoản người nộp #{userId} – không thể cấp quyền chủ bãi.");
        if (user.Status != UserStatus.Active)
            throw new ConflictException($"Tài khoản người nộp đang ở trạng thái {user.Status}, chỉ tài khoản đang hoạt động mới được cấp quyền chủ bãi.");

        var hasOwnerRole = user.Roles.Any(r => r.Role == UserRoleType.LotOwner);
        if (user.OwnerProfile is { } existing)
        {
            if (existing.IsLocked || existing.Status != OwnerStatus.Active || !hasOwnerRole)
                throw new ConflictException(
                    "Tài khoản này đã từng là chủ bãi (hồ sơ chủ bãi bị khóa hoặc đã ngừng hợp tác) – cần xử lý hồ sơ cũ trước khi cấp lại.");
            await tx.CommitAsync(cancellationToken);
            return new GrantedLotOwnerDto(user.Id, existing.Id);   // đã cấp ở lần gọi trước
        }

        if (!hasOwnerRole) user.Roles.Add(new UserRole { Role = UserRoleType.LotOwner });
        var profile = new OwnerProfile
        {
            UserId = user.Id,
            BusinessName = businessName,
            TaxCode = Optional(command.TaxCode, 20),
            BusinessAddress = Optional(command.BusinessAddress, 500),
            BankBin = bankBin,
            BankName = bankName,
            BankAccountNumber = bankAccountNumber,
            BankAccountName = bankAccountName,
            Status = OwnerStatus.Active,
        };
        user.OwnerProfile = profile;
        await store.SaveAsync(cancellationToken);
        store.Event(new OwnerProfileCreated(profile.Id, user.Id, businessName));
        await store.SaveAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new GrantedLotOwnerDto(user.Id, profile.Id);
    }

    /// <summary>
    /// Lỗi dữ liệu cũng trả 409: AdminService đổi 409 thành lỗi nghiệp vụ (thông điệp cố định), còn 400 / mã khác thành 503
    /// "UserService không phản hồi" – sai lệch hơn. Thông điệp chi tiết ở đây phục vụ log và người gọi trực tiếp API.
    /// </summary>
    private static string Required(string? value, string field, int max)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) throw new ConflictException($"{field} không được để trống.");
        if (text.Length > max) throw new ConflictException($"{field} tối đa {max} ký tự (giới hạn hồ sơ chủ bãi).");
        return text;
    }

    private static string? Optional(string? value, int max)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length > max ? text[..max] : text;
    }
}
