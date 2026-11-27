using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AdminService.Domain.Entities;

namespace AdminService.Infrastructure.Persistence.Configurations;

internal sealed class SystemConfigChangeConfiguration : IEntityTypeConfiguration<SystemConfigChange>
{
    public void Configure(EntityTypeBuilder<SystemConfigChange> e)
    {
        e.ToTable("SystemConfigChanges");
        e.Property(x => x.Value).HasMaxLength(1000).IsRequired();
        e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        e.Property(x => x.CancelReason).HasMaxLength(1000);
        e.HasOne<SystemConfig>().WithMany().HasForeignKey(x => x.SystemConfigId);
        // US-098: một tham số không có 2 thay đổi còn giá trị cùng mốc hiệu lực (chống 2 request đổi cùng lúc).
        // Thay đổi đã hủy rời khỏi index nên được đặt lại đúng mốc đó.
        e.HasIndex(x => new { x.SystemConfigId, x.EffectiveFromUtc })
            .IsUnique()
            .HasDatabaseName("IX_SystemConfigChanges_SystemConfigId_EffectiveFromUtc_Active")
            .HasFilter("\"CancelledAtUtc\" IS NULL");
    }
}
