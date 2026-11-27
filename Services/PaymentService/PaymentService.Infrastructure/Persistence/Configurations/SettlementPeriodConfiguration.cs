using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

/// <summary>US-080: lịch kỳ quyết toán dùng chung – mỗi ngày bắt đầu một kỳ (job chạy lặp / 2 instance → 23505, bỏ qua).</summary>
internal sealed class SettlementPeriodConfiguration : IEntityTypeConfiguration<SettlementPeriod>
{
    public void Configure(EntityTypeBuilder<SettlementPeriod> e)
    {
        e.ToTable("SettlementPeriods", t => t.HasCheckConstraint("CK_SettlementPeriods_Period", "\"PeriodEnd\" >= \"PeriodStart\""));
        e.Property(x => x.Cycle).HasMaxLength(20).IsRequired();
        e.Ignore(x => x.CutoffUtc);
        e.HasIndex(x => x.PeriodStart).IsUnique();
    }
}
