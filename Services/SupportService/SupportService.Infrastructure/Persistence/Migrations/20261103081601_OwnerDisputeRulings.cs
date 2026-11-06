using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SupportService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OwnerDisputeRulings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstResponseAtUtc",
                table: "Complaints",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DisputeRulings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComplaintId = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NewSlotCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeRulings", x => x.Id);
                    table.CheckConstraint("CK_DisputeRulings_Data", "(\"Decision\" = 'ForceRefund' AND \"RefundAmount\" > 0 AND \"NewSlotCode\" IS NULL) OR (\"Decision\" = 'MoveToSlot' AND \"NewSlotCode\" IS NOT NULL AND \"RefundAmount\" IS NULL) OR (\"Decision\" = 'RejectComplaint' AND \"RefundAmount\" IS NULL AND \"NewSlotCode\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_DisputeRulings_Complaints_ComplaintId",
                        column: x => x.ComplaintId,
                        principalTable: "Complaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotFineRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: false),
                    WrongSlotFine = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotFineRates", x => x.Id);
                    table.CheckConstraint("CK_LotFineRates_Amount", "\"WrongSlotFine\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "ParkingViolations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ComplaintId = table.Column<int>(type: "integer", nullable: false),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: true),
                    PlateNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SlotCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FineAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IssuedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingViolations", x => x.Id);
                    table.CheckConstraint("CK_ParkingViolations_Fine", "\"FineAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_ParkingViolations_Complaints_ComplaintId",
                        column: x => x.ComplaintId,
                        principalTable: "Complaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisputeRulings_ComplaintId",
                table: "DisputeRulings",
                column: "ComplaintId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisputeRulings_ParkingLotId_Category_CreatedAtUtc",
                table: "DisputeRulings",
                columns: new[] { "ParkingLotId", "Category", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LotFineRates_ParkingLotId",
                table: "LotFineRates",
                column: "ParkingLotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingViolations_Code",
                table: "ParkingViolations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingViolations_ComplaintId",
                table: "ParkingViolations",
                column: "ComplaintId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingViolations_ParkingLotId_CreatedAtUtc",
                table: "ParkingViolations",
                columns: new[] { "ParkingLotId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingViolations_ParkingLotId_PlateNumber",
                table: "ParkingViolations",
                columns: new[] { "ParkingLotId", "PlateNumber" });

            // US-090 AC4: phán quyết / override không sửa được – chặn UPDATE và DELETE ngay ở DB (kể cả câu SQL tay).
            migrationBuilder.Sql("""
                CREATE FUNCTION "DisputeRulings_AppendOnly"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'DisputeRulings chỉ được thêm, không được sửa hoặc xóa (US-090 AC4).' USING ERRCODE = 'P0001';
                END;
                $$;
                CREATE TRIGGER "TR_DisputeRulings_AppendOnly" BEFORE UPDATE OR DELETE ON "DisputeRulings"
                    FOR EACH ROW EXECUTE FUNCTION "DisputeRulings_AppendOnly"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_DisputeRulings_AppendOnly" ON "DisputeRulings";
                DROP FUNCTION IF EXISTS "DisputeRulings_AppendOnly"();
                """);

            migrationBuilder.DropTable(
                name: "DisputeRulings");

            migrationBuilder.DropTable(
                name: "LotFineRates");

            migrationBuilder.DropTable(
                name: "ParkingViolations");

            migrationBuilder.DropColumn(
                name: "FirstResponseAtUtc",
                table: "Complaints");
        }
    }
}
