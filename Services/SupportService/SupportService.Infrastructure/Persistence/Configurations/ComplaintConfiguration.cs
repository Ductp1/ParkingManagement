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
        e.ToTable("Complaints", t =>
        {
            // US-089 AC3 – bảo vệ ở tầng DB: không giữ tiền khi chưa biết chủ bãi, và ticket đã đóng thì không còn giữ tiền.
            t.HasCheckConstraint("CK_Complaints_PayoutHold_RequiresOwner", "NOT \"PayoutHeld\" OR \"OwnerProfileId\" IS NOT NULL");
            t.HasCheckConstraint("CK_Complaints_PayoutHold_OnlyWhileOpen", "NOT \"PayoutHeld\" OR \"Status\" NOT IN ('Resolved', 'Rejected')");
        });
        e.Property(x => x.RowVersion).IsConcurrencyToken();   // PostgreSQL: tự đóng dấu ở SaveChanges (giống Booking, Slot)
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
        e.HasIndex(x => new { x.AssignedTeam, x.Priority, x.Status });   // hàng đợi xử lý của Support/Supervisor
        e.HasIndex(x => x.BookingId).HasFilter("\"BookingId\" IS NOT NULL");
    }
}
