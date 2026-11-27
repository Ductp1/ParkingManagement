namespace SupportService.Domain.Rules;

/// <summary>Quy tắc khiếu nại "chỗ đã đặt bị xe khác chiếm" và phạt xe đậu sai chỗ (US-090).</summary>
public static class SlotOccupiedRules
{
    public const int MaxSlotCodeLength = 30;
    /// <summary>Mức phạt do chủ bãi tự đặt; trần chỉ để chặn nhập nhầm (tạm chốt, cần BA xác nhận).</summary>
    public const decimal MaxWrongSlotFine = 5_000_000m;
}
