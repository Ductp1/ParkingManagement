using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình ánh xạ bảng RateCardAuditLogs trong cơ sở dữ liệu PostgreSQL cho US-072.
/// </summary>
internal sealed class RateCardAuditLogConfiguration : IEntityTypeConfiguration<RateCardAuditLog>
{
    public void Configure(EntityTypeBuilder<RateCardAuditLog> e)
    {
        e.ToTable("RateCardAuditLogs");

        // Giới hạn độ dài trường Action để tối ưu lưu trữ
        e.Property(x => x.Action).HasMaxLength(50).IsRequired();

        // Cấu hình kiểu dữ liệu jsonb cho PostgreSQL để lưu chuỗi JSON cấu hình bảng giá
        e.Property(x => x.OldValueJson).HasColumnType("jsonb");
        e.Property(x => x.NewValueJson).HasColumnType("jsonb").IsRequired();

        // Độ chính xác phần trăm tăng giá: ví dụ 150.50%
        e.Property(x => x.MaxPriceIncreasePercentage).HasPrecision(6, 2);

        // Khóa ngoại liên kết tới bảng RateCards trong cùng service
        e.HasOne<RateCard>().WithMany().HasForeignKey(x => x.RateCardId);

        // Chỉ mục tối ưu hóa tốc độ tra cứu lịch sử theo từng bãi đỗ xe
        e.HasIndex(x => new { x.ParkingLotId, x.CreatedAtUtc });

        // Chỉ mục tối ưu hóa cho màn hình Admin lọc các yêu cầu tăng giá > 30% cần duyệt
        e.HasIndex(x => new { x.RequiresAdminApproval, x.CreatedAtUtc });
    }
}

