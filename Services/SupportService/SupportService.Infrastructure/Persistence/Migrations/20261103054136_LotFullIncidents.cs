using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SupportService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LotFullIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LotFullIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    BookingCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: true),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LotAvailableSlotsAtReport = table.Column<int>(type: "integer", nullable: true),
                    AlternativesJson = table.Column<string>(type: "text", maxLength: 500, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewParkingLotId = table.Column<int>(type: "integer", nullable: true),
                    NewParkingLotName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RefundPercent = table.Column<int>(type: "integer", nullable: true),
                    VoucherAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    VoucherValidDays = table.Column<int>(type: "integer", nullable: true),
                    IsSlaViolation = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotFullIncidents", x => x.Id);
                    table.CheckConstraint("CK_LotFullIncidents_PaidAmount", "\"PaidAmount\" >= 0");
                    table.CheckConstraint("CK_LotFullIncidents_RefundRequested", "\"Status\" <> 'RefundRequested' OR (\"RefundPercent\" = 100 AND \"VoucherAmount\" > 0 AND \"IsSlaViolation\" AND \"DecidedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_LotFullIncidents_TransferAccepted", "\"Status\" <> 'TransferAccepted' OR (\"NewParkingLotId\" IS NOT NULL AND \"DecidedAtUtc\" IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotFullIncidents_BookingId",
                table: "LotFullIncidents",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotFullIncidents_Code",
                table: "LotFullIncidents",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotFullIncidents_ParkingLotId_IsSlaViolation",
                table: "LotFullIncidents",
                columns: new[] { "ParkingLotId", "IsSlaViolation" });

            migrationBuilder.CreateIndex(
                name: "IX_LotFullIncidents_Status_CreatedAtUtc",
                table: "LotFullIncidents",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotFullIncidents");
        }
    }
}
