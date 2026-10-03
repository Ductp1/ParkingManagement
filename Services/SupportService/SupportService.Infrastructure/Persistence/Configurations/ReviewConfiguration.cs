using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using SupportService.Domain.Entities;

namespace SupportService.Infrastructure.Persistence.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> e)
    {
        e.ToTable("Reviews", t => t.HasCheckConstraint("CK_Reviews_Rating", "\"Rating\" BETWEEN 1 AND 5"));
        e.Property(x => x.ReviewerName).HasMaxLength(150).IsRequired();
        e.Property(x => x.Comment).HasMaxLength(2000);
        e.Property(x => x.OwnerReply).HasMaxLength(2000);
        e.Property(x => x.FlagReason).HasMaxLength(500);
        // Mỗi booking chỉ được đánh giá 1 lần.
        e.HasIndex(x => x.BookingId).IsUnique();
        e.HasIndex(x => new { x.ParkingLotId, x.IsHidden, x.CreatedAtUtc });
    }
}
