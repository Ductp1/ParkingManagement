using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

/// <summary>
/// US-079: sổ hoa hồng (chỉ thêm). Hai index duy nhất có lọc là chốt chống ghi trùng khi nhiều instance cùng chạy job:
/// mỗi giao dịch một Accrual; mỗi mức RefundedAmount một Reversal. Payment không có navigation (không đổi entity Payment).
/// </summary>
internal sealed class CommissionEntryConfiguration : IEntityTypeConfiguration<CommissionEntry>
{
    public const string UniqueAccrualIndex = "IX_CommissionEntries_PaymentId_Accrual";
    public const string UniqueReversalIndex = "IX_CommissionEntries_PaymentId_RefundedAmountCovered_Reversal";

    public void Configure(EntityTypeBuilder<CommissionEntry> e)
    {
        e.ToTable("CommissionEntries", t =>
        {
            t.HasCheckConstraint("CK_CommissionEntries_Rate", "\"Rate\" >= 0 AND \"Rate\" <= 1");
            t.HasCheckConstraint("CK_CommissionEntries_Sign",
                "(\"EntryType\" = 'Accrual' AND \"Amount\" >= 0 AND \"BaseAmount\" >= 0 AND \"RefundedAmountCovered\" = 0)"
                + " OR (\"EntryType\" = 'Reversal' AND \"Amount\" <= 0 AND \"BaseAmount\" < 0 AND \"RefundedAmountCovered\" > 0)");
        });
        e.Property(x => x.Rate).HasPrecision(9, 6);
        e.Property(x => x.RateSource).HasMaxLength(30).IsRequired();
        e.Property(x => x.ContractNumber).HasMaxLength(50);

        e.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId);

        // Index đặt tên riêng: cùng cột PaymentId nhưng là index khác (index không tên sẽ bị EF gộp làm một).
        e.HasIndex(x => x.PaymentId);
        e.HasIndex(x => x.PaymentId, UniqueAccrualIndex).IsUnique()
            .HasFilter($"\"EntryType\" = '{nameof(CommissionEntryType.Accrual)}'");
        e.HasIndex(x => new { x.PaymentId, x.RefundedAmountCovered }, UniqueReversalIndex).IsUnique()
            .HasFilter($"\"EntryType\" = '{nameof(CommissionEntryType.Reversal)}'");
        e.HasIndex(x => new { x.OwnerProfileId, x.PaidAtUtc });    // báo cáo chủ bãi (US-078)
        e.HasIndex(x => x.PaidAtUtc);                               // tổng hợp toàn sàn cho Admin
    }
}
