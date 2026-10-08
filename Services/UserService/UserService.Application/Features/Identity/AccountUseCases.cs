using System.Security.Cryptography;
using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Identity;

public sealed class AccountUseCases(IIdentityStore store, IPasswordHasher passwords, ISecretCipher cipher,
    IOtpDeliveryConfiguration delivery, TimeProvider clock)
{
    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var contact = IdentityRules.Contact(request.Contact);
        delivery.EnsureDestination(contact);
        IdentityRules.Password(request.Password);
        var name = IdentityRules.Name(request.FullName);
        if (request.Role is not (UserRoleType.Driver or UserRoleType.LotOwner))
            throw new ValidationException("Đăng ký công khai chỉ cho phép Driver hoặc LotOwner.");
        var business = request.Role == UserRoleType.LotOwner ? IdentityRules.Name(request.BusinessName ?? "", 200) : null;
        delivery.EnsureAvailable();
        await using var tx = await store.BeginAsync(ct);
        var existing = await store.FindContactAsync(contact, ct);
        if (existing is not null) { await tx.CommitAsync(ct); return; } // Không tiết lộ tài khoản đã có.
        var user = new User { FullName = name, PasswordHash = passwords.Hash(request.Password), Status = UserStatus.PendingVerification };
        if (contact.Contains('@')) user.Email = contact; else user.PhoneNumber = contact;
        user.Roles.Add(new UserRole { Role = request.Role });
        if (business is not null) user.OwnerProfile = new OwnerProfile { BusinessName = business };
        store.Add(user);
        await store.SaveAsync(ct);
        store.Event(new UserRegistered(user.Id, user.FullName, user.Email, user.PhoneNumber));
        if (user.OwnerProfile is not null) store.Event(new OwnerProfileCreated(user.OwnerProfile.Id, user.Id, business!));
        await IssueOtpAsync(user, contact, OtpPurpose.Register, ct);
        await tx.CommitAsync(ct);
    }

    public async Task RequestOtpAsync(OtpRequest request, CancellationToken ct)
    {
        if (request.Purpose != OtpPurpose.Register) throw new ValidationException("Mục đích OTP không được hỗ trợ.");
        var contact = IdentityRules.Contact(request.Contact);
        delivery.EnsureDestination(contact);
        delivery.EnsureAvailable();
        await using var tx = await store.BeginAsync(ct);
        var candidate = await store.FindContactAsync(contact, ct);
        if (candidate is null) { await tx.CommitAsync(ct); return; }
        var user = await store.FindUserAsync(candidate.Id, true, ct) ?? throw new AuthenticationException("Tài khoản không hợp lệ.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (user.IsDeleted || user.LockedUntilUtc > now || user.Status is UserStatus.Banned or UserStatus.Locked
            || (request.Purpose == OtpPurpose.Register && user.Status != UserStatus.PendingVerification)
)
        { await tx.CommitAsync(ct); return; }
        var latest = await store.LatestOtpAsync(user.Id, request.Purpose, ct);
        if (latest is not null && latest.ExpiresAtUtc > now.AddSeconds(240))
        { await tx.CommitAsync(ct); return; } // 60 giây giữa các lần gửi lại; không reset số lần nhập sai.
        await IssueOtpAsync(user, contact, request.Purpose, ct);
        await tx.CommitAsync(ct);
    }

    public async Task VerifyRegistrationAsync(VerifyOtpRequest request, CancellationToken ct)
    {
        await using var tx = await store.BeginAsync(ct);
        var user = await OtpUserAsync(request.Contact, ct);
        await VerifyCodeAsync(user, request.Code, OtpPurpose.Register, tx, ct);
        if (user.Status != UserStatus.PendingVerification) throw new AuthenticationException("Tài khoản không chờ xác minh.");
        user.Status = UserStatus.Active;
        if (user.Email is not null) user.EmailConfirmed = true; else user.PhoneConfirmed = true;
        user.LockedUntilUtc = null;
        user.LockReason = null;
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<User> OtpUserAsync(string value, CancellationToken ct)
    {
        var contact = IdentityRules.Contact(value);
        var found = await store.FindContactAsync(contact, ct) ?? throw new AuthenticationException("Mã xác minh không hợp lệ.");
        var user = await store.FindUserAsync(found.Id, true, ct) ?? throw new AuthenticationException("Mã xác minh không hợp lệ.");
        IdentityRules.CanAuthenticate(user, clock.GetUtcNow().UtcDateTime, true);
        return user;
    }

    private async Task VerifyCodeAsync(User user, string code, OtpPurpose purpose, IIdentityTransaction tx, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var otp = await store.LatestOtpAsync(user.Id, purpose, ct);
        if (otp is null || otp.ConsumedAtUtc is not null || otp.ExpiresAtUtc <= now)
            throw new AuthenticationException("Mã xác minh đã hết hạn hoặc không hợp lệ.");
        if (!IdentityRules.OtpFormat(code) || !passwords.Verify(code, otp.CodeHash))
        {
            otp.AttemptCount++;
            var previous = await store.OtpFailuresAsync(user.Id, now.AddMinutes(-15), ct);
            store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.OtpFailed });
            if (previous >= 2)
            {
                user.LockedUntilUtc = now.AddMinutes(15);
                user.LockReason = "Nhập sai OTP 3 lần.";
                otp.ConsumedAtUtc = now;
                await store.RevokeSessionsAsync(user.Id, now, ct);
                store.Event(new UserLocked(user.Id, user.LockReason));
                store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.AccountLocked, Detail = user.LockReason });
            }
            await store.SaveAsync(ct);
            await tx.CommitAsync(ct); // Giữ số lần sai/khóa ngay cả khi phản hồi HTTP là lỗi.
            throw new AuthenticationException("Mã xác minh không hợp lệ.");
        }
        otp.ConsumedAtUtc = now;
    }

    public async Task IssueOtpAsync(User user, string contact, OtpPurpose purpose, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var old in await store.PendingOtpsAsync(user.Id, purpose, ct)) old.ConsumedAtUtc = now;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var otp = new OtpCode { UserId = user.Id, Destination = contact, Purpose = purpose,
            CodeHash = passwords.Hash(code), ExpiresAtUtc = now.AddSeconds(300) };
        store.Add(otp);
        await store.SaveAsync(ct);
        // Outbox cùng transaction; code dùng để giao nhận được mã hóa, không ghi OTP thô vào DB/log.
        store.Add(new OutboxMessage { EventType = "OtpDeliveryRequested", PayloadJson = JsonSerializer.Serialize(new
        { OtpId = otp.Id, UserId = user.Id, Destination = contact, Purpose = purpose.ToString(), ExpiresAtUtc = otp.ExpiresAtUtc,
            EncryptedCode = cipher.Encrypt(code, "otp-delivery") }) });
        await store.SaveAsync(ct);
    }
}
