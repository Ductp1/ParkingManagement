using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportService.Domain.Entities;

namespace SupportService.Infrastructure.Persistence.Configurations;

/// <summary>US-090: phán quyết tranh chấp – chỉ thêm (trigger trong migration chặn UPDATE/DELETE).</summary>
internal sealed class DisputeRulingConfiguration : IEntityTypeConfiguration<DisputeRuling>
{
    public void Configure(EntityTypeBuilder<DisputeRuling> e)
    {
        // Dữ liệu khớp loại phán quyết: ép hoàn → có tiền, chuyển chỗ → có chỗ mới, bác → không có gì.
        e.ToTable("DisputeRulings", t => t.HasCheckConstraint("CK_DisputeRulings_Data",
            "(\"Decision\" = 'ForceRefund' AND \"RefundAmount\" > 0 AND \"NewSlotCode\" IS NULL) OR " +
            "(\"Decision\" = 'MoveToSlot' AND \"NewSlotCode\" IS NOT NULL AND \"RefundAmount\" IS NULL) OR " +
            "(\"Decision\" = 'RejectComplaint' AND \"RefundAmount\" IS NULL AND \"NewSlotCode\" IS NULL)"));
        e.Ignore(x => x.Outcome);
        e.Property(x => x.Reason).HasMaxLength(4000).IsRequired();
        e.Property(x => x.NewSlotCode).HasMaxLength(30);
        // 1–1: mỗi tranh chấp đúng 1 phán quyết (unique ComplaintId – bấm 2 lần cùng lúc → 23505 → 409).
        e.HasOne(x => x.Complaint).WithOne(c => c.Ruling).HasForeignKey<DisputeRuling>(x => x.ComplaintId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.ParkingLotId, x.Category, x.CreatedAtUtc });   // tiền lệ theo bãi + loại khiếu nại
    }
}

/// <summary>US-090: mức phạt đậu sai chỗ – mỗi bãi 1 dòng.</summary>
internal sealed class LotFineRateConfiguration : IEntityTypeConfiguration<LotFineRate>
{
    public void Configure(EntityTypeBuilder<LotFineRate> e)
    {
        e.ToTable("LotFineRates", t => t.HasCheckConstraint("CK_LotFineRates_Amount", "\"WrongSlotFine\" > 0"));
        e.HasIndex(x => x.ParkingLotId).IsUnique();
    }
}

/// <summary>US-090: biên bản xe đậu sai chỗ.</summary>
internal sealed class ParkingViolationConfiguration : IEntityTypeConfiguration<ParkingViolation>
{
    public void Configure(EntityTypeBuilder<ParkingViolation> e)
    {
        e.ToTable("ParkingViolations", t => t.HasCheckConstraint("CK_ParkingViolations_Fine", "\"FineAmount\" > 0"));
        e.Property(x => x.Code).HasMaxLength(30).IsRequired();
        e.Property(x => x.PlateNumber).HasMaxLength(20).IsRequired();
        e.Property(x => x.SlotCode).HasMaxLength(30);
        e.HasOne(x => x.Complaint).WithMany(c => c.Violations).HasForeignKey(x => x.ComplaintId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => new { x.ParkingLotId, x.CreatedAtUtc });       // danh sách vi phạm theo bãi
        e.HasIndex(x => new { x.ParkingLotId, x.PlateNumber });         // cổng ra tra phạt theo biển số
    }
}
