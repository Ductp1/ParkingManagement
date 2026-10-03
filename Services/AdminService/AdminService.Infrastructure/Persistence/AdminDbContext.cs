using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.ServiceDefaults.Persistence;

namespace AdminService.Infrastructure.Persistence;

/// <summary>Database riêng của AdminService: pm_admin. Tham số hệ thống, feature flag, chế tài SLA, audit log tập trung.</summary>
public sealed class AdminDbContext(DbContextOptions<AdminDbContext> options) : ServiceDbContext(options)
{
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<Sanction> Sanctions => Set<Sanction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RiskFlag> RiskFlags => Set<RiskFlag>();
}
