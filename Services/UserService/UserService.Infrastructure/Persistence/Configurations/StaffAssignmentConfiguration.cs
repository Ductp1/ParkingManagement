using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Configurations;

internal sealed class StaffAssignmentConfiguration : IEntityTypeConfiguration<StaffAssignment>
{
    public void Configure(EntityTypeBuilder<StaffAssignment> e)
    {
        e.ToTable("StaffAssignments");
        e.HasOne(x => x.StaffUser).WithMany(u => u.StaffAssignments).HasForeignKey(x => x.StaffUserId);
        e.HasOne(x => x.OwnerProfile).WithMany(o => o.StaffAssignments).HasForeignKey(x => x.OwnerProfileId);

        // 1 Staff chỉ có 1 phân công đang hiệu lực cho mỗi bãi.
        e.HasIndex(x => new { x.StaffUserId, x.ParkingLotId }).IsUnique().HasFilter("\"IsActive\" = true");
        e.HasIndex(x => x.ParkingLotId);
    }
}
