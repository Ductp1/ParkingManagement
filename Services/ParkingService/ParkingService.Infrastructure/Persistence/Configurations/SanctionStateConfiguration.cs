using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Configurations;

/// <summary>US-097: hiệu lực chế tài SLA đẩy từ AdminService. Tự đăng ký qua ApplyConfigurationsFromAssembly.</summary>
internal sealed class SanctionStateConfiguration : IEntityTypeConfiguration<SanctionState>
{
    public void Configure(EntityTypeBuilder<SanctionState> e)
    {
        e.ToTable("SanctionStates");
        // PUT idempotent theo chế tài: hai request đồng thời cùng SanctionId → request sau bị index chặn → 409.
        e.HasIndex(x => x.SanctionId).IsUnique();
        e.HasOne<ParkingLot>().WithMany().HasForeignKey(x => x.ParkingLotId);
        // Tìm kiếm: lấy hiệu lực theo danh sách bãi / chủ bãi.
        e.HasIndex(x => new { x.ParkingLotId, x.LiftedAtUtc });
        e.HasIndex(x => new { x.OwnerProfileId, x.LiftedAtUtc });
    }
}
