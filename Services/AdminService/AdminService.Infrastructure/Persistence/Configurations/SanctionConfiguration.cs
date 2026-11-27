using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;

namespace AdminService.Infrastructure.Persistence.Configurations;

internal sealed class SanctionConfiguration : IEntityTypeConfiguration<Sanction>
{
    public void Configure(EntityTypeBuilder<Sanction> e)
    {
        e.ToTable("Sanctions");
        e.Property(x => x.Reason).HasMaxLength(Sanction.ReasonMaxLength).IsRequired();
        e.Property(x => x.RevokeReason).HasMaxLength(Sanction.ReasonMaxLength);
        e.Property(x => x.EvidenceJson).IsMaxText();
        // Chế tài có trước US-096 không cần đồng bộ sang UserService.
        e.Property(x => x.UserServiceSyncStatus).HasDefaultValue(SanctionSyncStatus.NotRequired);
        // US-097: chế tài có trước không cần đồng bộ sang ParkingService / NotificationService.
        e.Property(x => x.ParkingSyncStatus).HasDefaultValue(SanctionSyncStatus.NotRequired);
        e.Property(x => x.NotificationSyncStatus).HasDefaultValue(SanctionSyncStatus.NotRequired);
        // US-097: mỗi bãi tối đa 1 chế tài hạ hạng và 1 tạm ngừng niêm yết đang Active (chống 2 request áp cùng lúc).
        e.HasIndex(x => new { x.ParkingLotId, x.Level }, "IX_Sanctions_ParkingLotId_Level_Active")
            .IsUnique()
            .HasDatabaseName("IX_Sanctions_ParkingLotId_Level_Active")
            .HasFilter($"\"Status\" = '{nameof(SanctionStatus.Active)}' AND \"ParkingLotId\" IS NOT NULL " +
                $"AND \"Level\" IN ('{nameof(SanctionLevel.RankingPenalty)}', '{nameof(SanctionLevel.TemporarySuspension)}')");
        // US-097: mỗi chủ bãi tối đa 1 chế tài hạ hạng toàn bộ bãi đang Active.
        e.HasIndex(x => x.OwnerProfileId, "IX_Sanctions_OwnerProfileId_ActiveOwnerRankingPenalty")
            .IsUnique()
            .HasDatabaseName("IX_Sanctions_OwnerProfileId_ActiveOwnerRankingPenalty")
            .HasFilter($"\"Status\" = '{nameof(SanctionStatus.Active)}' AND \"ParkingLotId\" IS NULL " +
                $"AND \"Level\" = '{nameof(SanctionLevel.RankingPenalty)}'");
        e.HasIndex(x => new { x.OwnerProfileId, x.Status });
        e.HasIndex(x => x.ParkingLotId).HasFilter("\"ParkingLotId\" IS NOT NULL");
        // US-096: mỗi chủ bãi tối đa 1 chế tài khóa cấp chủ bãi đang hiệu lực (chống 2 request khóa cùng lúc tạo trùng).
        e.HasIndex(x => x.OwnerProfileId)
            .IsUnique()
            .HasDatabaseName("IX_Sanctions_OwnerProfileId_ActiveOwnerLock")
            .HasFilter($"\"Status\" = '{nameof(SanctionStatus.Active)}' AND \"ParkingLotId\" IS NULL " +
                $"AND \"Level\" IN ('{nameof(SanctionLevel.TemporarySuspension)}', '{nameof(SanctionLevel.PermanentBan)}')");
    }
}
