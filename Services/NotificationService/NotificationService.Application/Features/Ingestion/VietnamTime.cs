namespace NotificationService.Application.Features.Ingestion;

/// <summary>US-083/084: hiển thị giờ VN trong nội dung thông báo/nhắc lịch (mặc định Asia/Ho_Chi_Minh).</summary>
public static class VietnamTime
{
    private static readonly TimeZoneInfo Zone = Resolve();

    /// <summary>Format "HH:mm dd/MM/yyyy" theo giờ Việt Nam (input luôn là DateTime UTC).</summary>
    public static string Format(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone).ToString("HH:mm dd/MM/yyyy");

    private static TimeZoneInfo Resolve()
    {
        // Linux/ICU dùng IANA id; Windows không có ICU thì dùng Windows id tương đương.
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }
}

