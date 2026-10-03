using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    PlateNumber = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: false),
                    ParkingLotName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ZoneId = table.Column<int>(type: "integer", nullable: true),
                    SlotId = table.Column<int>(type: "integer", nullable: true),
                    SlotCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AllocationMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HoldExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedInAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedOutAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CancelledByUserId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequiresOwnerApproval = table.Column<bool>(type: "boolean", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PromotionId = table.Column<int>(type: "integer", nullable: true),
                    PromotionCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    QrToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.CheckConstraint("CK_Bookings_Amount", "[TotalAmount] >= 0 AND [DiscountAmount] >= 0 AND [PaidAmount] >= 0");
                    table.CheckConstraint("CK_Bookings_Time", "[EndAtUtc] > [StartAtUtc]");
                });

            migrationBuilder.CreateTable(
                name: "MonthlyPasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    PlateNumber = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: false),
                    SlotId = table.Column<int>(type: "integer", nullable: true),
                    SlotCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PaymentId = table.Column<int>(type: "integer", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyPasses", x => x.Id);
                    table.CheckConstraint("CK_MonthlyPasses_Period", "[ValidTo] >= [ValidFrom]");
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", maxLength: 500, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingModifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    ModificationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OldStartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OldEndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewStartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewEndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OldVehicleId = table.Column<int>(type: "integer", nullable: true),
                    NewVehicleId = table.Column<int>(type: "integer", nullable: true),
                    OldSlotId = table.Column<int>(type: "integer", nullable: true),
                    NewSlotId = table.Column<int>(type: "integer", nullable: true),
                    PriceDifference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentId = table.Column<int>(type: "integer", nullable: true),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "BookingStatusLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingStatusLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingStatusLogs_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    RateCardId = table.Column<int>(type: "integer", nullable: false),
                    RateCardJson = table.Column<string>(type: "text", maxLength: 500, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SurchargeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BillingGracePeriodMinutes = table.Column<int>(type: "integer", nullable: false),
                    OverstayMultiplier = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceSnapshots_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingModifications_BookingId_CreatedAtUtc",
                table: "BookingModifications",
                columns: new[] { "BookingId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Code",
                table: "Bookings",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_OwnerProfileId_ParkingLotId_StartAtUtc",
                table: "Bookings",
                columns: new[] { "OwnerProfileId", "ParkingLotId", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PlateNumber",
                table: "Bookings",
                column: "PlateNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SlotId_StartAtUtc_EndAtUtc",
                table: "Bookings",
                columns: new[] { "SlotId", "StartAtUtc", "EndAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status_HoldExpiresAtUtc",
                table: "Bookings",
                columns: new[] { "Status", "HoldExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UserId_Status",
                table: "Bookings",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusLogs_BookingId_CreatedAtUtc",
                table: "BookingStatusLogs",
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

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAtUtc", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceSnapshots_BookingId",
                table: "PriceSnapshots",
                column: "BookingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingModifications");

            migrationBuilder.DropTable(
                name: "BookingStatusLogs");

            migrationBuilder.DropTable(
                name: "MonthlyPasses");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "PriceSnapshots");

            migrationBuilder.DropTable(
                name: "Bookings");
        }
    }
}
