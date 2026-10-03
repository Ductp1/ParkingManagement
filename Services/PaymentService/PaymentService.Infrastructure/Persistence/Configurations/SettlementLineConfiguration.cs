using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class SettlementLineConfiguration : IEntityTypeConfiguration<SettlementLine>
{
    public void Configure(EntityTypeBuilder<SettlementLine> e)
    {
        e.ToTable("SettlementLines");
        e.Property(x => x.ReferenceCode).HasMaxLength(30);
        e.HasIndex(x => x.SettlementId);
        e.HasIndex(x => x.PaymentId).HasFilter("\"PaymentId\" IS NOT NULL");
    }
}
