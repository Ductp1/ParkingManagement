using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.ToTable("RefreshTokens");
        e.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
        e.Property(x => x.ReplacedByTokenHash).HasMaxLength(200);
        e.Property(x => x.CreatedByIp).HasMaxLength(45);
        e.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId);
        e.HasIndex(x => x.TokenHash).IsUnique();
    }
}
