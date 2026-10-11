using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSanctionUserServiceSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserServiceSyncStatus",
                table: "Sanctions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<DateTime>(
                name: "UserServiceSyncedAtUtc",
                table: "Sanctions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserServiceSyncStatus",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "UserServiceSyncedAtUtc",
                table: "Sanctions");
        }
    }
}
