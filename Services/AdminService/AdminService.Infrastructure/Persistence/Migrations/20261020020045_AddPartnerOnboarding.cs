using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AdminService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "jsonb", maxLength: 500, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BusinessType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BusinessAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LegalRepresentativeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LegalRepresentativePosition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BusinessLicenseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LotCount = table.Column<int>(type: "integer", nullable: false),
                    PrimaryLotAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EstimatedCarSlots = table.Column<int>(type: "integer", nullable: true),
                    EstimatedMotorbikeSlots = table.Column<int>(type: "integer", nullable: true),
                    OperatingHours = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConsentAcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReviewerAdminId = table.Column<int>(type: "integer", nullable: true),
                    InfoRequestNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContractNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedOwnerProfileId = table.Column<int>(type: "integer", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContractDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerApplicationId = table.Column<int>(type: "integer", nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    TermMonths = table.Column<int>(type: "integer", nullable: false),
                    BankBin = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BankAccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BankAccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RevisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentToEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CreatedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractDrafts_PartnerApplications_PartnerApplicationId",
                        column: x => x.PartnerApplicationId,
                        principalTable: "PartnerApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OwnerContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: false),
                    PartnerApplicationId = table.Column<int>(type: "integer", nullable: false),
                    ContractDraftId = table.Column<int>(type: "integer", nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DraftVersion = table.Column<int>(type: "integer", nullable: false),
                    SignedFileKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SignedFileContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SignedFileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OwnerContracts_ContractDrafts_ContractDraftId",
                        column: x => x.ContractDraftId,
                        principalTable: "ContractDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OwnerContracts_PartnerApplications_PartnerApplicationId",
                        column: x => x.PartnerApplicationId,
                        principalTable: "PartnerApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractDrafts_PartnerApplicationId_Version",
                table: "ContractDrafts",
                columns: new[] { "PartnerApplicationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractDrafts_SingleCurrent",
                table: "ContractDrafts",
                column: "PartnerApplicationId",
                unique: true,
                filter: "\"Status\" = 'Current'");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_SingleActive",
                table: "ContractTemplates",
                column: "IsActive",
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_Version",
                table: "ContractTemplates",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OwnerContracts_ContractDraftId",
                table: "OwnerContracts",
                column: "ContractDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnerContracts_ContractNumber",
                table: "OwnerContracts",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OwnerContracts_OwnerProfileId",
                table: "OwnerContracts",
                column: "OwnerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnerContracts_PartnerApplicationId",
                table: "OwnerContracts",
                column: "PartnerApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_Code",
                table: "PartnerApplications",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_ContractNumber",
                table: "PartnerApplications",
                column: "ContractNumber",
                unique: true,
                filter: "\"ContractNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_Status",
                table: "PartnerApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_TaxCode",
                table: "PartnerApplications",
                column: "TaxCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractTemplates");

            migrationBuilder.DropTable(
                name: "OwnerContracts");

            migrationBuilder.DropTable(
                name: "ContractDrafts");

            migrationBuilder.DropTable(
                name: "PartnerApplications");
        }
    }
}
