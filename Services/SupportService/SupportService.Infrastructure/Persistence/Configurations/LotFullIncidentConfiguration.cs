using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportService.Domain.Entities;

namespace SupportService.Infrastructure.Persistence.Configurations;

internal sealed class LotFullIncidentConfiguration : IEntityTypeConfiguration<LotFullIncident>
{
    public void Configure(EntityTypeBuilder<LotFullIncident> e)
    {
        e.ToTable("LotFullIncidents", t =>
        {
            // US-091: quyết định của tài xế luôn đủ dữ liệu – chuyển bãi có bãi mới; hoàn tiền có % hoàn, voucher và vi phạm SLA.
            t.HasCheckConstraint("CK_LotFullIncidents_TransferAccepted",
                "\"Status\" <> 'TransferAccepted' OR (\"NewParkingLotId\" IS NOT NULL AND \"DecidedAtUtc\" IS NOT NULL)");
            t.HasCheckConstraint("CK_LotFullIncidents_RefundRequested",
                "\"Status\" <> 'RefundRequested' OR (\"RefundPercent\" = 100 AND \"VoucherAmount\" > 0 AND \"IsSlaViolation\" AND \"DecidedAtUtc\" IS NOT NULL)");
            t.HasCheckConstraint("CK_LotFullIncidents_PaidAmount", "\"PaidAmount\" >= 0");
        });
        e.Property(x => x.RowVersion).IsConcurrencyToken();   // tài xế bấm đồng thời "chuyển" và "hoàn tiền" → chỉ 1 thao tác thắng
        e.Property(x => x.Code).HasMaxLength(30).IsRequired();
        e.Property(x => x.BookingCode).HasMaxLength(30).IsRequired();
        e.Property(x => x.NewParkingLotName).HasMaxLength(200);
        e.Property(x => x.Note).HasMaxLength(500);
        // text: quy ước chung của ServiceDbContext là varchar(500) – 5 bãi gợi ý dạng JSON sẽ vượt quá.
        e.Property(x => x.AlternativesJson).HasColumnType("text").IsRequired();
        e.Property(x => x.PaidAmount).HasPrecision(18, 2);
        e.Property(x => x.VoucherAmount).HasPrecision(18, 2);

        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => x.BookingId).IsUnique();                              // mỗi booking 1 sự cố
        e.HasIndex(x => new { x.Status, x.CreatedAtUtc });                    // hàng đợi CSKH: sự cố đang mở
        e.HasIndex(x => new { x.ParkingLotId, x.IsSlaViolation });            // thống kê vi phạm SLA theo bãi
    }
}
