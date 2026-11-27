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
        e.ToTable("Reviews", t =>
        {
            t.HasCheckConstraint("CK_Reviews_Rating", "\"Rating\" BETWEEN 1 AND 5");
            // US-094 AC2: đánh giá đã gỡ thì không còn nằm trong hàng chờ khiếu nại, và luôn biết ai gỡ, khi nào.
            t.HasCheckConstraint("CK_Reviews_Hidden_NotFlagged", "NOT (\"IsHidden\" AND \"IsFlagged\")");
            t.HasCheckConstraint("CK_Reviews_Hidden_HasAudit", "NOT \"IsHidden\" OR (\"HiddenAtUtc\" IS NOT NULL AND \"HiddenByUserId\" IS NOT NULL)");
        });
        e.Property(x => x.RowVersion).IsConcurrencyToken();   // PostgreSQL: tự đóng dấu ở SaveChanges (giống Complaint)
        e.Property(x => x.ReviewerName).HasMaxLength(150).IsRequired();
        e.Property(x => x.Comment).HasMaxLength(2000);
        e.Property(x => x.OwnerReply).HasMaxLength(2000);
        e.Property(x => x.FlagReason).HasMaxLength(500);
        e.Property(x => x.HiddenReason).HasMaxLength(500);
        // Hàng chờ Admin: đánh giá đang bị khiếu nại, cũ nhất trước.
        e.HasIndex(x => x.FlaggedAtUtc).HasFilter("\"IsFlagged\"").HasDatabaseName("IX_Reviews_Flagged_Queue");
        // Mỗi booking chỉ được đánh giá 1 lần.
        e.HasIndex(x => x.BookingId).IsUnique();
        e.HasIndex(x => new { x.ParkingLotId, x.IsHidden, x.CreatedAtUtc });
    }
}
