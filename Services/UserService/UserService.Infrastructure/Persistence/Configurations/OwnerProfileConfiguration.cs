using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Configurations;

internal sealed class OwnerProfileConfiguration : IEntityTypeConfiguration<OwnerProfile>
{
    public void Configure(EntityTypeBuilder<OwnerProfile> e)
    {
        e.ToTable("OwnerProfiles");
        e.Property(x => x.BusinessName).HasMaxLength(200).IsRequired();
        e.Property(x => x.TaxCode).HasMaxLength(20);
        e.Property(x => x.BankBin).HasMaxLength(10).IsRequired();
        e.Property(x => x.BankName).HasMaxLength(100).IsRequired();
        e.Property(x => x.BankAccountNumber).HasMaxLength(30).IsRequired();
        e.Property(x => x.BankAccountName).HasMaxLength(100).IsRequired();
        e.Property(x => x.CommissionRateOverride).HasPrecision(5, 4);

        // 1 user ↔ tối đa 1 hồ sơ chủ bãi
        e.HasOne(x => x.User).WithOne(u => u.OwnerProfile).HasForeignKey<OwnerProfile>(x => x.UserId);
        e.HasIndex(x => x.UserId).IsUnique();
    }
}
