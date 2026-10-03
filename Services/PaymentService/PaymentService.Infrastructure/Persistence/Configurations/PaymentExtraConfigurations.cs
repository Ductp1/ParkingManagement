using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> e)
    {
        e.ToTable("Holidays");
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.Property(x => x.City).HasMaxLength(100);
        e.HasIndex(x => new { x.Date, x.City }).IsUnique();

        // Ngày nghỉ lễ Việt Nam năm 2027 (Tết Nguyên đán theo lịch dự kiến – Admin chỉnh khi có thông báo chính thức).
        var at = SeedClock.SeededAtUtc;
        var id = 0;
        object H(int y, int m, int d, string name) => new { Id = ++id, Date = new DateOnly(y, m, d), Name = name, CreatedAtUtc = at };
        e.HasData(
            H(2027, 1, 1, "Tết Dương lịch"),
            H(2027, 2, 5, "Tết Nguyên đán (29 Tết)"), H(2027, 2, 6, "Tết Nguyên đán (Mùng 1)"),
            H(2027, 2, 7, "Tết Nguyên đán (Mùng 2)"), H(2027, 2, 8, "Tết Nguyên đán (Mùng 3)"), H(2027, 2, 9, "Tết Nguyên đán (Mùng 4)"),
            H(2027, 4, 16, "Giỗ Tổ Hùng Vương"),
            H(2027, 4, 30, "Ngày Giải phóng miền Nam"), H(2027, 5, 1, "Quốc tế Lao động"),
            H(2027, 9, 2, "Quốc khánh"), H(2027, 9, 3, "Quốc khánh (nghỉ liền kề)"));
    }
}

internal sealed class PromotionRedemptionConfiguration : IEntityTypeConfiguration<PromotionRedemption>
{
    public void Configure(EntityTypeBuilder<PromotionRedemption> e)
    {
        e.ToTable("PromotionRedemptions", t => t.HasCheckConstraint("CK_PromotionRedemptions_Amount", "\"DiscountAmount\" >= 0"));
        e.HasOne(x => x.Promotion).WithMany(p => p.Redemptions).HasForeignKey(x => x.PromotionId);
        e.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId);
        // 1 booking chỉ dùng 1 mã; đếm lượt dùng theo user cho mã "lần đầu".
        e.HasIndex(x => x.BookingId).IsUnique().HasFilter("\"IsReverted\" = false");
        e.HasIndex(x => new { x.PromotionId, x.UserId });
    }
}

internal sealed class PaymentCallbackLogConfiguration : IEntityTypeConfiguration<PaymentCallbackLog>
{
    public void Configure(EntityTypeBuilder<PaymentCallbackLog> e)
    {
        e.ToTable("PaymentCallbackLogs");
        e.Property(x => x.ProviderTransactionId).HasMaxLength(100);
        e.Property(x => x.RawPayload).IsMaxText().IsRequired();
        e.Property(x => x.ResultCode).HasMaxLength(20);
        e.Property(x => x.ProcessingError).HasMaxLength(1000);
        e.Property(x => x.SourceIp).HasMaxLength(45);
        e.HasOne(x => x.Payment).WithMany(p => p.CallbackLogs).HasForeignKey(x => x.PaymentId);
        e.HasIndex(x => new { x.PaymentId, x.CreatedAtUtc });
        e.HasIndex(x => new { x.Provider, x.ProviderTransactionId });
    }
}

internal sealed class CompensationVoucherConfiguration : IEntityTypeConfiguration<CompensationVoucher>
{
    public void Configure(EntityTypeBuilder<CompensationVoucher> e)
    {
        e.ToTable("CompensationVouchers", t => t.HasCheckConstraint("CK_CompensationVouchers_Amount", "\"Amount\" > 0"));
        e.Property(x => x.Code).HasMaxLength(30).IsRequired();
        e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        e.HasOne(x => x.RedeemedPayment).WithMany().HasForeignKey(x => x.RedeemedPaymentId);
        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => new { x.UserId, x.Status, x.ExpiresAtUtc });          // voucher còn dùng được của tài xế
        e.HasIndex(x => x.ChargedToOwnerProfileId).HasFilter("\"ChargedToOwnerProfileId\" IS NOT NULL");
    }
}
