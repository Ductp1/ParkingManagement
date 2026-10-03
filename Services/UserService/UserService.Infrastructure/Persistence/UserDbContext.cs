using Microsoft.EntityFrameworkCore;
using ParkingManagement.ServiceDefaults.Persistence;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence;

/// <summary>Database riêng của UserService: PM_UserDb. Tài khoản, vai trò, OTP, token, hồ sơ chủ bãi, phân công Staff.</summary>
public sealed class UserDbContext(DbContextOptions<UserDbContext> options) : ServiceDbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OwnerProfile> OwnerProfiles => Set<OwnerProfile>();
    public DbSet<StaffAssignment> StaffAssignments => Set<StaffAssignment>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<DataSubjectRequest> DataSubjectRequests => Set<DataSubjectRequest>();
}
