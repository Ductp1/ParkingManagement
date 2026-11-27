using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewModerationByAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FlaggedAtUtc",
                table: "Reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FlaggedByOwnerProfileId",
                table: "Reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenAtUtc",
                table: "Reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HiddenByUserId",
                table: "Reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenReason",
                table: "Reviews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Reviews",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            // Sửa dữ liệu cũ trước khi thêm CHECK: đánh giá đã ẩn từ trước không có thông tin ai/khi nào gỡ
            // (HiddenByUserId = 0 nghĩa là "dữ liệu cũ, không rõ người gỡ"); đánh giá đã ẩn thì bỏ khỏi hàng chờ khiếu nại.
            migrationBuilder.Sql("""
                UPDATE "Reviews" SET "IsFlagged" = FALSE WHERE "IsHidden" AND "IsFlagged";
                UPDATE "Reviews" SET "HiddenAtUtc" = COALESCE("UpdatedAtUtc", "CreatedAtUtc"), "HiddenByUserId" = 0
                WHERE "IsHidden" AND ("HiddenAtUtc" IS NULL OR "HiddenByUserId" IS NULL);
                UPDATE "Reviews" SET "FlaggedAtUtc" = COALESCE("UpdatedAtUtc", "CreatedAtUtc") WHERE "IsFlagged" AND "FlaggedAtUtc" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Flagged_Queue",
                table: "Reviews",
                column: "FlaggedAtUtc",
                filter: "\"IsFlagged\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reviews_Hidden_HasAudit",
                table: "Reviews",
                sql: "NOT \"IsHidden\" OR (\"HiddenAtUtc\" IS NOT NULL AND \"HiddenByUserId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reviews_Hidden_NotFlagged",
                table: "Reviews",
                sql: "NOT (\"IsHidden\" AND \"IsFlagged\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_Flagged_Queue",
                table: "Reviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reviews_Hidden_HasAudit",
                table: "Reviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reviews_Hidden_NotFlagged",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "FlaggedAtUtc",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "FlaggedByOwnerProfileId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HiddenAtUtc",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HiddenByUserId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HiddenReason",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Reviews");
        }
    }
}
