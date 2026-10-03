using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Configurations;

internal sealed class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> e)
    {
        e.ToTable("OtpCodes");
        e.Property(x => x.Destination).HasMaxLength(256).IsRequired();
        e.Property(x => x.CodeHash).HasMaxLength(200).IsRequired();
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        e.HasIndex(x => new { x.Destination, x.Purpose, x.ExpiresAtUtc });
    }
}
