using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComplaintConcurrencyAndPayoutGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Complaints",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            // Sửa dữ liệu cũ trước khi thêm CHECK: bản trước có thể đã giữ tiền khi chưa biết chủ bãi hoặc khi ticket đã đóng.
            migrationBuilder.Sql("""
                UPDATE "Complaints" SET "PayoutHeld" = FALSE
                WHERE "PayoutHeld" AND ("OwnerProfileId" IS NULL OR "Status" IN ('Resolved', 'Rejected'));
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Complaints_PayoutHold_OnlyWhileOpen",
                table: "Complaints",
                sql: "NOT \"PayoutHeld\" OR \"Status\" NOT IN ('Resolved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Complaints_PayoutHold_RequiresOwner",
                table: "Complaints",
                sql: "NOT \"PayoutHeld\" OR \"OwnerProfileId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Complaints_PayoutHold_OnlyWhileOpen",
                table: "Complaints");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Complaints_PayoutHold_RequiresOwner",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Complaints");
        }
    }
}
