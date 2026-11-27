using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminService.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// US-116 v11: mẫu hợp đồng có loại (Markup / Structured / Document), nội dung form (jsonb), file DOCX gốc, nhiều mẫu Active cùng lúc;
    /// phiên bản hợp đồng bãi lưu TemplateId và cho phép ngày hiệu lực / thời hạn trống (hợp đồng theo file DOCX).
    /// </summary>
    public partial class ContractTemplateKindsAndDocxImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContractTemplates_SingleActive",
                table: "ContractTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ContractTemplates_SingleDraft",
                table: "ContractTemplates");

            migrationBuilder.AlterColumn<int>(
                name: "TermMonths",
                table: "LotContractVersions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EffectiveTo",
                table: "LotContractVersions",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EffectiveFrom",
                table: "LotContractVersions",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<int>(
                name: "TemplateId",
                table: "LotContractVersions",
                type: "integer",
                nullable: true);

            // Phiên bản hợp đồng bãi đã sinh: gán mẫu theo số phiên bản mẫu (TemplateVersion duy nhất).
            migrationBuilder.Sql("""
                UPDATE "LotContractVersions" AS v
                SET "TemplateId" = t."Id"
                FROM "ContractTemplates" AS t
                WHERE t."Version" = v."TemplateVersion";
                """);

            migrationBuilder.AddColumn<string>(
                name: "ContentJson",
                table: "ContractTemplates",
                type: "jsonb",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportNotes",
                table: "ContractTemplates",
                type: "text",
                maxLength: 500,
                nullable: true);

            // Mẫu có sẵn (v1–v5) đều gõ markup tay → Kind = Markup.
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "ContractTemplates",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Markup");

            migrationBuilder.AddColumn<string>(
                name: "SourceFileKey",
                table: "ContractTemplates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "ContractTemplates",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileSha256",
                table: "ContractTemplates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceTemplateId",
                table: "ContractTemplates",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotContractVersions_TemplateId",
                table: "LotContractVersions",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_Status",
                table: "ContractTemplates",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LotContractVersions_TemplateId",
                table: "LotContractVersions");

            migrationBuilder.DropIndex(
                name: "IX_ContractTemplates_Status",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "LotContractVersions");

            migrationBuilder.DropColumn(
                name: "ContentJson",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "ImportNotes",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "SourceFileKey",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "SourceFileSha256",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "SourceTemplateId",
                table: "ContractTemplates");

            // Hợp đồng theo file DOCX không có ngày / thời hạn: điền giá trị tạm trước khi đặt lại NOT NULL.
            migrationBuilder.Sql("""
                UPDATE "LotContractVersions"
                SET "EffectiveFrom" = COALESCE("EffectiveFrom", "CreatedAtUtc"::date),
                    "TermMonths" = COALESCE("TermMonths", 12),
                    "EffectiveTo" = COALESCE("EffectiveTo", ("CreatedAtUtc"::date + INTERVAL '12 months' - INTERVAL '1 day')::date);
                """);

            migrationBuilder.AlterColumn<int>(
                name: "TermMonths",
                table: "LotContractVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EffectiveTo",
                table: "LotContractVersions",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EffectiveFrom",
                table: "LotContractVersions",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            // Quay lại "tối đa một Active / một Draft": giữ mẫu mới nhất, các mẫu khác → Retired.
            migrationBuilder.Sql("""
                UPDATE "ContractTemplates" SET "Status" = 'Retired'
                WHERE "Status" = 'Active' AND "Id" <> (SELECT MAX("Id") FROM "ContractTemplates" WHERE "Status" = 'Active');
                UPDATE "ContractTemplates" SET "Status" = 'Retired'
                WHERE "Status" = 'Draft' AND "Id" <> (SELECT MAX("Id") FROM "ContractTemplates" WHERE "Status" = 'Draft');
                """);

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
    }
}
