using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodicSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettlementLines_RefundId",
                table: "SettlementLines");

            migrationBuilder.AddColumn<string>(
                name: "BlockedReasons",
                table: "Settlements",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CarryOverAmount",
                table: "Settlements",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CarryOverFromSettlementId",
                table: "Settlements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Settlements",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "GeneratedAtUtc",
                table: "Settlements",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "GeneratedByUserId",
                table: "Settlements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HoldsCapturedAtUtc",
                table: "Settlements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                table: "Settlements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OwnerCollectedAmount",
                table: "Settlements",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PaidByUserId",
                table: "Settlements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "Settlements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAtUtc",
                table: "Settlements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RejectedByUserId",
                table: "Settlements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Settlements",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "SettlementPeriodId",
                table: "Settlements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAtUtc",
                table: "Settlements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubmittedByUserId",
                table: "Settlements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockedReason",
                table: "SettlementLines",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOwnerCollected",
                table: "SettlementLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAtUtc",
                table: "SettlementLines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "FinancialAdjustments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "FinancialAdjustments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "FinancialAdjustments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAtUtc",
                table: "FinancialAdjustments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RejectedByUserId",
                table: "FinancialAdjustments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReversesAdjustmentId",
                table: "FinancialAdjustments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "FinancialAdjustments",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "FinancialAdjustments",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "FinancialAdjustments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "FinancialAdjustments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SettlementPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Cycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CycleEffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementPeriods", x => x.Id);
                    table.CheckConstraint("CK_SettlementPeriods_Period", "\"PeriodEnd\" >= \"PeriodStart\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_CarryOverFromSettlementId",
                table: "Settlements",
                column: "CarryOverFromSettlementId",
                unique: true,
                filter: "\"CarryOverFromSettlementId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_Code",
                table: "Settlements",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_SettlementPeriodId",
                table: "Settlements",
                column: "SettlementPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_Status_PeriodStart",
                table: "Settlements",
                columns: new[] { "Status", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "UX_SettlementLines_Hold",
                table: "SettlementLines",
                column: "ReferenceCode",
                unique: true,
                filter: "\"LineType\" = 'DisputeHold' AND \"Amount\" < 0");

            migrationBuilder.CreateIndex(
                name: "UX_SettlementLines_HoldRelease",
                table: "SettlementLines",
                column: "ReferenceCode",
                unique: true,
                filter: "\"LineType\" = 'DisputeHold' AND \"Amount\" > 0");

            migrationBuilder.CreateIndex(
                name: "UX_SettlementLines_Payment",
                table: "SettlementLines",
                column: "PaymentId",
                unique: true,
                filter: "\"PaymentId\" IS NOT NULL AND \"LineType\" IN ('Booking', 'WalkIn')");

            migrationBuilder.CreateIndex(
                name: "UX_SettlementLines_Refund",
                table: "SettlementLines",
                column: "RefundId",
                unique: true,
                filter: "\"RefundId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_OwnerProfileId_Status_SettlementId",
                table: "FinancialAdjustments",
                columns: new[] { "OwnerProfileId", "Status", "SettlementId" });

            migrationBuilder.CreateIndex(
                name: "UX_FinancialAdjustments_Voucher",
                table: "FinancialAdjustments",
                columns: new[] { "SourceType", "SourceReference" },
                unique: true,
                filter: "\"SourceType\" = 'CompensationVoucher'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialAdjustments_Amount",
                table: "FinancialAdjustments",
                sql: "\"Amount\" <> 0");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementPeriods_PeriodStart",
                table: "SettlementPeriods",
                column: "PeriodStart",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Settlements_SettlementPeriods_SettlementPeriodId",
                table: "Settlements",
                column: "SettlementPeriodId",
                principalTable: "SettlementPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Settlements_SettlementPeriods_SettlementPeriodId",
                table: "Settlements");

            migrationBuilder.DropTable(
                name: "SettlementPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_CarryOverFromSettlementId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_Code",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_SettlementPeriodId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_Status_PeriodStart",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "UX_SettlementLines_Hold",
                table: "SettlementLines");

            migrationBuilder.DropIndex(
                name: "UX_SettlementLines_HoldRelease",
                table: "SettlementLines");

            migrationBuilder.DropIndex(
                name: "UX_SettlementLines_Payment",
                table: "SettlementLines");

            migrationBuilder.DropIndex(
                name: "UX_SettlementLines_Refund",
                table: "SettlementLines");

            migrationBuilder.DropIndex(
                name: "IX_FinancialAdjustments_OwnerProfileId_Status_SettlementId",
                table: "FinancialAdjustments");

            migrationBuilder.DropIndex(
                name: "UX_FinancialAdjustments_Voucher",
                table: "FinancialAdjustments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialAdjustments_Amount",
                table: "FinancialAdjustments");

            migrationBuilder.DropColumn(
                name: "BlockedReasons",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "CarryOverAmount",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "CarryOverFromSettlementId",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "GeneratedAtUtc",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "GeneratedByUserId",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "HoldsCapturedAtUtc",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "IsBlocked",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "OwnerCollectedAmount",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "PaidByUserId",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "RejectedAtUtc",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "SettlementPeriodId",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "SubmittedAtUtc",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "BlockedReason",
                table: "SettlementLines");

            migrationBuilder.DropColumn(
                name: "IsOwnerCollected",
                table: "SettlementLines");

            migrationBuilder.DropColumn(
                name: "OccurredAtUtc",
                table: "SettlementLines");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "FinancialAdjustments");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "FinancialAdjustments");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "FinancialAdjustments");

            migrationBuilder.DropColumn(
                name: "RejectedAtUtc",
                table: "FinancialAdjustments");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "FinancialAdjustments");

            migrationBuilder.DropColumn(
                name: "ReversesAdjustmentId",
                table: "FinancialAdjustments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "FinancialAdjustments");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "FinancialAdjustments");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "FinancialAdjustments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FinancialAdjustments");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementLines_RefundId",
                table: "SettlementLines",
                column: "RefundId");
        }
    }
}
