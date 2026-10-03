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
        e.ToTable("Settlements", t => t.HasCheckConstraint("CK_Settlements_Period", "[PeriodEnd] >= [PeriodStart]"));
        e.Property(x => x.CommissionRate).HasPrecision(5, 4);
        e.Property(x => x.PayoutReference).HasMaxLength(100);
        e.HasMany(x => x.Lines).WithOne(l => l.Settlement).HasForeignKey(l => l.SettlementId);
        e.HasMany(x => x.Adjustments).WithOne(a => a.Settlement).HasForeignKey(a => a.SettlementId);
        // Mỗi chủ bãi chỉ có 1 kỳ quyết toán cho mỗi tuần.
        e.HasIndex(x => new { x.OwnerProfileId, x.PeriodStart }).IsUnique();
    }
}
