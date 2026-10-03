using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GateService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 500, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParkingSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ParkingLotId = table.Column<int>(type: "int", nullable: false),
                    OwnerProfileId = table.Column<int>(type: "int", nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    BookingCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    VehicleId = table.Column<int>(type: "int", nullable: true),
                    SlotId = table.Column<int>(type: "int", nullable: true),
                    SlotCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PlateNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    VehicleType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsWalkIn = table.Column<bool>(type: "bit", nullable: false),
                    EntryAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckInMethod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EntryImagePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EntryOcrRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntryOcrConfidence = table.Column<double>(type: "float", nullable: true),
                    CheckedInByStaffId = table.Column<int>(type: "int", nullable: true),
                    ExitAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckOutMethod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ExitImagePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ExitOcrRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExitOcrConfidence = table.Column<double>(type: "float", nullable: true),
                    CheckedOutByStaffId = table.Column<int>(type: "int", nullable: true),
                    Fee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OverstayFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingSessions", x => x.Id);
                    table.CheckConstraint("CK_ParkingSessions_Exit", "[ExitAtUtc] IS NULL OR [ExitAtUtc] >= [EntryAtUtc]");
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParkingLotId = table.Column<int>(type: "int", nullable: false),
                    OwnerProfileId = table.Column<int>(type: "int", nullable: false),
                    StaffUserId = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CheckInCount = table.Column<int>(type: "int", nullable: false),
                    CheckOutCount = table.Column<int>(type: "int", nullable: false),
                    NoShowCancelCount = table.Column<int>(type: "int", nullable: false),
                    ManualExceptionCount = table.Column<int>(type: "int", nullable: false),
                    CashCollected = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HandoverNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GateEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParkingLotId = table.Column<int>(type: "int", nullable: false),
                    ParkingSessionId = table.Column<int>(type: "int", nullable: true),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PlateNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OcrRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OcrConfidence = table.Column<double>(type: "float", nullable: true),
                    PerformedByUserId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GateEvents_ParkingSessions_ParkingSessionId",
                        column: x => x.ParkingSessionId,
                        principalTable: "ParkingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GateEvents_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_EventType_CreatedAtUtc",
                table: "GateEvents",
                columns: new[] { "EventType", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_ParkingLotId_CreatedAtUtc",
                table: "GateEvents",
                columns: new[] { "ParkingLotId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_ParkingSessionId",
                table: "GateEvents",
                column: "ParkingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_ShiftId",
                table: "GateEvents",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAtUtc", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_BookingId",
                table: "ParkingSessions",
                column: "BookingId",
                unique: true,
                filter: "[BookingId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_Code",
                table: "ParkingSessions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_OwnerProfileId_ParkingLotId_EntryAtUtc",
                table: "ParkingSessions",
                columns: new[] { "OwnerProfileId", "ParkingLotId", "EntryAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_ParkingLotId_PlateNumber",
                table: "ParkingSessions",
                columns: new[] { "ParkingLotId", "PlateNumber" },
                unique: true,
                filter: "[Status] = N'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_ParkingLotId_StartedAtUtc",
                table: "Shifts",
                columns: new[] { "ParkingLotId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_StaffUserId",
                table: "Shifts",
                column: "StaffUserId",
                unique: true,
                filter: "[Status] = N'Open'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GateEvents");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "ParkingSessions");

            migrationBuilder.DropTable(
                name: "Shifts");
        }
    }
}
