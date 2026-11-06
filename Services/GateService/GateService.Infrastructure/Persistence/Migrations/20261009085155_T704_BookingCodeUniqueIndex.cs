using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GateService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T704_BookingCodeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_BookingCode",
                table: "ParkingSessions",
                column: "BookingCode",
                unique: true,
                filter: "\"BookingCode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParkingSessions_BookingCode",
                table: "ParkingSessions");
        }
    }
}
