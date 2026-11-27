using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRateCardAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RateCardAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RateCardId = table.Column<int>(type: "integer", nullable: false),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OldValueJson = table.Column<string>(type: "jsonb", maxLength: 500, nullable: true),
                    NewValueJson = table.Column<string>(type: "jsonb", maxLength: 500, nullable: false),
                    MaxPriceIncreasePercentage = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    RequiresAdminApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateCardAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RateCardAuditLogs_RateCards_RateCardId",
                        column: x => x.RateCardId,
                        principalTable: "RateCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RateCardAuditLogs_ParkingLotId_CreatedAtUtc",
                table: "RateCardAuditLogs",
                columns: new[] { "ParkingLotId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RateCardAuditLogs_RateCardId",
                table: "RateCardAuditLogs",
                column: "RateCardId");

            migrationBuilder.CreateIndex(
                name: "IX_RateCardAuditLogs_RequiresAdminApproval_CreatedAtUtc",
                table: "RateCardAuditLogs",
                columns: new[] { "RequiresAdminApproval", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RateCardAuditLogs");
        }
    }
}
