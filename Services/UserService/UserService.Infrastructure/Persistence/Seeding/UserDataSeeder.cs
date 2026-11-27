using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Seeding;

/// <summary>6 tài khoản demo (mật khẩu Demo@123), 2 hồ sơ chủ bãi, 1 phân công Staff. Thứ tự insert khớp DemoIds.</summary>
public sealed class UserDataSeeder(ILogger<UserDataSeeder> logger) : IDataSeeder<UserDbContext>
{
    public async Task SeedAsync(UserDbContext db, CancellationToken cancellationToken)
    {
        await SeedCoreAsync(db, cancellationToken);
        await SeedExtrasAsync(db, cancellationToken);
    }

    /// <summary>Dữ liệu demo ban đầu (migration InitialCreate).</summary>
    private async Task SeedCoreAsync(UserDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(cancellationToken)) return;

        var hash = BCrypt.Net.BCrypt.HashPassword(DemoIds.Password, workFactor: 12);
        User NewUser(string name, string email, string phone, params UserRoleType[] roles)
        {
            var u = new User
            {
                FullName = name, Email = email, PhoneNumber = phone, PasswordHash = hash,
                EmailConfirmed = true, PhoneConfirmed = true, Status = UserStatus.Active
            };
            foreach (var r in roles) u.Roles.Add(new UserRole { Role = r });
            return u;
        }

        User[] users =
        [
            NewUser("Quản trị Smart Parking", "admin@smartparking.vn", "0900000001", UserRoleType.Admin),
            NewUser("Công ty Vincom Parking", "owner.vincom@smartparking.vn", "0900000002", UserRoleType.LotOwner, UserRoleType.Driver),
            NewUser("Công ty Bãi xe Tân Sơn Nhất", "owner.tsn@smartparking.vn", "0900000003", UserRoleType.LotOwner),
            NewUser("Lê Văn Cổng", "staff.vincom@smartparking.vn", "0900000004", UserRoleType.Staff),
            NewUser("Nguyễn Văn An", "driver1@smartparking.vn", "0900000005", UserRoleType.Driver),
            NewUser("Trần Thị Bình", "driver2@smartparking.vn", "0900000006", UserRoleType.Driver),
        ];
        users[4].KycStatus = KycStatus.Verified;
        users[4].KycDocumentType = KycDocumentType.DriverLicense;
        users[4].KycVerifiedAtUtc = DateTime.UtcNow.AddDays(-20);

        foreach (var u in users)
        {
            db.Users.Add(u);
            await db.SaveChangesAsync(cancellationToken);   // từng user một để Id = 1..6 đúng DemoIds
        }

        var vincom = new OwnerProfile
        {
            UserId = DemoIds.OwnerVincomUser, BusinessName = "Công ty TNHH Vincom Parking", TaxCode = "0312345678",
            BusinessAddress = "72 Lê Thánh Tôn, Q.1, TP.HCM", BankBin = "970436", BankName = "Vietcombank",
            BankAccountNumber = "0011000000001", BankAccountName = "CONG TY TNHH VINCOM PARKING"
        };
        db.OwnerProfiles.Add(vincom);
        await db.SaveChangesAsync(cancellationToken);
        db.OwnerProfiles.Add(new OwnerProfile
        {
            UserId = DemoIds.OwnerTsnUser, BusinessName = "Công ty CP Bãi xe Tân Sơn Nhất", TaxCode = "0398765432",
            BusinessAddress = "Trường Sơn, Q.Tân Bình, TP.HCM", BankBin = "970415", BankName = "VietinBank",
            BankAccountNumber = "1010000000002", BankAccountName = "CONG TY CP BAI XE TAN SON NHAT", CommissionRateOverride = 0.08m
        });
        db.StaffAssignments.Add(new StaffAssignment
        {
            StaffUserId = DemoIds.StaffVincomUser, OwnerProfile = vincom, ParkingLotId = DemoIds.LotVincom
        });
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seed UserService xong: {Users} user demo.", users.Length);
    }

    /// <summary>Dữ liệu demo cho các bảng thêm ở migration AddDocumentCoverage – mỗi bảng kiểm tra riêng nên chạy được trên DB cũ.</summary>
    private async Task SeedExtrasAsync(UserDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        if (!await db.SecurityEvents.AnyAsync(cancellationToken))
        {
            db.SecurityEvents.AddRange(
                new SecurityEvent { UserId = DemoIds.AdminUser, EventType = SecurityEventType.LoginSucceeded, Identifier = "admin@smartparking.vn", IpAddress = "127.0.0.1" },
                new SecurityEvent { UserId = DemoIds.Driver2User, EventType = SecurityEventType.OtpFailed, Identifier = "0900000006", IpAddress = "113.161.10.20", Detail = "Sai OTP lần 1/3" },
                new SecurityEvent { UserId = null, EventType = SecurityEventType.LoginFailed, Identifier = "khongtontai@gmail.com", IpAddress = "45.12.33.7", Detail = "Tài khoản không tồn tại" });
        }

        if (!await db.DataSubjectRequests.AnyAsync(cancellationToken))
        {
            db.DataSubjectRequests.Add(new DataSubjectRequest
            {
                UserId = DemoIds.Driver2User, RequestType = DataRequestType.Export, Status = DataRequestStatus.Submitted,
                Reason = "Tôi muốn tải về toàn bộ dữ liệu cá nhân và lịch sử gửi xe.", DueAtUtc = now.AddHours(72)
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
