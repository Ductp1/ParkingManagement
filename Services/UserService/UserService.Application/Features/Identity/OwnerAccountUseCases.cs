using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Identity;

// ===== DTO (khớp AdminService IssueOwnerAccountCommand / IssuedOwnerAccountDto) =====

/// <summary>U2 – Admin cấp tài khoản chủ bãi từ hồ sơ đối tác đã thẩm định (US-116 T6).</summary>
/// <summary>
/// Body U2 (khớp AdminService IssueOwnerAccountCommand). ApplicantUserId có giá trị = hồ sơ do tài khoản đã đăng nhập nộp (US-053)
/// → thêm role chủ bãi cho chính tài khoản đó; null = đối tác nộp không cần tài khoản / Admin nhập (US-116) → tạo tài khoản mới
/// + gửi link kích hoạt.
/// </summary>
public sealed record IssueOwnerAccountRequest(int PartnerApplicationId, string? FullName, string? Email, string? Phone,
    string? BusinessName, string? TaxCode, string? BusinessAddress, string? BankBin, string? BankName, string? BankAccountNumber,
    string? BankAccountName, int? ApplicantUserId = null);

public sealed record IssuedOwnerAccount(int UserId, int OwnerProfileId);

/// <summary>U4 – thông tin hiển thị trên trang kích hoạt (không có dữ liệu nhạy cảm ngoài tên + email của chính người nhận link).</summary>
public sealed record OwnerActivationInfo(string FullName, string Email, string BusinessName, DateTime ExpiresAtUtc);

/// <summary>U5 – chủ bãi đặt mật khẩu bằng link kích hoạt.</summary>
public sealed record ActivateOwnerRequest(string? Token, string? Password);

/// <summary>Thời hạn link kích hoạt ("OwnerActivation:TtlHours", mặc định 72 giờ – OWNER_ACTIVATION_TTL_HOURS của US-116).</summary>
public sealed record OwnerActivationOptions(TimeSpan Ttl)
{
    public static readonly OwnerActivationOptions Default = new(TimeSpan.FromHours(72));
}

// ===== PORT =====

public interface IOwnerAccountStore
{
    /// <summary>Hồ sơ chủ bãi có mã số thuế này (không tracking, kèm User).</summary>
    Task<OwnerProfile?> FindByTaxCodeAsync(string taxCode, CancellationToken ct);

    /// <summary>Hồ sơ chủ bãi có tracking, kèm User.</summary>
    Task<OwnerProfile?> FindProfileAsync(int ownerProfileId, CancellationToken ct);

    /// <summary>Link kích hoạt theo SHA-256 của token (tracking, kèm User + OwnerProfile).</summary>
    Task<OtpCode?> FindActivationAsync(string tokenHash, CancellationToken ct);

    /// <summary>Các link kích hoạt chưa dùng của một user (tracking) – để thu hồi khi cấp link mới / đã kích hoạt.</summary>
    Task<IReadOnlyList<OtpCode>> OpenActivationsAsync(int userId, CancellationToken ct);
}

// ===== USE CASE =====

