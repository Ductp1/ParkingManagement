using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Configurations;

internal sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> e)
    {
        e.ToTable("NotificationTemplates");
        e.Property(x => x.Key).HasMaxLength(50).IsRequired();
        e.Property(x => x.TitleTemplate).HasMaxLength(200).IsRequired();
        e.Property(x => x.BodyTemplate).HasMaxLength(2000).IsRequired();
        e.HasIndex(x => new { x.Key, x.Channel }).IsUnique();

        var at = SeedClock.SeededAtUtc;
        var id = 0;
        object T(string key, NotificationChannel channel, string title, string body) => new
        {
            Id = ++id, Key = key, Channel = channel, TitleTemplate = title, BodyTemplate = body, IsActive = true, CreatedAtUtc = at
        };

        e.HasData(
            T("OTP_CODE", NotificationChannel.Sms, "Mã xác thực", "Mã OTP Smart Parking của bạn là {Code}, hiệu lực 5 phút."),
            T("BOOKING_CONFIRMED", NotificationChannel.InApp, "Đặt chỗ thành công", "Booking {BookingCode} tại {LotName} lúc {StartAt} đã được xác nhận."),
            T("BOOKING_CONFIRMED", NotificationChannel.Email, "Xác nhận đặt chỗ {BookingCode}", "Bạn đã đặt chỗ tại {LotName}, slot {SlotCode}, từ {StartAt} đến {EndAt}. Số tiền: {Amount}."),
            T("BOOKING_HOLD_EXPIRED", NotificationChannel.InApp, "Hết thời gian giữ chỗ", "Booking {BookingCode} đã hết 15 phút giữ chỗ do chưa thanh toán."),
            T("BOOKING_REMINDER", NotificationChannel.InApp, "Sắp đến giờ đỗ xe", "Booking {BookingCode} bắt đầu lúc {StartAt}. Bạn có 15 phút ân hạn khi đến trễ."),
            T("BOOKING_CANCELLED", NotificationChannel.InApp, "Đã hủy booking", "Booking {BookingCode} đã hủy. Hoàn tiền: {RefundAmount}."),
            T("BOOKING_NO_SHOW", NotificationChannel.InApp, "Booking bị hủy do không đến", "Booking {BookingCode} đã bị hủy vì quá 30 phút chưa check-in."),
            T("CHECKIN_SUCCESS", NotificationChannel.InApp, "Xe đã vào bãi", "Xe {PlateNumber} đã vào {LotName} lúc {EntryAt}."),
            T("CHECKOUT_RECEIPT", NotificationChannel.InApp, "Xe đã ra bãi", "Xe {PlateNumber} ra lúc {ExitAt}. Tổng phí: {Amount}."),
            T("PAYMENT_FAILED", NotificationChannel.InApp, "Thanh toán thất bại", "Thanh toán cho {BookingCode} không thành công. Vui lòng thử lại."),
            T("KYB_APPROVED", NotificationChannel.Email, "Bãi xe đã được duyệt", "Bãi {LotName} đã được duyệt và hiển thị trên Smart Parking."),
            T("KYB_REJECTED", NotificationChannel.Email, "Hồ sơ bãi xe cần bổ sung", "Hồ sơ bãi {LotName} chưa được duyệt. Lý do: {Reason}."),
            T("COMPLAINT_CREATED", NotificationChannel.InApp, "Có khiếu nại mới", "Khiếu nại {ComplaintCode} cho bãi {LotName}. Vui lòng phản hồi trong 48 giờ."),
            T("EMERGENCY_CLOSURE", NotificationChannel.Sms, "Bãi đóng khẩn cấp", "Bãi {LotName} tạm đóng. Booking {BookingCode} sẽ được hoàn tiền hoặc đổi bãi."));
    }
}
