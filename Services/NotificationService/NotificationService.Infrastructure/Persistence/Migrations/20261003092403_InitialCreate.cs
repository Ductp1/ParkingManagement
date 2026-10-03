using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NotificationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Platform = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    QuietFrom = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    QuietTo = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TitleTemplate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BodyTemplate = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", maxLength: 500, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<int>(type: "integer", nullable: true),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DataJson = table.Column<string>(type: "text", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_NotificationTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "NotificationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "NotificationTemplates",
                columns: new[] { "Id", "BodyTemplate", "Channel", "CreatedAtUtc", "IsActive", "Key", "TitleTemplate", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, "Mã OTP Smart Parking của bạn là {Code}, hiệu lực 5 phút.", "Sms", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "OTP_CODE", "Mã xác thực", null },
                    { 2, "Booking {BookingCode} tại {LotName} lúc {StartAt} đã được xác nhận.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "BOOKING_CONFIRMED", "Đặt chỗ thành công", null },
                    { 3, "Bạn đã đặt chỗ tại {LotName}, slot {SlotCode}, từ {StartAt} đến {EndAt}. Số tiền: {Amount}.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "BOOKING_CONFIRMED", "Xác nhận đặt chỗ {BookingCode}", null },
                    { 4, "Booking {BookingCode} đã hết 15 phút giữ chỗ do chưa thanh toán.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "BOOKING_HOLD_EXPIRED", "Hết thời gian giữ chỗ", null },
                    { 5, "Booking {BookingCode} bắt đầu lúc {StartAt}. Bạn có 15 phút ân hạn khi đến trễ.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "BOOKING_REMINDER", "Sắp đến giờ đỗ xe", null },
                    { 6, "Booking {BookingCode} đã hủy. Hoàn tiền: {RefundAmount}.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "BOOKING_CANCELLED", "Đã hủy booking", null },
                    { 7, "Booking {BookingCode} đã bị hủy vì quá 30 phút chưa check-in.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "BOOKING_NO_SHOW", "Booking bị hủy do không đến", null },
                    { 8, "Xe {PlateNumber} đã vào {LotName} lúc {EntryAt}.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "CHECKIN_SUCCESS", "Xe đã vào bãi", null },
                    { 9, "Xe {PlateNumber} ra lúc {ExitAt}. Tổng phí: {Amount}.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "CHECKOUT_RECEIPT", "Xe đã ra bãi", null },
                    { 10, "Thanh toán cho {BookingCode} không thành công. Vui lòng thử lại.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "PAYMENT_FAILED", "Thanh toán thất bại", null },
                    { 11, "Bãi {LotName} đã được duyệt và hiển thị trên Smart Parking.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "KYB_APPROVED", "Bãi xe đã được duyệt", null },
                    { 12, "Hồ sơ bãi {LotName} chưa được duyệt. Lý do: {Reason}.", "Email", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "KYB_REJECTED", "Hồ sơ bãi xe cần bổ sung", null },
                    { 13, "Khiếu nại {ComplaintCode} cho bãi {LotName}. Vui lòng phản hồi trong 48 giờ.", "InApp", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "COMPLAINT_CREATED", "Có khiếu nại mới", null },
                    { 14, "Bãi {LotName} tạm đóng. Booking {BookingCode} sẽ được hoàn tiền hoặc đổi bãi.", "Sms", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EMERGENCY_CLOSURE", "Bãi đóng khẩn cấp", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_Token",
                table: "DeviceTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_UserId_IsRevoked",
                table: "DeviceTokens",
                columns: new[] { "UserId", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId_TemplateKey_Channel",
                table: "NotificationPreferences",
                columns: new[] { "UserId", "TemplateKey", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Status_RetryCount",
                table: "Notifications",
                columns: new[] { "Status", "RetryCount" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TemplateId",
                table: "Notifications",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAtUtc",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Key_Channel",
                table: "NotificationTemplates",
                columns: new[] { "Key", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAtUtc", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceTokens");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");
        }
    }
}
