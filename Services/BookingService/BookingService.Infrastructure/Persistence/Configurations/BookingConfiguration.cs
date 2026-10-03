using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using BookingService.Domain.Entities;

namespace BookingService.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> e)
    {
        e.ToTable("Bookings", t =>
        {
            t.HasCheckConstraint("CK_Bookings_Time", "\"EndAtUtc\" > \"StartAtUtc\"");
            t.HasCheckConstraint("CK_Bookings_Amount", "\"TotalAmount\" >= 0 AND \"DiscountAmount\" >= 0 AND \"PaidAmount\" >= 0");
        });
        e.Property(x => x.Code).HasMaxLength(30).IsRequired();
        e.Property(x => x.PlateNumber).HasMaxLength(15).IsRequired();
        e.Property(x => x.ParkingLotName).HasMaxLength(200).IsRequired();
        e.Property(x => x.SlotCode).HasMaxLength(20);
        e.Property(x => x.PromotionCode).HasMaxLength(30);
        e.Property(x => x.QrToken).HasMaxLength(1000);
        e.Property(x => x.RowVersion).IsRowVersion();

        // ----- Quan hệ -----
        e.HasOne(x => x.PriceSnapshot).WithOne(p => p.Booking).HasForeignKey<PriceSnapshot>(p => p.BookingId);
        e.HasMany(x => x.StatusLogs).WithOne(l => l.Booking).HasForeignKey(l => l.BookingId);

        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => new { x.UserId, x.Status });                      // "Booking của tôi" + chống giữ chỗ ảo
        e.HasIndex(x => new { x.OwnerProfileId, x.ParkingLotId, x.StartAtUtc }); // danh sách booking của chủ bãi / trong ca
        e.HasIndex(x => new { x.SlotId, x.StartAtUtc, x.EndAtUtc });      // kiểm tra trùng khung giờ trên 1 slot
        e.HasIndex(x => new { x.Status, x.HoldExpiresAtUtc });            // worker giải phóng giữ chỗ 15′
        e.HasIndex(x => x.PlateNumber);                                   // cổng tra theo biển số
    }
}
