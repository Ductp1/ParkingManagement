using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> e)
    {
        e.ToTable("Settlements", t => t.HasCheckConstraint("CK_Settlements_Period", "\"PeriodEnd\" >= \"PeriodStart\""));
        e.Property(x => x.Code).HasMaxLength(30).IsRequired();
        e.Property(x => x.CommissionRate).HasPrecision(5, 4);
        e.Property(x => x.PayoutReference).HasMaxLength(100);
        e.Property(x => x.BlockedReasons).HasMaxLength(200);
        e.Property(x => x.RejectReason).HasMaxLength(500);
        e.Property(x => x.RowVersion).IsConcurrencyToken();          // US-080: hai Admin thao tác cùng kỳ → người sau 409
        e.HasMany(x => x.Lines).WithOne(l => l.Settlement).HasForeignKey(l => l.SettlementId);
        e.HasMany(x => x.Adjustments).WithOne(a => a.Settlement).HasForeignKey(a => a.SettlementId);
        e.HasOne(x => x.Period).WithMany().HasForeignKey(x => x.SettlementPeriodId);
        // Mỗi chủ bãi chỉ có 1 kỳ quyết toán cho mỗi ngày bắt đầu kỳ (job chạy lặp / 2 instance → 23505, bỏ qua).
        e.HasIndex(x => new { x.OwnerProfileId, x.PeriodStart }).IsUnique();
        e.HasIndex(x => x.Code).IsUnique();
        // Số âm của một kỳ chỉ được mang sang đúng 1 kỳ sau.
        e.HasIndex(x => x.CarryOverFromSettlementId).IsUnique().HasFilter("\"CarryOverFromSettlementId\" IS NOT NULL");
        e.HasIndex(x => new { x.Status, x.PeriodStart });           // danh sách Admin lọc theo trạng thái / kỳ
    }
}
