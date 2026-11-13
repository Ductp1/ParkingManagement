using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ParkingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Us067068069_LotOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmergencyStopReason",
                table: "ParkingLots",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmergencyStoppedAtUtc",
                table: "ParkingLots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDataStale",
                table: "ParkingLots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartbeatAtUtc",
                table: "ParkingLots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmergencyStopLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AffectedBookings = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyStopLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingLots_IsDataStale",
                table: "ParkingLots",
                column: "IsDataStale");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyStopLogs_ParkingLotId_CreatedAtUtc",
                table: "EmergencyStopLogs",
                columns: new[] { "ParkingLotId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmergencyStopLogs");

            migrationBuilder.DropIndex(
                name: "IX_ParkingLots_IsDataStale",
                table: "ParkingLots");

            migrationBuilder.DropColumn(
                name: "EmergencyStopReason",
                table: "ParkingLots");

            migrationBuilder.DropColumn(
                name: "EmergencyStoppedAtUtc",
                table: "ParkingLots");

            migrationBuilder.DropColumn(
                name: "IsDataStale",
                table: "ParkingLots");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatAtUtc",
                table: "ParkingLots");
        }
    }
}