/// <summary>
/// US-116 – tài khoản chủ bãi do Admin cấp sau khi thẩm định hồ sơ đối tác (AdminService), chủ bãi tự đặt mật khẩu:
/// U2 IssueAsync (T6) · U3 ResendActivationAsync (T7) · U4 ValidateActivationAsync · U5 ActivateAsync (T8).
/// Không gửi mật khẩu qua email: chỉ gửi link một lần, token 32 byte ngẫu nhiên, lưu SHA-256 trong OtpCodes (Purpose = OwnerActivation),
/// token đi trong fragment của link (#token=) và header X-Activation-Token, không nằm trong query string / log.
/// </summary>
public sealed class OwnerAccountUseCases(IIdentityStore store, IOwnerAccountStore owners, IPasswordHasher passwords,
    IOtpDeliveryConfiguration delivery, ISecretCipher cipher, OwnerActivationOptions options, TimeProvider clock)
{
    public const string ActivationEvent = "OwnerActivationDeliveryRequested";
    public const string ActivationCipherPurpose = "owner-activation";
    private const string InvalidLinkMessage = "Link kích hoạt không hợp lệ, đã được dùng hoặc đã hết hạn. Liên hệ ParkMaster (1900 6868) để nhận link mới.";
    private const string DuplicateMessage = "Email, số điện thoại hoặc mã số thuế đã thuộc một tài khoản khác.";

    /// <summary>
    /// U2: tạo User LotOwner (PendingVerification, chưa có mật khẩu dùng được) + OwnerProfile (doanh nghiệp + tài khoản nhận tiền)
    /// rồi gửi link kích hoạt. Gọi lại cho cùng hồ sơ (cùng email + MST, tài khoản chưa kích hoạt) trả lại đúng tài khoản đã tạo
    /// thay vì tạo trùng – AdminService thử lại an toàn với cùng Idempotency-Key.
    /// </summary>
    public async Task<IssuedOwnerAccount> IssueAsync(IssueOwnerAccountRequest request, CancellationToken ct)
    {
        if (request is null) throw new ValidationException("Thiếu dữ liệu tài khoản chủ bãi.");
        if (request.PartnerApplicationId <= 0) throw new ValidationException("Mã hồ sơ đối tác không hợp lệ.");
        var name = IdentityRules.Name(request.FullName ?? "", 150);
        var email = IdentityRules.Contact(request.Email ?? "");
        if (!email.Contains('@')) throw new ValidationException("Email không hợp lệ.");
        var phone = IdentityRules.Contact(request.Phone ?? "");
        if (phone.Contains('@')) throw new ValidationException("Số điện thoại không hợp lệ.");
        var business = IdentityRules.Name(request.BusinessName ?? "", 200);
        var taxCode = Required(request.TaxCode, "Mã số thuế", 20);
        var bank = (Bin: Required(request.BankBin, "Mã BIN ngân hàng", 10), Name: Required(request.BankName, "Tên ngân hàng", 100),
            Number: Required(request.BankAccountNumber, "Số tài khoản", 30), Holder: Required(request.BankAccountName, "Chủ tài khoản", 200));
        delivery.EnsureDestination(email);
        delivery.EnsureAvailable();

        await using var tx = await store.BeginAsync(ct);
        var byEmail = await store.FindContactAsync(email, ct);
        var byPhone = await store.FindContactAsync(phone, ct);
        var byTax = await owners.FindByTaxCodeAsync(taxCode, ct);
        if (byEmail is not null && byTax is not null && byTax.UserId == byEmail.Id && byEmail.Status == UserStatus.PendingVerification
            && (byPhone is null || byPhone.Id == byEmail.Id))
        {
            await tx.CommitAsync(ct);
            return new IssuedOwnerAccount(byEmail.Id, byTax.Id);       // gọi lại cùng hồ sơ – không tạo trùng
        }
        if (byEmail is not null || byPhone is not null || byTax is not null) throw new ConflictException(DuplicateMessage);

        var user = new User
        {
            FullName = name,
            Email = email,
            PhoneNumber = phone,
            Status = UserStatus.PendingVerification,
            // Mật khẩu ngẫu nhiên không ai biết: tài khoản chỉ đăng nhập được sau khi chủ bãi tự đặt mật khẩu qua link.
            PasswordHash = passwords.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
        };
        user.Roles.Add(new UserRole { Role = UserRoleType.LotOwner });
        user.OwnerProfile = new OwnerProfile
        {
            BusinessName = business,
            TaxCode = taxCode,
            BusinessAddress = string.IsNullOrWhiteSpace(request.BusinessAddress) ? null : request.BusinessAddress.Trim(),
            BankBin = bank.Bin,
            BankName = bank.Name,
            BankAccountNumber = bank.Number,
            BankAccountName = bank.Holder,
        };
        store.Add(user);
        await store.SaveAsync(ct);
        store.Event(new UserRegistered(user.Id, user.FullName, user.Email, user.PhoneNumber));
        store.Event(new OwnerProfileCreated(user.OwnerProfile.Id, user.Id, business));
        await IssueActivationAsync(user, business, ct);
        await tx.CommitAsync(ct);
        return new IssuedOwnerAccount(user.Id, user.OwnerProfile.Id);
    }

    /// <summary>U3: thu hồi link cũ, gửi link mới. Tài khoản đã kích hoạt → 409; không có hồ sơ → 404.</summary>
    public async Task ResendActivationAsync(int ownerProfileId, CancellationToken ct)
    {
        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId không hợp lệ.");
        await using var tx = await store.BeginAsync(ct);
        var profile = await owners.FindProfileAsync(ownerProfileId, ct) ?? throw new NotFoundException("Hồ sơ chủ bãi", ownerProfileId);
        var user = profile.User;
        if (user.Status != UserStatus.PendingVerification) throw new ConflictException("Tài khoản chủ bãi đã được kích hoạt.");
        if (string.IsNullOrEmpty(user.Email)) throw new ConflictException("Tài khoản chủ bãi chưa có email nhận link kích hoạt.");
        delivery.EnsureDestination(user.Email);
        delivery.EnsureAvailable();
        await RevokeOpenActivationsAsync(user.Id, ct);
        await IssueActivationAsync(user, profile.BusinessName, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>U4: kiểm tra link trước khi hiện form đặt mật khẩu.</summary>
    public async Task<OwnerActivationInfo> ValidateActivationAsync(string? token, CancellationToken ct)
    {
        var activation = await OpenActivationAsync(token, ct);
        var user = activation.User!;
        return new OwnerActivationInfo(user.FullName, user.Email!, user.OwnerProfile?.BusinessName ?? "", activation.ExpiresAtUtc);
    }

    /// <summary>U5 (T8): đặt mật khẩu, tài khoản Active, link hết hiệu lực. Sau đó chủ bãi đăng nhập bằng email + mật khẩu vừa đặt.</summary>
    public async Task ActivateAsync(ActivateOwnerRequest request, CancellationToken ct)
    {
        IdentityRules.Password(request?.Password ?? "");
        await using var tx = await store.BeginAsync(ct);
        var activation = await OpenActivationAsync(request!.Token, ct);
        var user = await store.FindUserAsync(activation.UserId!.Value, true, ct) ?? throw new ValidationException(InvalidLinkMessage);
        if (user.Status != UserStatus.PendingVerification) throw new ValidationException(InvalidLinkMessage);

        user.PasswordHash = passwords.Hash(request.Password!);
        user.Status = UserStatus.Active;
        user.EmailConfirmed = true;
        await RevokeOpenActivationsAsync(user.Id, ct);
        store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.AccountActivated, Detail = "Chủ bãi kích hoạt tài khoản qua link" });
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
    }

    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<OtpCode> OpenActivationAsync(string? token, CancellationToken ct)
    {
        token = token?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length > 128) throw new ValidationException(InvalidLinkMessage);
        var activation = await owners.FindActivationAsync(HashToken(token), ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (activation is null || activation.Purpose != OtpPurpose.OwnerActivation || activation.ConsumedAtUtc is not null
            || activation.ExpiresAtUtc <= now || activation.User is not { Status: UserStatus.PendingVerification, IsDeleted: false })
            throw new ValidationException(InvalidLinkMessage);
        return activation;
    }

    private async Task RevokeOpenActivationsAsync(int userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var open in await owners.OpenActivationsAsync(userId, ct)) open.ConsumedAtUtc = now;
    }

    /// <summary>Token mới + outbox gửi email cùng transaction; token chỉ nằm (đã mã hóa) trong outbox, DB giữ SHA-256.</summary>
    private async Task IssueActivationAsync(User user, string businessName, CancellationToken ct)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expires = clock.GetUtcNow().UtcDateTime.Add(options.Ttl);
        var activation = new OtpCode
        {
            UserId = user.Id,
            Destination = user.Email!,
            CodeHash = HashToken(token),
            Purpose = OtpPurpose.OwnerActivation,
            ExpiresAtUtc = expires,
        };
        store.Add(activation);
        await store.SaveAsync(ct);
        store.Add(new OutboxMessage
        {
            EventType = ActivationEvent,
            PayloadJson = JsonSerializer.Serialize(new
            {
                ActivationId = activation.Id, UserId = user.Id, Destination = user.Email, user.FullName, BusinessName = businessName,
                ExpiresAtUtc = expires, EncryptedToken = cipher.Encrypt(token, ActivationCipherPurpose),
            }),
        });
        await store.SaveAsync(ct);
    }

    private static string Required(string? value, string field, int max)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0) throw new ValidationException($"{field} không được để trống.");
        if (text.Length > max) throw new ValidationException($"{field} tối đa {max} ký tự.");
        return text;
    }
}
