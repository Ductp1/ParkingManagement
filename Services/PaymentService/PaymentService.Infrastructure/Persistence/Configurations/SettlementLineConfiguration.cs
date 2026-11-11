using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class SettlementLineConfiguration : IEntityTypeConfiguration<SettlementLine>
{
    /// <summary>US-080: mỗi giao dịch chỉ vào đúng 1 kỳ (dòng Booking / WalkIn).</summary>
    public const string UniquePaymentIndex = "UX_SettlementLines_Payment";
    /// <summary>US-080: mỗi lần hoàn chỉ vào đúng 1 kỳ.</summary>
    public const string UniqueRefundIndex = "UX_SettlementLines_Refund";
    /// <summary>US-080: một khiếu nại chỉ bị giữ một lần và giải phóng một lần.</summary>
    public const string UniqueHoldIndex = "UX_SettlementLines_Hold";
    public const string UniqueHoldReleaseIndex = "UX_SettlementLines_HoldRelease";

    public void Configure(EntityTypeBuilder<SettlementLine> e)
    {
        e.ToTable("SettlementLines");
        e.Property(x => x.ReferenceCode).HasMaxLength(30);
        e.Property(x => x.BlockedReason).HasMaxLength(50);
        e.HasIndex(x => x.SettlementId);
        e.HasIndex(x => x.PaymentId).HasFilter("\"PaymentId\" IS NOT NULL");
        e.HasIndex(x => x.PaymentId, UniquePaymentIndex).IsUnique()
            .HasFilter("\"PaymentId\" IS NOT NULL AND \"LineType\" IN ('Booking', 'WalkIn')");
        e.HasIndex(x => x.RefundId, UniqueRefundIndex).IsUnique().HasFilter("\"RefundId\" IS NOT NULL");
        e.HasIndex(x => x.ReferenceCode, UniqueHoldIndex).IsUnique()
            .HasFilter("\"LineType\" = 'DisputeHold' AND \"Amount\" < 0");
        e.HasIndex(x => x.ReferenceCode, UniqueHoldReleaseIndex).IsUnique()
            .HasFilter("\"LineType\" = 'DisputeHold' AND \"Amount\" > 0");
    }
}
