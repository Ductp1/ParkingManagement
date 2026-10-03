using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: false),
                    SharedWithUserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleShares", x => x.Id);
                    table.CheckConstraint("CK_VehicleShares_NotSelf", "[OwnerUserId] <> [SharedWithUserId]");
                    table.ForeignKey(
                        name: "FK_VehicleShares_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleShares_SharedWithUserId",
                table: "VehicleShares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleShares_VehicleId_SharedWithUserId",
                table: "VehicleShares",
                columns: new[] { "VehicleId", "SharedWithUserId" },
                unique: true,
                filter: "[Status] <> N'Revoked'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleShares");
        }
    }
}
