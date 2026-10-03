using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using GateService.Domain.Entities;

namespace GateService.Infrastructure.Persistence.Configurations;

internal sealed class ParkingSessionConfiguration : IEntityTypeConfiguration<ParkingSession>
{
    public void Configure(EntityTypeBuilder<ParkingSession> e)
    {
        e.ToTable("ParkingSessions", t =>
            t.HasCheckConstraint("CK_ParkingSessions_Exit", "[ExitAtUtc] IS NULL OR [ExitAtUtc] >= [EntryAtUtc]"));
        e.Property(x => x.Code).HasMaxLength(30).IsRequired();
        e.Property(x => x.PlateNumber).HasMaxLength(15).IsRequired();
        e.Property(x => x.BookingCode).HasMaxLength(30);
        e.Property(x => x.SlotCode).HasMaxLength(20);
        e.Property(x => x.EntryImagePath).HasMaxLength(512);
        e.Property(x => x.ExitImagePath).HasMaxLength(512);
        e.Property(x => x.EntryOcrRaw).HasMaxLength(100);
        e.Property(x => x.ExitOcrRaw).HasMaxLength(100);

        // ----- Quan hệ -----
        e.HasMany(x => x.Events).WithOne(ev => ev.ParkingSession).HasForeignKey(ev => ev.ParkingSessionId);

        e.HasIndex(x => x.Code).IsUnique();
        // Một biển số chỉ có tối đa 1 lượt đang trong bãi (giữ quy tắc của project Smart_Parking_System).
        e.HasIndex(x => new { x.ParkingLotId, x.PlateNumber }).IsUnique().HasFilter("[Status] = N'Active'");
        // Một booking chỉ sinh ra 1 lượt gửi xe.
        e.HasIndex(x => x.BookingId).IsUnique().HasFilter("[BookingId] IS NOT NULL");
        e.HasIndex(x => new { x.OwnerProfileId, x.ParkingLotId, x.EntryAtUtc });
    }
}
