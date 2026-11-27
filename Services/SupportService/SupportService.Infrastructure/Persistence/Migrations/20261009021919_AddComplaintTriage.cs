using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComplaintTriage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OwnerProfileId",
                table: "Complaints",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "AssignedTeam",
                table: "Complaints",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Support");

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Complaints",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Low");

            // Phân loại lại ticket đã có theo ComplaintRules.Classify (US-088 AC3).
            migrationBuilder.Sql("""
                UPDATE "Complaints" SET "Priority" = 'High', "AssignedTeam" = 'Supervisor' WHERE "Category" IN ('LotFull', 'Damage');
                UPDATE "Complaints" SET "Priority" = 'Medium' WHERE "Category" IN ('Overcharge', 'RefundRequest', 'AppError');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_AssignedTeam_Priority_Status",
                table: "Complaints",
                columns: new[] { "AssignedTeam", "Priority", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Complaints_AssignedTeam_Priority_Status",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "AssignedTeam",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Complaints");

            migrationBuilder.AlterColumn<int>(
                name: "OwnerProfileId",
                table: "Complaints",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
