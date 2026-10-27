using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.ServiceDefaults.Persistence;

namespace AdminService.Infrastructure.Persistence;

/// <summary>Database riêng của AdminService: pm_admin. Tham số hệ thống, feature flag, chế tài SLA, audit log tập trung.</summary>
public sealed class AdminDbContext(DbContextOptions<AdminDbContext> options) : ServiceDbContext(options)
{
    // T-802: InboxReceipts (khóa chính EventId) chống ghi trùng sự kiện AuditRecorded nhận từ service khác.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureInbox();
    }

    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<SystemConfigChange> SystemConfigChanges => Set<SystemConfigChange>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<Sanction> Sanctions => Set<Sanction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RiskFlag> RiskFlags => Set<RiskFlag>();
    public DbSet<PartnerApplication> PartnerApplications => Set<PartnerApplication>();
    public DbSet<ContractTemplate> ContractTemplates => Set<ContractTemplate>();
    public DbSet<LotContract> LotContracts => Set<LotContract>();
    public DbSet<LotContractVersion> LotContractVersions => Set<LotContractVersion>();
}
