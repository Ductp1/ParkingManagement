using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> e)
    {
        e.ToTable("Refunds", t => t.HasCheckConstraint("CK_Refunds_Amount", "\"Amount\" > 0"));
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.ProviderRefundId).HasMaxLength(100);
        e.HasIndex(x => new { x.PaymentId, x.Status });
        e.HasIndex(x => x.ComplaintId).HasFilter("\"ComplaintId\" IS NOT NULL");
    }
}
