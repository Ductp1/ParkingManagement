using System.Security.Cryptography;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Identity;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Privacy;

/// <summary>
/// Ẩn danh hóa một tài khoản: dùng chung cho yêu cầu xóa dữ liệu cá nhân (US-007, <see cref="DataRequestUseCases"/>),
/// Admin xóa user và Admin hủy hồ sơ đối tác đã cấp tài khoản chưa kích hoạt (US-116).
/// Giữ Id để lịch sử booking / payment / audit vẫn trỏ được; giải phóng Email / SĐT (Email đổi sang địa chỉ .invalid duy nhất).
/// Không SaveChanges: người gọi tự lưu trong transaction của mình.
/// </summary>
public static class UserAnonymizer
{
    public const string ErasedName = "Người dùng đã xóa";

    public static async Task AnonymizeAsync(User user, DateTime now, string securityDetail, IIdentityStore store,
        IDataRequestStore requests, IPasswordHasher passwords, CancellationToken ct)
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
        foreach (var a in await requests.DeactivateStaffAssignmentsAsync(user.Id, now, ct))
            store.Event(new StaffAssignmentRevoked(a.StaffUserId, a.OwnerProfileId, a.ParkingLotId));
        store.Add(new SecurityEvent { UserId = user.Id, EventType = SecurityEventType.AccountLocked, Detail = securityDetail });
        // Chưa phát UserPersonalDataErased: RabbitMQ chưa có binding cho sự kiện này (publish mandatory sẽ lỗi).
        // Cần thống nhất với các service nhận trước khi bật.
    }
}
