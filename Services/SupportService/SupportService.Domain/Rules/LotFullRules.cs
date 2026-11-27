namespace SupportService.Domain.Rules;

/// <summary>Trạng thái sự cố "đến nơi nhưng bãi hết chỗ" (US-091).</summary>
public enum LotFullIncidentStatus
{
    /// <summary>Đã ghi nhận, đang chờ tài xế chọn bãi thay thế hoặc từ chối.</summary>
    Reported = 0,
    /// <summary>AC2: tài xế đồng ý chuyển → đã phát LotFullTransferAccepted cho BookingService chuyển booking.</summary>
    TransferAccepted = 1,
    /// <summary>AC3: tài xế không nhận bãi thay thế → đã phát LotFullRefundRequested (hoàn 100% + voucher) và ghi vi phạm SLA.</summary>
    RefundRequested = 2,
}

/// <summary>Quy tắc nghiệp vụ US-091 – gom một chỗ để BA dễ đối chiếu / điều chỉnh.</summary>
public static class LotFullRules
{
    /// <summary>AC2: chỉ gợi ý bãi thay thế trong bán kính 1 km quanh bãi đã đặt.</summary>
    public const double AlternativeRadiusKm = 1.0;
    public const int MaxAlternatives = 5;

    /// <summary>Chỉ báo sự cố khi đã tới bãi: từ 30 phút trước giờ bắt đầu đến hết khung giờ đã đặt.</summary>
    public const int ReportFromMinutesBeforeStart = 30;

    /// <summary>AC3: hoàn 100% + voucher đền bù (chủ bãi chịu). Giá trị voucher tạm chốt, BA xác nhận lại với nhóm.</summary>
    public const int RefundPercent = 100;
    public const decimal CompensationVoucherAmount = 20_000m;
    public const int CompensationVoucherValidDays = 90;

    public const int MaxNoteLength = 500;

    /// <summary>Booking khung giờ [start, end] được báo sự cố tại thời điểm <paramref name="nowUtc"/>?</summary>
    public static bool IsWithinReportWindow(DateTime startAtUtc, DateTime endAtUtc, DateTime nowUtc)
        => nowUtc >= startAtUtc.AddMinutes(-ReportFromMinutesBeforeStart) && nowUtc <= endAtUtc;
}
