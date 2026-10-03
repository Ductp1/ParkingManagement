using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VehicleService.Domain.Entities;

namespace VehicleService.Infrastructure.Persistence.Configurations;

internal sealed class VehicleShareConfiguration : IEntityTypeConfiguration<VehicleShare>
{
    public void Configure(EntityTypeBuilder<VehicleShare> e)
    {
        e.ToTable("VehicleShares", t => t.HasCheckConstraint("CK_VehicleShares_NotSelf", "[OwnerUserId] <> [SharedWithUserId]"));
        e.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId);
        // 1 xe chỉ chia sẻ 1 lần cho 1 người khi đang hiệu lực.
        e.HasIndex(x => new { x.VehicleId, x.SharedWithUserId }).IsUnique().HasFilter("[Status] <> N'Revoked'");
        e.HasIndex(x => x.SharedWithUserId);
    }
}
