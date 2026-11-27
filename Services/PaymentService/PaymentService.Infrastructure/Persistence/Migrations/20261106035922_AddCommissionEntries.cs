using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommissionEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentId = table.Column<int>(type: "integer", nullable: false),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RateSource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContractVersion = table.Column<int>(type: "integer", nullable: true),
                    RefundedAmountCovered = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionEntries", x => x.Id);
                    table.CheckConstraint("CK_CommissionEntries_Rate", "\"Rate\" >= 0 AND \"Rate\" <= 1");
                    table.CheckConstraint("CK_CommissionEntries_Sign", "(\"EntryType\" = 'Accrual' AND \"Amount\" >= 0 AND \"BaseAmount\" >= 0 AND \"RefundedAmountCovered\" = 0) OR (\"EntryType\" = 'Reversal' AND \"Amount\" <= 0 AND \"BaseAmount\" < 0 AND \"RefundedAmountCovered\" > 0)");
                    table.ForeignKey(
                        name: "FK_CommissionEntries_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionEntries_OwnerProfileId_PaidAtUtc",
                table: "CommissionEntries",
                columns: new[] { "OwnerProfileId", "PaidAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionEntries_PaidAtUtc",
                table: "CommissionEntries",
                column: "PaidAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionEntries_PaymentId",
                table: "CommissionEntries",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionEntries_PaymentId_Accrual",
                table: "CommissionEntries",
                column: "PaymentId",
                unique: true,
                filter: "\"EntryType\" = 'Accrual'");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionEntries_PaymentId_RefundedAmountCovered_Reversal",
                table: "CommissionEntries",
                columns: new[] { "PaymentId", "RefundedAmountCovered" },
                unique: true,
                filter: "\"EntryType\" = 'Reversal'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommissionEntries");
        }
    }
}
