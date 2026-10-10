using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSanctionActiveOwnerLockIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sanctions_OwnerProfileId_ActiveOwnerLock",
                table: "Sanctions",
                column: "OwnerProfileId",
                unique: true,
                filter: "\"Status\" = 'Active' AND \"ParkingLotId\" IS NULL AND \"Level\" IN ('TemporarySuspension', 'PermanentBan')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sanctions_OwnerProfileId_ActiveOwnerLock",
                table: "Sanctions");
        }
    }
}
