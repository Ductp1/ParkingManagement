using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AdminService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LotContractsPerParkingLot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OwnerContracts");

            migrationBuilder.DropTable(
                name: "ContractDrafts");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_ContractNumber",
                table: "PartnerApplications");

            // Sửa tay sau khi scaffold: EF đoán đổi tên ContractNumber → PayoutBankAccountNumber (cùng kiểu chuỗi 30) – sai nghĩa,
            // nên xóa cột cũ và thêm cột mới. Hồ sơ đang ContractPending (trạng thái đã bỏ) quay về UnderReview.
            migrationBuilder.Sql("UPDATE \"PartnerApplications\" SET \"Status\" = 'UnderReview' WHERE \"Status\" = 'ContractPending';");

            migrationBuilder.DropColumn(
                name: "ContractNumber",
                table: "PartnerApplications");

            migrationBuilder.AddColumn<string>(
                name: "PayoutBankAccountNumber",
                table: "PartnerApplications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutBankAccountName",
                table: "PartnerApplications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutBankBin",
                table: "PartnerApplications",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutBankName",
                table: "PartnerApplications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LotContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    KybApplicationId = table.Column<int>(type: "integer", nullable: false),
                    LotName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LotAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    LotCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LotDistrict = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LotTotalSlots = table.Column<int>(type: "integer", nullable: false),
                    LotOpeningHours = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: false),
                    PartnerApplicationId = table.Column<int>(type: "integer", nullable: false),
                    ApplicationCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BusinessAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LegalRepresentativeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LegalRepresentativePosition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BankBin = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BankAccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BankAccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SignedFileKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SignedFileContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SignedFileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotContracts_PartnerApplications_PartnerApplicationId",
                        column: x => x.PartnerApplicationId,
                        principalTable: "PartnerApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotContractVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LotContractId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    TermMonths = table.Column<int>(type: "integer", nullable: false),
                    FileKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RevisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotContractVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotContractVersions_LotContracts_LotContractId",
                        column: x => x.LotContractId,
                        principalTable: "LotContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_CreatedOwnerProfileId",
                table: "PartnerApplications",
                column: "CreatedOwnerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_LotContracts_ContractNumber",
                table: "LotContracts",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotContracts_OwnerProfileId",
                table: "LotContracts",
                column: "OwnerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_LotContracts_PartnerApplicationId",
                table: "LotContracts",
                column: "PartnerApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LotContracts_SingleOpenPerLot",
                table: "LotContracts",
                column: "ParkingLotId",
                unique: true,
                filter: "\"Status\" <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_LotContracts_Status",
                table: "LotContracts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LotContractVersions_LotContractId_Version",
                table: "LotContractVersions",
                columns: new[] { "LotContractId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotContractVersions_SingleCurrent",
                table: "LotContractVersions",
                column: "LotContractId",
                unique: true,
                filter: "\"Status\" = 'Current'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotContractVersions");

            migrationBuilder.DropTable(
                name: "LotContracts");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_CreatedOwnerProfileId",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "PayoutBankAccountName",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "PayoutBankBin",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "PayoutBankName",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "PayoutBankAccountNumber",
                table: "PartnerApplications");

            migrationBuilder.AddColumn<string>(
                name: "ContractNumber",
                table: "PartnerApplications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerApplicationId = table.Column<int>(type: "integer", nullable: false),
                    BankAccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BankAccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BankBin = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    FileContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RevisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentToEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    TermMonths = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
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
                    ContractDraftId = table.Column<int>(type: "integer", nullable: false),
                    PartnerApplicationId = table.Column<int>(type: "integer", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    DraftVersion = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: false),
                    SignedFileContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SignedFileKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SignedFileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                name: "IX_PartnerApplications_ContractNumber",
                table: "PartnerApplications",
                column: "ContractNumber",
                unique: true,
                filter: "\"ContractNumber\" IS NOT NULL");

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
        }
    }
}
