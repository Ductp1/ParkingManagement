using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> e)
    {
        e.ToTable("Payments", t =>
        {
            t.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0 AND [RefundedAmount] >= 0 AND [RefundedAmount] <= [Amount]");
            t.HasCheckConstraint("CK_Payments_Target", "[BookingId] IS NOT NULL OR [ParkingSessionId] IS NOT NULL");
        });
        e.Property(x => x.Code).HasMaxLength(30).IsRequired();
        e.Property(x => x.BookingCode).HasMaxLength(30);
        e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        e.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        e.Property(x => x.ProviderTransactionId).HasMaxLength(100);
        e.Property(x => x.ProviderResponseCode).HasMaxLength(20);
        e.Property(x => x.RawCallbackJson).IsMaxText();
        e.Property(x => x.TransferContent).HasMaxLength(100);

        // ----- Quan hệ: 1 booking / 1 lượt gửi có thể có nhiều payment (đặt chỗ, gia hạn, phụ thu) -----
        e.HasMany(x => x.Refunds).WithOne(r => r.Payment).HasForeignKey(r => r.PaymentId);
        e.HasOne(x => x.Invoice).WithOne(i => i.Payment).HasForeignKey<Invoice>(i => i.PaymentId);

        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => x.IdempotencyKey).IsUnique();   // callback lặp không trừ tiền 2 lần
        e.HasIndex(x => new { x.Method, x.ProviderTransactionId }).IsUnique().HasFilter("[ProviderTransactionId] IS NOT NULL");
        e.HasIndex(x => x.BookingId);
        e.HasIndex(x => x.ParkingSessionId);
        e.HasIndex(x => new { x.OwnerProfileId, x.Status, x.PaidAtUtc }); // báo cáo doanh thu & quyết toán
    }
}
