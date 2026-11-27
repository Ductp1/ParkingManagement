using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminService.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// US-116: màn hình Contract Templates (Status Draft / Active / Retired thay IsActive, Bên A theo phiên bản, RowVersion)
    /// + thỏa thuận riêng của từng phiên bản hợp đồng bãi. Chuyển dữ liệu IsActive → Status bằng SQL tay trước khi xóa cột.
    /// </summary>
    public partial class ContractTemplateEditorAndSpecialTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpecialTerms",
                table: "LotContractVersions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAtUtc",
                table: "ContractTemplates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeNote",
                table: "ContractTemplates",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByAdminId",
                table: "ContractTemplates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformAddress",
                table: "ContractTemplates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlatformEmail",
                table: "ContractTemplates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlatformName",
                table: "ContractTemplates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlatformPhone",
                table: "ContractTemplates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlatformRepresentativeName",
                table: "ContractTemplates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlatformRepresentativePosition",
                table: "ContractTemplates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlatformTaxCode",
                table: "ContractTemplates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ContractTemplates",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ContractTemplates",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            // Dữ liệu cũ: IsActive → Status (mẫu đang dùng = Active, còn lại = Retired); mốc áp dụng lấy EffectiveFromUtc.
            migrationBuilder.Sql("""
                UPDATE "ContractTemplates"
                SET "Status" = CASE WHEN "IsActive" THEN 'Active' ELSE 'Retired' END,
                    "ActivatedAtUtc" = "EffectiveFromUtc";
                """);

            migrationBuilder.DropIndex(
                name: "IX_ContractTemplates_SingleActive",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ContractTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_SingleActive",
                table: "ContractTemplates",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_SingleDraft",
                table: "ContractTemplates",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 'Draft'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContractTemplates_SingleActive",
                table: "ContractTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ContractTemplates_SingleDraft",
                table: "ContractTemplates");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ContractTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""UPDATE "ContractTemplates" SET "IsActive" = ("Status" = 'Active');""");

            migrationBuilder.DropColumn(
                name: "SpecialTerms",
                table: "LotContractVersions");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "ChangeNote",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedByAdminId",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "PlatformAddress",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "PlatformEmail",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "PlatformName",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "PlatformPhone",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "PlatformRepresentativeName",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "PlatformRepresentativePosition",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "PlatformTaxCode",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ContractTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_SingleActive",
                table: "ContractTemplates",
                column: "IsActive",
                unique: true,
                filter: "\"IsActive\" = TRUE");
        }
    }
}
