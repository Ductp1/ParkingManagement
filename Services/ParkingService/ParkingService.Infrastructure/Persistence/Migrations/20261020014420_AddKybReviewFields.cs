using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKybReviewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FieldSurveyCompletedAtUtc",
                table: "KybApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "KybApplications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "KybApplications",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FieldSurveyCompletedAtUtc",
                table: "KybApplications");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "KybApplications");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "KybApplications");
        }
    }
}
