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
        // US-083 i18n: mỗi key×channel có bản "vi" (bắt buộc, seed) và "en" (bổ sung dần).
        e.Property(x => x.Language).HasMaxLength(5).IsRequired();
        e.Property(x => x.TitleTemplate).HasMaxLength(200).IsRequired();
        e.Property(x => x.BodyTemplate).HasMaxLength(2000).IsRequired();
        e.HasIndex(x => new { x.Key, x.Channel, x.Language }).IsUnique();

        var at = SeedClock.SeededAtUtc;
        var id = 0;
        object T(string key, NotificationChannel channel, string title, string body, string language = "vi") => new
        {
            Id = ++id, Key = key, Channel = channel, Language = language, TitleTemplate = title, BodyTemplate = body, IsActive = true, CreatedAtUtc = at
        };

        e.HasData(
            T("OTP_CODE", NotificationChannel.Sms, "Mã xác thực", "Mã OTP Smart Parking của bạn là {Code}, hiệu lực 5 phút."),
            T("BOOKING_CONFIRMED", NotificationChannel.InApp, "Đặt chỗ thành công", "Booking {BookingCode} tại {LotName} lúc {StartAt} đã được xác nhận."),
            T("BOOKING_CONFIRMED", NotificationChannel.InApp, "Booking confirmed", "Booking {BookingCode} at {LotName} starting {StartAt} has been confirmed.", "en"),
            T("BOOKING_CONFIRMED", NotificationChannel.Email, "Xác nhận đặt chỗ {BookingCode}", "Bạn đã đặt chỗ tại {LotName}, slot {SlotCode}, từ {StartAt} đến {EndAt}. Số tiền: {Amount}."),
            T("BOOKING_CONFIRMED", NotificationChannel.Email, "Booking {BookingCode} confirmed", "Your booking at {LotName}, slot {SlotCode}, from {StartAt} to {EndAt}. Amount: {Amount}.", "en"),
            T("BOOKING_HOLD_EXPIRED", NotificationChannel.InApp, "Hết thời gian giữ chỗ", "Booking {BookingCode} đã hết 15 phút giữ chỗ do chưa thanh toán."),
            T("BOOKING_REMINDER", NotificationChannel.InApp, "Sắp đến giờ đỗ xe", "Booking {BookingCode} bắt đầu lúc {StartAt}. Bạn có 15 phút ân hạn khi đến trễ."),
            T("BOOKING_CANCELLED", NotificationChannel.InApp, "Đã hủy booking", "Booking {BookingCode} đã hủy. Hoàn tiền: {RefundAmount}."),
            T("BOOKING_CANCELLED", NotificationChannel.InApp, "Booking cancelled", "Booking {BookingCode} has been cancelled. Refund: {RefundAmount}.", "en"),
            T("BOOKING_NO_SHOW", NotificationChannel.InApp, "Booking bị hủy do không đến", "Booking {BookingCode} đã bị hủy vì quá 30 phút chưa check-in."),
            T("CHECKIN_SUCCESS", NotificationChannel.InApp, "Xe đã vào bãi", "Xe {PlateNumber} đã vào {LotName} lúc {EntryAt}."),
            T("CHECKOUT_RECEIPT", NotificationChannel.InApp, "Xe đã ra bãi", "Xe {PlateNumber} ra lúc {ExitAt}. Tổng phí: {Amount}."),
            T("PAYMENT_SUCCEEDED", NotificationChannel.InApp, "Thanh toán thành công", "Đã nhận {Amount} qua {Method}. Cảm ơn bạn!"),
            T("PAYMENT_SUCCEEDED", NotificationChannel.InApp, "Payment successful", "We received {Amount} via {Method}. Thank you!", "en"),
            T("PAYMENT_FAILED", NotificationChannel.InApp, "Thanh toán thất bại", "Thanh toán cho booking {BookingCode} không thành công ({Reason}). Vui lòng thử lại trong 15 phút giữ chỗ."),
            T("KYB_APPROVED", NotificationChannel.Email, "Bãi xe đã được duyệt", "Bãi {LotName} đã được duyệt và hiển thị trên Smart Parking."),
            T("KYB_REJECTED", NotificationChannel.Email, "Hồ sơ bãi xe cần bổ sung", "Hồ sơ bãi {LotName} chưa được duyệt. Lý do: {Reason}."),
            T("COMPLAINT_CREATED", NotificationChannel.InApp, "Có khiếu nại mới", "Khiếu nại {ComplaintCode} cho bãi {LotName}. Vui lòng phản hồi trong 48 giờ."),
            T("EMERGENCY_CLOSURE", NotificationChannel.Sms, "Bãi đóng khẩn cấp", "Bãi {LotName} tạm đóng. Booking {BookingCode} sẽ được hoàn tiền hoặc đổi bãi."),
            T("EMERGENCY_CLOSURE", NotificationChannel.Sms, "Emergency closure", "Lot {LotName} is temporarily closed. Booking {BookingCode} will be refunded or relocated.", "en"),
            T("EMERGENCY_CLOSURE", NotificationChannel.InApp, "Bãi đóng khẩn cấp", "Bãi {LotName} tạm đóng khẩn cấp. Lý do: {Reason}. Booking {BookingCode} của bạn bị ảnh hưởng — đội hỗ trợ sẽ liên hệ hoàn tiền hoặc đổi bãi sớm nhất."),
            T("EMERGENCY_CLOSURE", NotificationChannel.InApp, "Emergency closure", "Lot {LotName} is temporarily closed. Reason: {Reason}. Your booking {BookingCode} is affected — support will contact you for a refund or relocation shortly.", "en"),
            T("EMERGENCY_CLOSURE", NotificationChannel.Email, "Bãi {LotName} đóng khẩn cấp – booking {BookingCode}", "Bãi {LotName} tạm đóng khẩn cấp. Lý do: {Reason}. Booking {BookingCode} của bạn bị ảnh hưởng, chúng tôi sẽ xử lý hoàn tiền/đổi bãi."),
            T("EMERGENCY_CLOSURE", NotificationChannel.Push, "Bãi đóng khẩn cấp", "Bãi {LotName} tạm đóng. Booking {BookingCode} bị ảnh hưởng."),
            T("OWNER_BOOKING_NEW", NotificationChannel.InApp, "Có đặt chỗ mới", "Booking {BookingCode} tại {LotName}: {StartAt} → {EndAt}."),
            T("OWNER_BOOKING_CANCELLED", NotificationChannel.InApp, "Booking đã hủy", "Booking {BookingCode} tại {LotName} vừa bị hủy. Lý do: {Reason}."),
            T("OWNER_BOOKING_MODIFIED", NotificationChannel.InApp, "Booking thay đổi", "Booking {BookingCode} tại {LotName} đã đổi: {ChangedNote}. Thời gian mới: {StartAt} → {EndAt}."),
            T("BOOKING_REMINDER_ARRIVAL", NotificationChannel.InApp, "Nhắc lịch đỗ xe", "Bạn có lịch đỗ tại {LotName} lúc {StartAt} (mã {BookingCode}). Xuất phát đúng giờ nhé!"),
            T("BOOKING_REMINDER_ARRIVAL", NotificationChannel.InApp, "Parking reminder", "Your parking at {LotName} starts at {StartAt} (booking {BookingCode}).", "en"),
            T("BOOKING_REMINDER_ARRIVAL", NotificationChannel.Email, "Nhắc lịch: đỗ xe lúc {StartAt}", "Bạn có lịch đỗ tại {LotName} lúc {StartAt} (mã {BookingCode}, slot {SlotCode})."),
            T("BOOKING_REMINDER_END", NotificationChannel.InApp, "Sắp hết giờ đỗ", "Lượt đỗ {BookingCode} tại {LotName} kết thúc lúc {EndAt}. Vui lòng xuất xe đúng giờ để tránh phát sinh phí."),
            T("BOOKING_REMINDER_END", NotificationChannel.InApp, "Parking ending soon", "Your parking {BookingCode} at {LotName} ends at {EndAt}. Please check out on time.", "en"),
            T("BOOKING_REMINDER_END", NotificationChannel.Email, "Sắp hết giờ đỗ – {BookingCode}", "Lượt đỗ {BookingCode} tại {LotName} kết thúc lúc {EndAt}."),
            T("LOT_DATA_STALE", NotificationChannel.InApp, "Dữ liệu bãi không cập nhật", "Bãi {LotName} không nhận tín hiệu từ thiết bị kể từ {LastHeartbeat}. Dashboard đang hiển thị dữ liệu cũ — vui lòng kiểm tra thiết bị."),
            T("LOT_DATA_STALE", NotificationChannel.InApp, "Stale lot data", "Lot {LotName} has received no device heartbeat since {LastHeartbeat}. The dashboard is showing stale data — please check your devices.", "en"),
            T("LOT_DATA_RECOVERED", NotificationChannel.InApp, "Dữ liệu bãi đã phục hồi", "Bãi {LotName} đã nhận lại tín hiệu lúc {Heartbeat}. Dashboard trở lại chế độ real-time."),
            T("LOT_DATA_RECOVERED", NotificationChannel.InApp, "Lot data recovered", "Lot {LotName} received a heartbeat at {Heartbeat}. The dashboard is back to real-time.", "en"));
    }
}
