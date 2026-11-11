namespace BookingService.Domain.Rules;

/// <summary>
/// Tham số chống lạm dụng đặt/hủy (US-103, UC-10 E1b, Answer_8 §46-47) – bind từ appsettings "BookingAbuse",
/// mọi ngưỡng cấu hình được, không hard-code trong logic.
/// </summary>
public sealed record BookingAbuseOptions
{
    /// <summary>Số lượt đặt + hủy tối đa trong cửa sổ thời gian; vượt → tạm chặn đặt/hủy mới.</summary>
    public int MaxBookingActionsPerHour { get; init; } = 10;
    /// <summary>Độ rộng cửa sổ đếm tần suất đặt/hủy (phút).</summary>
    public int ActionWindowMinutes { get; init; } = 60;
    /// <summary>Tỷ lệ No-show tối đa cho phép trên số booking đã kết thúc trong kỳ nhìn lại.</summary>
    public decimal NoShowRateThreshold { get; init; } = 0.5m;
    /// <summary>Số booking đã kết thúc tối thiểu trước khi áp dụng đánh giá tỷ lệ No-show.</summary>
    public int NoShowMinFinishedBookings { get; init; } = 5;
    /// <summary>Kỳ nhìn lại khi tính tỷ lệ No-show (ngày).</summary>
    public int NoShowLookbackDays { get; init; } = 30;

    public static BookingAbuseOptions Default { get; } = new();
}

/// <summary>Kết quả đánh giá một hành vi đặt/hủy: cho phép hay tạm chặn kèm thông điệp.</summary>
public sealed record BookingAbuseDecision(bool Allowed, string? RestrictionMessage)
{
    public static BookingAbuseDecision Allow() => new(true, null);
    public static BookingAbuseDecision Block(string message) => new(false, message);
}

/// <summary>
/// US-103: Quy tắc giới hạn hành vi lạm dụng đặt/hủy.
///  - AC1: đặt/hủy &gt; MaxBookingActionsPerHour lần trong ActionWindowMinutes → tạm chặn đặt/hủy mới (giữ chỗ ảo).
///  - AC2: tỷ lệ No-show vượt ngưỡng trên số booking đã kết thúc → hạn chế đặt mới (yêu cầu liên hệ hỗ trợ).
/// Hàm thuần (pure function) để unit test độc lập với database.
/// </summary>
public static class BookingAbusePolicy
{
    /// <summary>Đánh giá tần suất đặt/hủy trong cửa sổ thời gian (đếm trên Bookings: CreatedAtUtc và CancelledAtUtc).</summary>
    public static BookingAbuseDecision EvaluateActionFrequency(int createdInWindow, int cancelledInWindow, BookingAbuseOptions options)
    {
        var total = createdInWindow + cancelledInWindow;
        if (total < options.MaxBookingActionsPerHour)
            return BookingAbuseDecision.Allow();

        return BookingAbuseDecision.Block(
            $"Bạn đã đặt/hủy {total} lần trong {options.ActionWindowMinutes} phút (giới hạn {options.MaxBookingActionsPerHour}). " +
            "Tài khoản tạm thời bị chặn đặt/hủy chỗ mới để tránh giữ chỗ ảo. Vui lòng thử lại sau hoặc liên hệ hỗ trợ.");
    }

    /// <summary>Đánh giá tỷ lệ No-show trong kỳ nhìn lại (finished = booking đã kết thúc Completed/NoShow/CancelledNoRefund).</summary>
    public static BookingAbuseDecision EvaluateNoShowRate(int finishedBookings, int noShowBookings, BookingAbuseOptions options)
    {
        if (finishedBookings < options.NoShowMinFinishedBookings || finishedBookings <= 0)
            return BookingAbuseDecision.Allow();

        var rate = (decimal)noShowBookings / finishedBookings;
        if (rate < options.NoShowRateThreshold)
            return BookingAbuseDecision.Allow();

        return BookingAbuseDecision.Block(
            $"Tỷ lệ không đến (No-show) của bạn là {rate:P0} ({noShowBookings}/{finishedBookings} lượt), vượt ngưỡng cho phép {options.NoShowRateThreshold:P0}. " +
            "Tài khoản tạm thời bị hạn chế đặt chỗ mới trong 24 giờ. Vui lòng liên hệ hỗ trợ nếu cần đặt chỗ gấp.");
    }
}