using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Configurations;

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> e)
    {
        e.ToTable("UserRoles");
        e.HasKey(x => new { x.UserId, x.Role });
        e.HasOne(x => x.User).WithMany(u => u.Roles).HasForeignKey(x => x.UserId);
    }
}
