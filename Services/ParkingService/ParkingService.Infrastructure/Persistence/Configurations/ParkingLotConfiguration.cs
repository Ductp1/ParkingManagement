using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Configurations;

internal sealed class ParkingLotConfiguration : IEntityTypeConfiguration<ParkingLot>
{
    public void Configure(EntityTypeBuilder<ParkingLot> e)
    {
        e.ToTable("ParkingLots", t =>
        {
            t.HasCheckConstraint("CK_ParkingLots_Slots", "\"AvailableSlots\" >= 0 AND \"AvailableSlots\" <= \"TotalSlots\"");
            t.HasCheckConstraint("CK_ParkingLots_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
            t.HasCheckConstraint("CK_ParkingLots_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
        });
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.Address).HasMaxLength(500).IsRequired();
        e.Property(x => x.City).HasMaxLength(100).IsRequired();
        e.Property(x => x.District).HasMaxLength(100);
        e.Property(x => x.Description).HasMaxLength(2000);
        e.Property(x => x.HotlinePhone).HasMaxLength(20);
        e.Property(x => x.CoverImageUrl).HasMaxLength(512);
        e.Property(x => x.RatingAverage).HasPrecision(3, 2);
        // US-069/068: emergency stop & heartbeat (column EmergencyStopReason dùng chung convention varchar(500))
        e.Property(x => x.EmergencyStopReason).HasMaxLength(500);

        e.HasMany(x => x.Zones).WithOne(z => z.ParkingLot).HasForeignKey(z => z.ParkingLotId);
        e.HasOne(x => x.CapacityConfig).WithOne(c => c.ParkingLot).HasForeignKey<LotCapacityConfig>(c => c.ParkingLotId);

        e.HasIndex(x => x.OwnerProfileId);               // lọc theo tenant
        e.HasIndex(x => x.Status);
        e.HasIndex(x => x.IsDataStale);                  // US-068: quét/đếm bãi stale nhanh
        e.HasIndex(x => new { x.Latitude, x.Longitude });
        e.HasIndex(x => new { x.City, x.District }); // tìm bãi theo khung tọa độ trước khi tính Haversine
    }
}

/// <summary>US-069: bảng audit Emergency StopLogs – mỗi lần đóng/mở khẩn cấp 1 dòng.</summary>
internal sealed class EmergencyStopLogConfiguration : IEntityTypeConfiguration<EmergencyStopLog>
{
    public void Configure(EntityTypeBuilder<EmergencyStopLog> e)
    {
        e.ToTable("EmergencyStopLogs");
        e.Property(x => x.Action).HasMaxLength(10).IsRequired();      // "Stop" | "Lift"
        e.Property(x => x.ActorRole).HasMaxLength(40).IsRequired();   // LotOwner | Staff | Admin | System
        e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        e.HasIndex(x => new { x.ParkingLotId, x.CreatedAtUtc });
    }
}
