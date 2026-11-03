using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using AdminService.Domain.Entities;

namespace AdminService.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> e)
    {
        e.ToTable("AuditLogs");
        e.Property(x => x.SourceService).HasMaxLength(50).IsRequired();
        e.Property(x => x.Action).HasMaxLength(100).IsRequired();
        e.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
        e.Property(x => x.EntityId).HasMaxLength(50);
        e.Property(x => x.OldValuesJson).IsMaxText();
        e.Property(x => x.NewValuesJson).IsMaxText();
        e.Property(x => x.Reason).HasMaxLength(1000);
        e.Property(x => x.IpAddress).HasMaxLength(45);
        // US-100: mỗi điều kiện lọc chính có index kết thúc bằng CreatedAtUtc để lọc + sắp xếp mới nhất trước ngay trên index.
        e.HasIndex(x => new { x.CreatedAtUtc, x.Id });
        e.HasIndex(x => new { x.SourceService, x.CreatedAtUtc });
        e.HasIndex(x => new { x.Action, x.CreatedAtUtc });
        e.HasIndex(x => new { x.EntityName, x.EntityId, x.CreatedAtUtc });
        e.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        e.HasIndex(x => new { x.OwnerProfileId, x.CreatedAtUtc });
    }
}
