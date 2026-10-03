using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Configurations;

internal sealed class KybApplicationConfiguration : IEntityTypeConfiguration<KybApplication>
{
    public void Configure(EntityTypeBuilder<KybApplication> e)
    {
        e.ToTable("KybApplications");
        e.Property(x => x.BusinessLicenseUrl).HasMaxLength(512);
        e.Property(x => x.SitePhotoUrlsJson).IsMaxText();
        e.Property(x => x.FireSafetyCertificateUrl).HasMaxLength(512);
        e.Property(x => x.FieldSurveyNote).HasMaxLength(2000);
        e.Property(x => x.RejectReason).HasMaxLength(1000);
        e.HasOne(x => x.ParkingLot).WithMany().HasForeignKey(x => x.ParkingLotId);
        e.HasIndex(x => new { x.ParkingLotId, x.Status });
        e.HasIndex(x => x.Status);
    }
}
