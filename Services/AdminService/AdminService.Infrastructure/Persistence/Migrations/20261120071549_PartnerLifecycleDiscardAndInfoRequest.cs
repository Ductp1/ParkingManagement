using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartnerLifecycleDiscardAndInfoRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "PartnerApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancelledByAdminId",
                table: "PartnerApplications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CleanupError",
                table: "PartnerApplications",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CleanupSyncStatus",
                table: "PartnerApplications",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<DateTime>(
                name: "CleanupSyncedAtUtc",
                table: "PartnerApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InfoProvidedAtUtc",
                table: "PartnerApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InfoRequestDueAtUtc",
                table: "PartnerApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfoRequestEmailError",
                table: "PartnerApplications",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfoRequestEmailStatus",
                table: "PartnerApplications",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<string>(
                name: "InfoRequestItemsJson",
                table: "PartnerApplications",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InfoRequestedAtUtc",
                table: "PartnerApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupersededAtUtc",
                table: "PartnerApplicationDocuments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CancelledByAdminId",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CleanupError",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CleanupSyncStatus",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CleanupSyncedAtUtc",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "InfoProvidedAtUtc",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "InfoRequestDueAtUtc",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "InfoRequestEmailError",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "InfoRequestEmailStatus",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "InfoRequestItemsJson",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "InfoRequestedAtUtc",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "SupersededAtUtc",
                table: "PartnerApplicationDocuments");
        }
    }
}
