namespace GateService.Domain.Enums;

/// <summary>
/// Loại xe khi VÀO cổng (US-066): Transient = vãng lai (chịu trần TransientBuffer B),
/// Subscriber = có đặt trước/thuê bao (BookingCode hoặc vé tháng) — chỉ chịu trần tổng C.
/// Loại được ghi lúc check-in và dùng để trừ đúng bộ đếm lúc check-out (luật 6).
/// </summary>
public enum AdmissionType
{
    Transient = 0,
    Subscriber = 1
}