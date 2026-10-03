using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using SupportService.Domain.Entities;

namespace SupportService.Infrastructure.Persistence.Configurations;

internal sealed class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> e)
    {
        e.ToTable("Complaints");
        e.Property(x => x.Code).HasMaxLength(30).IsRequired();
        e.Property(x => x.BookingCode).HasMaxLength(30);
        e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        e.Property(x => x.EvidenceUrlsJson).IsMaxText();
        e.Property(x => x.OwnerResponse).HasMaxLength(4000);
        e.Property(x => x.Resolution).HasMaxLength(4000);
        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => new { x.OwnerProfileId, x.Status });               // hộp tranh chấp của chủ bãi
        e.HasIndex(x => new { x.Status, x.OwnerResponseDueAtUtc });        // job escalate khi quá 48h
        e.HasIndex(x => x.UserId);
        e.HasIndex(x => x.BookingId).HasFilter("[BookingId] IS NOT NULL");
    }
}
