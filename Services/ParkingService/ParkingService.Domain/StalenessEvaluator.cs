namespace ParkingService.Domain;

/// <summary>
/// US-068: Pure logic đánh giá "độ tươi" dữ liệu bãi xe – không phụ thuộc EF/HTTP/DB để unit test trực tiếp.
/// Quy ước: dữ liệu được coi là STALE khi chưa từng nhận heartbeat (null) HOẶC khoảng cách từ heartbeat
/// gần nhất đến thời điểm hiện tại vượt quá ngưỡng <paramref name="staleAfter"/> (so sánh >, đúng ngưỡng vẫn Fresh).
/// </summary>
public static class StalenessEvaluator
{
    /// <summary>Kiểm tra bãi đã quá hạn heartbeat hay chưa.</summary>
    /// <param name="lastHeartbeatAtUtc">Thời điểm heartbeat gần nhất (UTC); null = chưa từng nhận heartbeat.</param>
    /// <param name="staleAfter">Ngưỡng tối đa cho phép giữa hai lần heartbeat (mặc định 3 phút theo appsettings).</param>
    /// <param name="nowUtc">Thời điểm hiện tại (UTC).</param>
    public static bool IsStale(DateTime? lastHeartbeatAtUtc, TimeSpan staleAfter, DateTime nowUtc)
        => lastHeartbeatAtUtc is null || nowUtc - lastHeartbeatAtUtc.Value > staleAfter;
}
