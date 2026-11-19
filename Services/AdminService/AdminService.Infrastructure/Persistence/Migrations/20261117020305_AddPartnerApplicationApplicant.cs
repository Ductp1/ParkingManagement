using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerApplicationApplicant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplicantUserId",
                table: "PartnerApplications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_ApplicantUserId",
                table: "PartnerApplications",
                column: "ApplicantUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_ApplicantUserId",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "ApplicantUserId",
                table: "PartnerApplications");
        }
    }
}
