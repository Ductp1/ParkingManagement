namespace AdminService.Application.Features.Settings;

/// <summary>Một dòng lịch sử thay đổi tham số, đúng như đọc từ pm_admin (chưa lọc gì).</summary>
public sealed record SystemConfigChangePoint(int Id, string Value, DateTime EffectiveFromUtc, DateTime? CancelledAtUtc);

/// <summary>Giá trị hiệu lực của một tham số. EffectiveFromUtc = null khi vẫn là giá trị gốc.</summary>
public sealed record EffectiveConfigValue(string Value, DateTime? EffectiveFromUtc);

/// <summary>
/// US-098: Tính giá trị hiệu lực của một tham số tại một thời điểm. Hàm thuần – không đọc database, không đọc đồng hồ.
/// Giá trị hiệu lực = thay đổi chưa bị hủy có EffectiveFromUtc ≤ thời điểm hỏi và mới nhất; không có thì là giá trị gốc.
/// Vì thay đổi không được đặt ở quá khứ và chỉ hủy được khi chưa hiệu lực, kết quả cho một thời điểm đã qua không bao giờ đổi
/// – service khác dùng nó để chốt tham số theo lúc tạo booking.
/// </summary>
public static class SystemConfigValueResolver
{
    public static EffectiveConfigValue Resolve(string defaultValue, IEnumerable<SystemConfigChangePoint> changes, DateTime atUtc)
    {
        var current = changes
            .Where(c => c.CancelledAtUtc is null && c.EffectiveFromUtc <= atUtc)
            .OrderByDescending(c => c.EffectiveFromUtc)
            .ThenByDescending(c => c.Id)
            .FirstOrDefault();

        return current is null
            ? new EffectiveConfigValue(defaultValue, null)
            : new EffectiveConfigValue(current.Value, current.EffectiveFromUtc);
    }
}
