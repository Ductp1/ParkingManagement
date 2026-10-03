using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> e)
    {
        // Hóa đơn phí đỗ gắn Payment, hóa đơn hoa hồng gắn Settlement – phải có đúng 1 trong 2.
        e.ToTable("Invoices", t => t.HasCheckConstraint("CK_Invoices_Source",
            "([Type] = N'ParkingFee' AND [PaymentId] IS NOT NULL) OR ([Type] = N'PlatformCommission' AND [SettlementId] IS NOT NULL)"));
        e.Property(x => x.InvoiceNumber).HasMaxLength(30).IsRequired();
        e.Property(x => x.BuyerName).HasMaxLength(200).IsRequired();
        e.Property(x => x.BuyerTaxCode).HasMaxLength(20);
        e.Property(x => x.BuyerEmail).HasMaxLength(256);
        e.Property(x => x.PdfUrl).HasMaxLength(512);
        e.HasIndex(x => x.InvoiceNumber).IsUnique();
        e.HasOne(x => x.Settlement).WithMany().HasForeignKey(x => x.SettlementId);
        e.HasIndex(x => x.PaymentId).IsUnique().HasFilter("[PaymentId] IS NOT NULL");
        e.HasIndex(x => x.SettlementId).IsUnique().HasFilter("[SettlementId] IS NOT NULL");
    }
}
