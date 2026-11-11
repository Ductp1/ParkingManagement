using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSanctionEnforcement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AppealDeadlineUtc",
                table: "Sanctions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CorrectionDueAtUtc",
                table: "Sanctions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NotificationSentAtUtc",
                table: "Sanctions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificationSyncStatus",
                table: "Sanctions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<int>(
                name: "OwnerUserId",
                table: "Sanctions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParkingSyncStatus",
                table: "Sanctions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<DateTime>(
                name: "ParkingSyncedAtUtc",
                table: "Sanctions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevokeReason",
                table: "Sanctions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAtUtc",
                table: "Sanctions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevokedByUserId",
                table: "Sanctions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sanctions_OwnerProfileId_ActiveOwnerRankingPenalty",
                table: "Sanctions",
                column: "OwnerProfileId",
                unique: true,
                filter: "\"Status\" = 'Active' AND \"ParkingLotId\" IS NULL AND \"Level\" = 'RankingPenalty'");

            migrationBuilder.CreateIndex(
                name: "IX_Sanctions_ParkingLotId_Level_Active",
                table: "Sanctions",
                columns: new[] { "ParkingLotId", "Level" },
                unique: true,
                filter: "\"Status\" = 'Active' AND \"ParkingLotId\" IS NOT NULL AND \"Level\" IN ('RankingPenalty', 'TemporarySuspension')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sanctions_OwnerProfileId_ActiveOwnerRankingPenalty",
                table: "Sanctions");

            migrationBuilder.DropIndex(
                name: "IX_Sanctions_ParkingLotId_Level_Active",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "AppealDeadlineUtc",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "CorrectionDueAtUtc",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "NotificationSentAtUtc",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "NotificationSyncStatus",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "ParkingSyncStatus",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "ParkingSyncedAtUtc",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "RevokeReason",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "RevokedAtUtc",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "RevokedByUserId",
                table: "Sanctions");
        }
    }
}
