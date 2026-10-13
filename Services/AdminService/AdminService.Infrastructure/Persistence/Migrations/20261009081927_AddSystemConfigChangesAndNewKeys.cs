using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AdminService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemConfigChangesAndNewKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemConfigChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SystemConfigId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<int>(type: "integer", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemConfigChanges_SystemConfigs_SystemConfigId",
                        column: x => x.SystemConfigId,
                        principalTable: "SystemConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "SystemConfigs",
                columns: new[] { "Id", "CreatedAtUtc", "DataType", "Description", "Key", "UpdatedAtUtc", "UpdatedByUserId", "Value" },
                values: new object[,]
                {
                    { 25, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", "Sàn giá gửi xe mỗi giờ (giá trị tạm, chờ PO)", "PRICE_FLOOR_PER_HOUR_VND", null, null, "5000" },
                    { 26, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", "Trần giá gửi xe mỗi giờ (giá trị tạm, chờ PO)", "PRICE_CEILING_PER_HOUR_VND", null, null, "200000" },
                    { 27, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", "Tăng giá một lần vượt tỷ lệ này phải được Admin duyệt", "PRICE_INCREASE_APPROVAL_THRESHOLD_RATE", null, null, "0.30" },
                    { 28, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Số lần đặt/hủy trong 1 giờ trước khi tạm chặn và gắn cờ tài khoản", "FRAUD_BOOKING_CANCEL_MAX_PER_HOUR", null, null, "10" },
                    { 29, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", "Tỷ lệ no-show bị hạn chế đặt chỗ (giá trị tạm, chờ PO)", "FRAUD_NO_SHOW_RATE_THRESHOLD", null, null, "0.30" },
                    { 30, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Số booking Staff hủy trong 1 giờ trước khi cảnh báo (giá trị tạm, chờ PO)", "FRAUD_STAFF_CANCEL_MAX_PER_HOUR", null, null, "5" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigChanges_SystemConfigId_EffectiveFromUtc_Active",
                table: "SystemConfigChanges",
                columns: new[] { "SystemConfigId", "EffectiveFromUtc" },
                unique: true,
                filter: "\"CancelledAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemConfigChanges");

            migrationBuilder.DeleteData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: 30);
        }
    }
}
