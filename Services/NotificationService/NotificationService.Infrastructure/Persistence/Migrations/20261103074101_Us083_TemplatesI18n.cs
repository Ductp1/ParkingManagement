using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NotificationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Us083_TemplatesI18n : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "BodyTemplate", "Channel", "Language", "TitleTemplate" },
                values: new object[] { "Booking {BookingCode} at {LotName} starting {StartAt} has been confirmed.", "InApp", "en", "Booking confirmed" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "BodyTemplate", "Channel", "Key", "TitleTemplate" },
                values: new object[] { "Bạn đã đặt chỗ tại {LotName}, slot {SlotCode}, từ {StartAt} đến {EndAt}. Số tiền: {Amount}.", "Email", "BOOKING_CONFIRMED", "Xác nhận đặt chỗ {BookingCode}" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "BodyTemplate", "Channel", "Key", "Language", "TitleTemplate" },
                values: new object[] { "Your booking at {LotName}, slot {SlotCode}, from {StartAt} to {EndAt}. Amount: {Amount}.", "Email", "BOOKING_CONFIRMED", "en", "Booking {BookingCode} confirmed" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "BodyTemplate", "Key", "TitleTemplate" },
                values: new object[] { "Booking {BookingCode} đã hết 15 phút giữ chỗ do chưa thanh toán.", "BOOKING_HOLD_EXPIRED", "Hết thời gian giữ chỗ" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "BodyTemplate", "Key", "TitleTemplate" },
                values: new object[] { "Booking {BookingCode} bắt đầu lúc {StartAt}. Bạn có 15 phút ân hạn khi đến trễ.", "BOOKING_REMINDER", "Sắp đến giờ đỗ xe" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "BodyTemplate", "Key", "TitleTemplate" },
                values: new object[] { "Booking {BookingCode} đã hủy. Hoàn tiền: {RefundAmount}.", "BOOKING_CANCELLED", "Đã hủy booking" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "BodyTemplate", "Key", "Language", "TitleTemplate" },
                values: new object[] { "Booking {BookingCode} has been cancelled. Refund: {RefundAmount}.", "BOOKING_CANCELLED", "en", "Booking cancelled" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "BodyTemplate", "Key", "TitleTemplate" },
                values: new object[] { "Booking {BookingCode} đã bị hủy vì quá 30 phút chưa check-in.", "BOOKING_NO_SHOW", "Booking bị hủy do không đến" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "BodyTemplate", "Channel", "Key", "TitleTemplate" },
                values: new object[] { "Xe {PlateNumber} đã vào {LotName} lúc {EntryAt}.", "InApp", "CHECKIN_SUCCESS", "Xe đã vào bãi" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "BodyTemplate", "Channel", "Key", "TitleTemplate" },
                values: new object[] { "Xe {PlateNumber} ra lúc {ExitAt}. Tổng phí: {Amount}.", "InApp", "CHECKOUT_RECEIPT", "Xe đã ra bãi" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "BodyTemplate", "Key", "TitleTemplate" },
                values: new object[] { "Đã nhận {Amount} qua {Method}. Cảm ơn bạn!", "PAYMENT_SUCCEEDED", "Thanh toán thành công" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "BodyTemplate", "Channel", "Key", "Language", "TitleTemplate" },
                values: new object[] { "We received {Amount} via {Method}. Thank you!", "InApp", "PAYMENT_SUCCEEDED", "en", "Payment successful" });

            migrationBuilder.InsertData(
                table: "NotificationTemplates",
                columns: new[] { "Id", "BodyTemplate", "Channel", "CreatedAtUtc", "IsActive", "Key", "Language", "TitleTemplate", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 15, "Thanh toán cho booking {BookingCode} không thành công ({Reason}). Vui lòng thử lại trong 15 phút giữ chỗ.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "PAYMENT_FAILED", "vi", "Thanh toán thất bại", null },
                    { 16, "Bãi {LotName} đã được duyệt và hiển thị trên Smart Parking.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "KYB_APPROVED", "vi", "Bãi xe đã được duyệt", null },
                    { 17, "Hồ sơ bãi {LotName} chưa được duyệt. Lý do: {Reason}.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "KYB_REJECTED", "vi", "Hồ sơ bãi xe cần bổ sung", null },
                    { 18, "Khiếu nại {ComplaintCode} cho bãi {LotName}. Vui lòng phản hồi trong 48 giờ.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "COMPLAINT_CREATED", "vi", "Có khiếu nại mới", null },
                    { 19, "Bãi {LotName} tạm đóng. Booking {BookingCode} sẽ được hoàn tiền hoặc đổi bãi.", "Sms", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE", "vi", "Bãi đóng khẩn cấp", null },
                    { 20, "Lot {LotName} is temporarily closed. Booking {BookingCode} will be refunded or relocated.", "Sms", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE", "en", "Emergency closure", null },
                    { 21, "Bãi {LotName} tạm đóng khẩn cấp. Lý do: {Reason}. Booking {BookingCode} của bạn bị ảnh hưởng — đội hỗ trợ sẽ liên hệ hoàn tiền hoặc đổi bãi sớm nhất.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE", "vi", "Bãi đóng khẩn cấp", null },
                    { 22, "Lot {LotName} is temporarily closed. Reason: {Reason}. Your booking {BookingCode} is affected — support will contact you for a refund or relocation shortly.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE", "en", "Emergency closure", null },
                    { 23, "Bãi {LotName} tạm đóng khẩn cấp. Lý do: {Reason}. Booking {BookingCode} của bạn bị ảnh hưởng, chúng tôi sẽ xử lý hoàn tiền/đổi bãi.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE", "vi", "Bãi {LotName} đóng khẩn cấp – booking {BookingCode}", null },
                    { 24, "Bãi {LotName} tạm đóng. Booking {BookingCode} bị ảnh hưởng.", "Push", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE", "vi", "Bãi đóng khẩn cấp", null },
                    { 25, "Booking {BookingCode} tại {LotName}: {StartAt} → {EndAt}.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "OWNER_BOOKING_NEW", "vi", "Có đặt chỗ mới", null },
                    { 26, "Booking {BookingCode} tại {LotName} vừa bị hủy. Lý do: {Reason}.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "OWNER_BOOKING_CANCELLED", "vi", "Booking đã hủy", null },
                    { 27, "Booking {BookingCode} tại {LotName} đã đổi: {ChangedNote}. Thời gian mới: {StartAt} → {EndAt}.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "OWNER_BOOKING_MODIFIED", "vi", "Booking thay đổi", null },
                    { 28, "Bạn có lịch đỗ tại {LotName} lúc {StartAt} (mã {BookingCode}). Xuất phát đúng giờ nhé!", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "BOOKING_REMINDER_ARRIVAL", "vi", "Nhắc lịch đỗ xe", null },
                    { 29, "Your parking at {LotName} starts at {StartAt} (booking {BookingCode}).", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "BOOKING_REMINDER_ARRIVAL", "en", "Parking reminder", null },
                    { 30, "Bạn có lịch đỗ tại {LotName} lúc {StartAt} (mã {BookingCode}, slot {SlotCode}).", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "BOOKING_REMINDER_ARRIVAL", "vi", "Nhắc lịch: đỗ xe lúc {StartAt}", null },
                    { 31, "Lượt đỗ {BookingCode} tại {LotName} kết thúc lúc {EndAt}. Vui lòng xuất xe đúng giờ để tránh phát sinh phí.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "BOOKING_REMINDER_END", "vi", "Sắp hết giờ đỗ", null },
                    { 32, "Your parking {BookingCode} at {LotName} ends at {EndAt}. Please check out on time.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "BOOKING_REMINDER_END", "en", "Parking ending soon", null },
                    { 33, "Lượt đỗ {BookingCode} tại {LotName} kết thúc lúc {EndAt}.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "BOOKING_REMINDER_END", "vi", "Sắp hết giờ đỗ – {BookingCode}", null },
                    { 34, "Bãi {LotName} không nhận tín hiệu từ thiết bị kể từ {LastHeartbeat}. Dashboard đang hiển thị dữ liệu cũ — vui lòng kiểm tra thiết bị.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "LOT_DATA_STALE", "vi", "Dữ liệu bãi không cập nhật", null },
                    { 35, "Lot {LotName} has received no device heartbeat since {LastHeartbeat}. The dashboard is showing stale data — please check your devices.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "LOT_DATA_STALE", "en", "Stale lot data", null },
                    { 36, "Bãi {LotName} đã nhận lại tín hiệu lúc {Heartbeat}. Dashboard trở lại chế độ real-time.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "LOT_DATA_RECOVERED", "vi", "Dữ liệu bãi đã phục hồi", null },
                    { 37, "Lot {LotName} received a heartbeat at {Heartbeat}. The dashboard is back to real-time.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "LOT_DATA_RECOVERED", "en", "Lot data recovered", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "BodyTemplate", "Channel", "Language", "TitleTemplate" },
                values: new object[] { "Bạn đã đặt chỗ tại {LotName}, slot {SlotCode}, từ {StartAt} đến {EndAt}. Số tiền: {Amount}.", "Email", "vi", "Xác nhận đặt chỗ {BookingCode}" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "BodyTemplate", "Channel", "Key", "TitleTemplate" },
                values: new object[] { "Booking {BookingCode} đã hết 15 phút giữ chỗ do chưa thanh toán.", "InApp", "BOOKING_HOLD_EXPIRED", "Hết thời gian giữ chỗ" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "BodyTemplate", "Channel", "Key", "Language", "TitleTemplate" },
                values: new object[] { "Booking {BookingCode} bắt đầu lúc {StartAt}. Bạn có 15 phút ân hạn khi đến trễ.", "InApp", "BOOKING_REMINDER", "vi", "Sắp đến giờ đỗ xe" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "BodyTemplate", "Key", "TitleTemplate" },
                values: new object[] { "Booking {BookingCode} đã hủy. Hoàn tiền: {RefundAmount}.", "BOOKING_CANCELLED", "Đã hủy booking" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "BodyTemplate", "Key", "TitleTemplate" },
                values: new object[] { "Booking {BookingCode} đã bị hủy vì quá 30 phút chưa check-in.", "BOOKING_NO_SHOW", "Booking bị hủy do không đến" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "BodyTemplate", "Key", "TitleTemplate" },
                values: new object[] { "Xe {PlateNumber} đã vào {LotName} lúc {EntryAt}.", "CHECKIN_SUCCESS", "Xe đã vào bãi" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "BodyTemplate", "Key", "Language", "TitleTemplate" },
                values: new object[] { "Xe {PlateNumber} ra lúc {ExitAt}. Tổng phí: {Amount}.", "CHECKOUT_RECEIPT", "vi", "Xe đã ra bãi" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "BodyTemplate", "Key", "TitleTemplate" },
                values: new object[] { "Thanh toán cho {BookingCode} không thành công. Vui lòng thử lại.", "PAYMENT_FAILED", "Thanh toán thất bại" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "BodyTemplate", "Channel", "Key", "TitleTemplate" },
                values: new object[] { "Bãi {LotName} đã được duyệt và hiển thị trên Smart Parking.", "Email", "KYB_APPROVED", "Bãi xe đã được duyệt" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "BodyTemplate", "Channel", "Key", "TitleTemplate" },
                values: new object[] { "Hồ sơ bãi {LotName} chưa được duyệt. Lý do: {Reason}.", "Email", "KYB_REJECTED", "Hồ sơ bãi xe cần bổ sung" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "BodyTemplate", "Key", "TitleTemplate" },
                values: new object[] { "Khiếu nại {ComplaintCode} cho bãi {LotName}. Vui lòng phản hồi trong 48 giờ.", "COMPLAINT_CREATED", "Có khiếu nại mới" });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "BodyTemplate", "Channel", "Key", "Language", "TitleTemplate" },
                values: new object[] { "Bãi {LotName} tạm đóng. Booking {BookingCode} sẽ được hoàn tiền hoặc đổi bãi.", "Sms", "EMERGENCY_CLOSURE", "vi", "Bãi đóng khẩn cấp" });
        }
    }
}
