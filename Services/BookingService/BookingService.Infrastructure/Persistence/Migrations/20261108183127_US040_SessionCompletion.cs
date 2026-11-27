using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class US040_SessionCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutoProcessStage",
                table: "Bookings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "None"); // BookingAutoStage.None – các booking hiện có chưa qua xử lý tự động

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status_EndAtUtc",
                table: "Bookings",
                columns: new[] { "Status", "EndAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_Status_EndAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AutoProcessStage",
                table: "Bookings");
        }
    }
}
