using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NotificationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Us086_EmergencyNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DedupeKey",
                table: "Notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchKey",
                table: "Notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmergencyAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    LotName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StoppedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalRecipients = table.Column<int>(type: "integer", nullable: false),
                    StoppedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyAlerts", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "NotificationTemplates",
                columns: new[] { "Id", "BodyTemplate", "Channel", "CreatedAtUtc", "IsActive", "Key", "Language", "TitleTemplate", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 38, "Bãi {LotName} không nhận heartbeat kể từ {LastHeartbeat}. Dashboard đang hiển thị dữ liệu cũ (US-068) — vui lòng kiểm tra thiết bị/controller của bãi.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "LOT_DATA_STALE", "vi", "Cảnh báo: bãi {LotName} mất tín hiệu thiết bị", null },
                    { 39, "Lot {LotName} received no heartbeat since {LastHeartbeat}. The dashboard is showing stale data (US-068) — please check your devices/controllers.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "LOT_DATA_STALE", "en", "Alert: lot {LotName} lost device heartbeat", null },
                    { 40, "Lot {LotName} is temporarily closed. Reason: {Reason}. Your booking {BookingCode} is affected — we will process a refund or relocation.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE", "en", "Emergency closure at {LotName} – booking {BookingCode}", null },
                    { 41, "Lot {LotName} is temporarily closed. Booking {BookingCode} is affected.", "Push", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE", "en", "Emergency closure", null },
                    { 42, "Bãi {LotName} đã ĐÓNG khẩn cấp lúc {StoppedAt}. Lý do: {Reason}. {Count} booking bị ảnh hưởng đã được thông báo đa kênh.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE_OWNER", "vi", "Bạn đã đóng bãi khẩn cấp", null },
                    { 43, "Lot {LotName} was emergency-stopped at {StoppedAt}. Reason: {Reason}. {Count} affected bookings have been notified across channels.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE_OWNER", "en", "Emergency stop triggered", null },
                    { 44, "Bãi {LotName} đã ĐÓNG khẩn cấp lúc {StoppedAt}. Lý do: {Reason}. {Count} booking bị ảnh hưởng — khách đã nhận thông báo đa kênh (push/SMS/email/in-app). Booking mới bị chặn cho đến khi mở lại bãi.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE_OWNER", "vi", "[KHẨN] Bãi {LotName} đã đóng khẩn cấp", null },
                    { 45, "Lot {LotName} was emergency-stopped at {StoppedAt}. Reason: {Reason}. {Count} affected bookings were notified across channels (push/SMS/email/in-app). New bookings are blocked until you lift the closure.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE_OWNER", "en", "[URGENT] Lot {LotName} emergency-stopped", null },
                    { 46, "Bãi {LotName} đã MỞ lại lúc {ReopenedAt}. Đặt chỗ và check-in hoạt động bình thường.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "LOT_REOPENED", "vi", "Bãi đã mở lại", null },
                    { 47, "Lot {LotName} reopened at {ReopenedAt}. Bookings and check-ins are working normally.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "LOT_REOPENED", "en", "Lot reopened", null },
                    { 48, "Bãi {LotName} đã MỞ lại lúc {ReopenedAt} sau sự cố khẩn cấp. Đặt chỗ và check-in hoạt động bình thường.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "LOT_REOPENED", "vi", "Bãi {LotName} đã mở lại", null },
                    { 49, "Lot {LotName} reopened at {ReopenedAt} after the emergency closure. Bookings and check-ins are working normally.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "LOT_REOPENED", "en", "Lot {LotName} reopened", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_BatchKey",
                table: "Notifications",
                column: "BatchKey");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyAlerts_BatchKey",
                table: "EmergencyAlerts",
                column: "BatchKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyAlerts_ParkingLotId_CreatedAtUtc",
                table: "EmergencyAlerts",
                columns: new[] { "ParkingLotId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmergencyAlerts");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_BatchKey",
                table: "Notifications");

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DropColumn(
                name: "BatchKey",
                table: "Notifications");

            migrationBuilder.AlterColumn<string>(
                name: "DedupeKey",
                table: "Notifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
