using Microsoft.EntityFrameworkCore;
using ParkingManagement.ServiceDefaults.Persistence;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>Database riêng của PaymentService: pm_payment. Biểu giá, khuyến mãi, thanh toán, hoàn tiền, hóa đơn, quyết toán.</summary>
public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : ServiceDbContext(options)
{
    public DbSet<RateCard> RateCards => Set<RateCard>();
    public DbSet<RateRule> RateRules => Set<RateRule>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<SettlementLine> SettlementLines => Set<SettlementLine>();
    public DbSet<FinancialAdjustment> FinancialAdjustments => Set<FinancialAdjustment>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<PromotionRedemption> PromotionRedemptions => Set<PromotionRedemption>();
    public DbSet<PaymentCallbackLog> PaymentCallbackLogs => Set<PaymentCallbackLog>();
    public DbSet<CompensationVoucher> CompensationVouchers => Set<CompensationVoucher>();
    public DbSet<CommissionEntry> CommissionEntries => Set<CommissionEntry>();   // US-079: sổ hoa hồng nền tảng
    public DbSet<SettlementPeriod> SettlementPeriods => Set<SettlementPeriod>(); // US-080: lịch kỳ quyết toán
    public DbSet<RateCardAuditLog> RateCardAuditLogs => Set<RateCardAuditLog>(); // US-072: kiểm soát thay đổi giá & audit log

}
