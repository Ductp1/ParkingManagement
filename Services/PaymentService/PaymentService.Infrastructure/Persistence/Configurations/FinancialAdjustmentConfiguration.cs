using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class FinancialAdjustmentConfiguration : IEntityTypeConfiguration<FinancialAdjustment>
{
    public void Configure(EntityTypeBuilder<FinancialAdjustment> e)
    {
        e.ToTable("FinancialAdjustments");
        e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        e.HasIndex(x => x.OwnerProfileId);
    }
}
