using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Configurations;

internal sealed class LotOperatingHourConfiguration : IEntityTypeConfiguration<LotOperatingHour>
{
    public void Configure(EntityTypeBuilder<LotOperatingHour> e)
    {
        e.ToTable("LotOperatingHours");
        e.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(10);
        e.HasOne(x => x.ParkingLot).WithMany(l => l.OperatingHours).HasForeignKey(x => x.ParkingLotId);
        e.HasIndex(x => new { x.ParkingLotId, x.DayOfWeek }).IsUnique();
    }
}

internal sealed class ClosureScheduleConfiguration : IEntityTypeConfiguration<ClosureSchedule>
{
    public void Configure(EntityTypeBuilder<ClosureSchedule> e)
    {
        e.ToTable("ClosureSchedules", t => t.HasCheckConstraint("CK_ClosureSchedules_Period", "\"EndsAtUtc\" IS NULL OR \"EndsAtUtc\" > \"StartsAtUtc\""));
        e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        e.HasOne(x => x.ParkingLot).WithMany().HasForeignKey(x => x.ParkingLotId);
        e.HasIndex(x => new { x.ParkingLotId, x.StartsAtUtc, x.EndsAtUtc });   // bãi/khu có đang đóng khi đặt chỗ không
        e.HasIndex(x => new { x.TargetType, x.TargetId });
    }
}

internal sealed class LotAmenityConfiguration : IEntityTypeConfiguration<LotAmenity>
{
    public void Configure(EntityTypeBuilder<LotAmenity> e)
    {
        e.ToTable("LotAmenities");
        e.Property(x => x.Code).HasMaxLength(40).IsRequired();
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.Property(x => x.Note).HasMaxLength(200);
        e.HasOne(x => x.ParkingLot).WithMany(l => l.Amenities).HasForeignKey(x => x.ParkingLotId);
        e.HasIndex(x => new { x.ParkingLotId, x.Code }).IsUnique();
        e.HasIndex(x => x.Code);                                              // lọc "bãi có mái che"
    }
}

internal sealed class LotPhotoConfiguration : IEntityTypeConfiguration<LotPhoto>
{
    public void Configure(EntityTypeBuilder<LotPhoto> e)
    {
        e.ToTable("LotPhotos");
        e.Property(x => x.Url).HasMaxLength(512).IsRequired();
        e.Property(x => x.Caption).HasMaxLength(200);
        e.HasOne(x => x.ParkingLot).WithMany(l => l.Photos).HasForeignKey(x => x.ParkingLotId);
        e.HasIndex(x => new { x.ParkingLotId, x.SortOrder });
        // Mỗi bãi chỉ có 1 ảnh bìa.
        e.HasIndex(x => x.ParkingLotId).IsUnique().HasFilter("\"IsCover\" = true").HasDatabaseName("UX_LotPhotos_OneCover");
    }
}

internal sealed class LotIntegrationConfiguration : IEntityTypeConfiguration<LotIntegration>
{
    public void Configure(EntityTypeBuilder<LotIntegration> e)
    {
        e.ToTable("LotIntegrations");
        e.Property(x => x.ProviderName).HasMaxLength(100).IsRequired();
        e.Property(x => x.ApiKeyPrefix).HasMaxLength(12).IsRequired();
        e.Property(x => x.ApiKeyHash).HasMaxLength(200).IsRequired();
        e.Property(x => x.WebhookUrl).HasMaxLength(512);
        e.Property(x => x.WebhookSecretHash).HasMaxLength(200);
        e.Property(x => x.LastError).HasMaxLength(1000);
        e.HasOne(x => x.ParkingLot).WithMany().HasForeignKey(x => x.ParkingLotId);
        e.HasIndex(x => x.ParkingLotId).IsUnique();
        e.HasIndex(x => x.ApiKeyPrefix).IsUnique();                          // tra API key nhanh khi PMS gọi vào
    }
}

internal sealed class SlotStateLogConfiguration : IEntityTypeConfiguration<SlotStateLog>
{
    public void Configure(EntityTypeBuilder<SlotStateLog> e)
    {
        e.ToTable("SlotStateLogs");
        e.Property(x => x.Reason).HasMaxLength(500);
        e.HasOne(x => x.Slot).WithMany(s => s.StateLogs).HasForeignKey(x => x.SlotId);
        e.HasIndex(x => new { x.SlotId, x.CreatedAtUtc });
        e.HasIndex(x => new { x.Source, x.CreatedAtUtc });
    }
}

internal sealed class ExternalParkingLotConfiguration : IEntityTypeConfiguration<ExternalParkingLot>
{
    public void Configure(EntityTypeBuilder<ExternalParkingLot> e)
    {
        e.ToTable("ExternalParkingLots");
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.Address).HasMaxLength(500).IsRequired();
        e.Property(x => x.City).HasMaxLength(100);
        e.Property(x => x.OpeningHoursText).HasMaxLength(200);
        e.Property(x => x.Source).HasMaxLength(200);
        e.HasIndex(x => new { x.Latitude, x.Longitude });
    }
}
