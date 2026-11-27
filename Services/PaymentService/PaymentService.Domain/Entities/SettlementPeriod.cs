using ParkingManagement.SharedKernel.Domain;
using PaymentService.Domain.Services;

namespace PaymentService.Domain.Entities;

/// <summary>
/// [PaymentService] US-080: lịch kỳ quyết toán dùng chung cho mọi chủ bãi. Kỳ sau bắt đầu ngay sau ngày cuối của kỳ trước;
/// chu kỳ = SETTLEMENT_CYCLE có hiệu lực lúc 00:00 ICT ngày đầu kỳ (đổi chu kỳ giữa chừng → kỳ đang chạy giữ chu kỳ cũ,
/// kỳ kế có thể ngắn hơn một khối). Chỉ thêm, không sửa / xóa. Module: TV8.
/// </summary>
public class SettlementPeriod : BaseEntity
{
    public DateOnly PeriodStart { get; private set; }
    /// <summary>Ngày cuối (bao gồm).</summary>
    public DateOnly PeriodEnd { get; private set; }
    /// <summary>WEEKLY | BIWEEKLY | MONTHLY.</summary>
    public string Cycle { get; private set; } = string.Empty;
    /// <summary>Mốc hiệu lực của giá trị SETTLEMENT_CYCLE đã dùng (null = giá trị gốc).</summary>
    public DateTime? CycleEffectiveFromUtc { get; private set; }

    private SettlementPeriod() { }

    public static SettlementPeriod Create(DateOnly periodStart, SettlementCycle cycle, DateTime? cycleEffectiveFromUtc, DateOnly biweeklyAnchor,
        DateTime nowUtc)
        => new()
        {
            PeriodStart = periodStart,
            PeriodEnd = SettlementCalendar.BlockEnd(cycle, periodStart, biweeklyAnchor),
            Cycle = SettlementCalendar.Format(cycle),
            CycleEffectiveFromUtc = cycleEffectiveFromUtc,
            CreatedAtUtc = nowUtc,
        };

    /// <summary>Kỳ chưa lưu dựng lại từ ngày đầu / cuối (kỳ cũ chưa gắn lịch) – chỉ để có mốc chốt.</summary>
    public static SettlementPeriod Rehydrate(DateOnly periodStart, DateOnly periodEnd)
        => new() { PeriodStart = periodStart, PeriodEnd = periodEnd };

    /// <summary>Mốc chốt (24:00 ICT ngày cuối kỳ) quy ra UTC.</summary>
    public DateTime CutoffUtc => SettlementCalendar.CutoffUtc(PeriodEnd);
}
