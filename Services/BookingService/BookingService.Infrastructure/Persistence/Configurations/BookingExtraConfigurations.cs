using BookingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingService.Infrastructure.Persistence.Configurations;

internal sealed class BookingModificationConfiguration : IEntityTypeConfiguration<BookingModification>
{
    public void Configure(EntityTypeBuilder<BookingModification> e)
    {
        e.ToTable("BookingModifications");
        e.HasOne(x => x.Booking).WithMany(b => b.Modifications).HasForeignKey(x => x.BookingId);
        e.HasIndex(x => new { x.BookingId, x.CreatedAtUtc });
    }
}

internal sealed class MonthlyPassConfiguration : IEntityTypeConfiguration<MonthlyPass>
{
    public void Configure(EntityTypeBuilder<MonthlyPass> e)
    {
        e.ToTable("MonthlyPasses", t => t.HasCheckConstraint("CK_MonthlyPasses_Period", "\"ValidTo\" >= \"ValidFrom\""));
        e.Property(x => x.Code).HasMaxLength(30).IsRequired();
        e.Property(x => x.PlateNumber).HasMaxLength(15).IsRequired();
        e.Property(x => x.SlotCode).HasMaxLength(20);
        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => new { x.ParkingLotId, x.PlateNumber, x.Status });     // cổng nhận diện xe vé tháng
        // 1 Dedicated Slot chỉ thuộc 1 vé đang hiệu lực.
        e.HasIndex(x => x.SlotId).IsUnique().HasFilter("\"SlotId\" IS NOT NULL AND \"Status\" = 'Active'");
        e.HasIndex(x => x.UserId);
    }
}
