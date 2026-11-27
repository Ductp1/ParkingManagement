using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class FinancialAdjustmentConfiguration : IEntityTypeConfiguration<FinancialAdjustment>
{
    /// <summary>US-080: mỗi voucher đền bù chỉ bị trừ một lần (và đảo một lần – SourceReference "{mã}:REVOKED").</summary>
    public const string UniqueVoucherIndex = "UX_FinancialAdjustments_Voucher";

    public void Configure(EntityTypeBuilder<FinancialAdjustment> e)
    {
        e.ToTable("FinancialAdjustments", t => t.HasCheckConstraint("CK_FinancialAdjustments_Amount", "\"Amount\" <> 0"));
        e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        e.Property(x => x.SourceReference).HasMaxLength(60);
        e.Property(x => x.RejectReason).HasMaxLength(500);
        e.Property(x => x.RowVersion).IsConcurrencyToken();
        e.HasIndex(x => x.OwnerProfileId);
        e.HasIndex(x => new { x.OwnerProfileId, x.Status, x.SettlementId });   // bút toán đã duyệt chưa gắn kỳ của chủ bãi
        e.HasIndex(x => new { x.SourceType, x.SourceReference }, UniqueVoucherIndex).IsUnique()
            .HasFilter("\"SourceType\" = 'CompensationVoucher'");
    }
}
