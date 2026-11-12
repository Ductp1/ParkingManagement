using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng PmsUnmatchedEvents – hàng chờ sự kiện PMS không khớp (US-052, UC-51 E1).</summary>
internal sealed class PmsUnmatchedEventConfiguration : IEntityTypeConfiguration<PmsUnmatchedEvent>
{
    public void Configure(EntityTypeBuilder<PmsUnmatchedEvent> e)
    {
        e.ToTable("PmsUnmatchedEvents");
        e.Property(x => x.PlateNumber).HasMaxLength(15);
        e.Property(x => x.BookingCode).HasMaxLength(30);
        e.Property(x => x.Reason).HasMaxLength(1000);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.PayloadJson).HasColumnType("jsonb");
        // Idempotency theo EventId của PMS: tra cứu nhanh + chặn trùng dòng đang Pending.
        e.HasIndex(x => x.EventId);
        e.HasIndex(x => new { x.Status, x.ParkingLotId });
    }
}