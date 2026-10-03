using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using VehicleService.Domain.Entities;

namespace VehicleService.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> e)
    {
        e.ToTable("Vehicles", t => t.HasCheckConstraint("CK_Vehicles_Height", "\"HeightCm\" > 0"));
        e.Property(x => x.PlateNumber).HasMaxLength(15).IsRequired();
        e.Property(x => x.PlateDisplay).HasMaxLength(20).IsRequired();
        e.Property(x => x.Brand).HasMaxLength(50);
        e.Property(x => x.Model).HasMaxLength(50);
        e.Property(x => x.Color).HasMaxLength(30);

        // 1 tài khoản không đăng ký trùng biển số (bỏ qua xe đã xóa mềm).
        e.HasIndex(x => new { x.UserId, x.PlateNumber }).IsUnique().HasFilter("\"IsDeleted\" = false");
        e.HasIndex(x => x.PlateNumber);
    }
}
