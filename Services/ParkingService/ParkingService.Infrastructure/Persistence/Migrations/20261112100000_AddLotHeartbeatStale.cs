using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLotHeartbeatStale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastHeartbeatAtUtc",
                table: "ParkingLots");

            migrationBuilder.DropColumn(
                name: "IsDataStale",
                table: "ParkingLots");
        }
    }
}
