using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NotificationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "NotificationTemplates",
                columns: new[] { "Id", "BodyTemplate", "Channel", "CreatedAtUtc", "IsActive", "Key", "Language", "TitleTemplate", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 50, "Nhân viên #{StaffId} chưa bắt đầu ca {StartAt} → {EndAt} tại bãi #{LotId}.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "SHIFT_MISSED", "vi", "Ca trực chưa bắt đầu", null },
                    { 51, "Ca #{ShiftId} của nhân viên #{StaffId} tại bãi #{LotId} đã tự đóng: {Reason}.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "SHIFT_AUTO_CLOSED", "vi", "Ca trực bị đóng tự động", null },
                    { 52, "Ca #{ShiftId} tại bãi #{LotId} đã kết thúc, chờ đếm tiền và nhận bàn giao.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "SHIFT_HANDOVER_PENDING", "vi", "Có bàn giao ca chờ nhận", null },
                    { 53, "Ca #{ShiftId} tại bãi #{LotId} lệch {Amount}. Vui lòng kiểm tra.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "SHIFT_CASH_DISCREPANCY", "vi", "Lệch tiền mặt", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 53);
        }
    }
}
