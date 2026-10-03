using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingModifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    ModificationType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OldStartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OldEndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewStartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewEndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OldVehicleId = table.Column<int>(type: "int", nullable: true),
                    NewVehicleId = table.Column<int>(type: "int", nullable: true),
                    OldSlotId = table.Column<int>(type: "int", nullable: true),
                    NewSlotId = table.Column<int>(type: "int", nullable: true),
                    PriceDifference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingModifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingModifications_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyPasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    PlateNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    ParkingLotId = table.Column<int>(type: "int", nullable: false),
                    OwnerProfileId = table.Column<int>(type: "int", nullable: false),
                    SlotId = table.Column<int>(type: "int", nullable: true),
                    SlotCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AutoRenew = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyPasses", x => x.Id);
                    table.CheckConstraint("CK_MonthlyPasses_Period", "[ValidTo] >= [ValidFrom]");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingModifications_BookingId_CreatedAtUtc",
                table: "BookingModifications",
                columns: new[] { "BookingId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyPasses_Code",
                table: "MonthlyPasses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyPasses_ParkingLotId_PlateNumber_Status",
                table: "MonthlyPasses",
                columns: new[] { "ParkingLotId", "PlateNumber", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyPasses_SlotId",
                table: "MonthlyPasses",
                column: "SlotId",
                unique: true,
                filter: "[SlotId] IS NOT NULL AND [Status] = N'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyPasses_UserId",
                table: "MonthlyPasses",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingModifications");

            migrationBuilder.DropTable(
                name: "MonthlyPasses");
        }
    }
}
