using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GateService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shifts_ParkingLotId_StartedAtUtc",
                table: "Shifts");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartedAtUtc",
                table: "Shifts",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateOnly>(
                name: "BusinessDate",
                table: "Shifts",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<decimal>(
                name: "CashCounted",
                table: "Shifts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CashReceived",
                table: "Shifts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CashSystem",
                table: "Shifts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedReason",
                table: "Shifts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "Shifts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DiscrepancyNote",
                table: "Shifts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscrepancyStatus",
                table: "Shifts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HandoverAcceptedAtUtc",
                table: "Shifts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HandoverAcceptedByUserId",
                table: "Shifts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HandoverToShiftId",
                table: "Shifts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCashHolder",
                table: "Shifts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFlagged",
                table: "Shifts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledEndUtc",
                table: "Shifts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledStartUtc",
                table: "Shifts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "TemplateId",
                table: "Shifts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Shifts",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotedAdditionalDue",
                table: "ParkingSessions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuotedAtUtc",
                table: "ParkingSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotedPrepaidAmount",
                table: "ParkingSessions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotedSessionFee",
                table: "ParkingSessions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAtUtc",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShiftTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsCashHolder = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftTemplates", x => x.Id);
                });

            // Ca cũ: giờ dự kiến = giờ thực; Closed / Flagged → Completed (cờ bất thường tách sang IsFlagged).
            migrationBuilder.Sql("""
                UPDATE "Shifts" SET
                    "ScheduledStartUtc" = "StartedAtUtc",
                    "ScheduledEndUtc" = COALESCE("EndedAtUtc", "StartedAtUtc" + interval '8 hours'),
                    "BusinessDate" = ("StartedAtUtc" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date,
                    "IsFlagged" = ("Status" = 'Flagged'),
                    "IsCashHolder" = TRUE,
                    "ClosedReason" = CASE WHEN "Status" = 'Open' THEN NULL ELSE 'Normal' END,
                    "Status" = CASE WHEN "Status" = 'Open' THEN 'Open' ELSE 'Completed' END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_HandoverToShiftId",
                table: "Shifts",
                column: "HandoverToShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_ParkingLotId_ScheduledStartUtc",
                table: "Shifts",
                columns: new[] { "ParkingLotId", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_StaffUserId_ScheduledStartUtc",
                table: "Shifts",
                columns: new[] { "StaffUserId", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_ParkingLotId_Name",
                table: "ShiftTemplates",
                columns: new[] { "ParkingLotId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_HandoverToShiftId",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_ParkingLotId_ScheduledStartUtc",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_StaffUserId_ScheduledStartUtc",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "BusinessDate",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "CashCounted",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "CashReceived",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "CashSystem",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ClosedReason",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "DiscrepancyNote",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "DiscrepancyStatus",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "HandoverAcceptedAtUtc",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "HandoverAcceptedByUserId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "HandoverToShiftId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "IsCashHolder",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "IsFlagged",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ScheduledEndUtc",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ScheduledStartUtc",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "QuotedAdditionalDue",
                table: "ParkingSessions");

            migrationBuilder.DropColumn(
                name: "QuotedAtUtc",
                table: "ParkingSessions");

            migrationBuilder.DropColumn(
                name: "QuotedPrepaidAmount",
                table: "ParkingSessions");

            migrationBuilder.DropColumn(
                name: "QuotedSessionFee",
                table: "ParkingSessions");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "OutboxMessages");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartedAtUtc",
                table: "Shifts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_ParkingLotId_StartedAtUtc",
                table: "Shifts",
                columns: new[] { "ParkingLotId", "StartedAtUtc" });
        }
    }
}
