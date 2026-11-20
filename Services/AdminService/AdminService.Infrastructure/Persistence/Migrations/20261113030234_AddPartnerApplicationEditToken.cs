using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerApplicationEditToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EditTokenExpiresAtUtc",
                table: "PartnerApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditTokenHash",
                table: "PartnerApplications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditTokenExpiresAtUtc",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "EditTokenHash",
                table: "PartnerApplications");
        }
    }
}
