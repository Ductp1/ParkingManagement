using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "ParkingLots",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "TP.HCM");

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "ParkingLots",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClosureSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParkingLotId = table.Column<int>(type: "int", nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsEmergency = table.Column<bool>(type: "bit", nullable: false),
                    AffectedBookingsNotified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosureSchedules", x => x.Id);
                    table.CheckConstraint("CK_ClosureSchedules_Period", "[EndsAtUtc] IS NULL OR [EndsAtUtc] > [StartsAtUtc]");
                    table.ForeignKey(
                        name: "FK_ClosureSchedules_ParkingLots_ParkingLotId",
                        column: x => x.ParkingLotId,
                        principalTable: "ParkingLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalParkingLots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    ReferencePricePerHour = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OpeningHoursText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImportedByUserId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalParkingLots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LotAmenities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParkingLotId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotAmenities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotAmenities_ParkingLots_ParkingLotId",
                        column: x => x.ParkingLotId,
                        principalTable: "ParkingLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotIntegrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParkingLotId = table.Column<int>(type: "int", nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApiKeyPrefix = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    ApiKeyHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WebhookUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    WebhookSecretHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LastSyncAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotIntegrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotIntegrations_ParkingLots_ParkingLotId",
                        column: x => x.ParkingLotId,
                        principalTable: "ParkingLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotOperatingHours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParkingLotId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OpenTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    CloseTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotOperatingHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotOperatingHours_ParkingLots_ParkingLotId",
                        column: x => x.ParkingLotId,
                        principalTable: "ParkingLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParkingLotId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsCover = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotPhotos_ParkingLots_ParkingLotId",
                        column: x => x.ParkingLotId,
                        principalTable: "ParkingLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SlotStateLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SlotId = table.Column<int>(type: "int", nullable: false),
                    FromState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ToState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    ParkingSessionId = table.Column<int>(type: "int", nullable: true),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlotStateLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlotStateLogs_Slots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "Slots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingLots_City_District",
                table: "ParkingLots",
                columns: new[] { "City", "District" });

            migrationBuilder.CreateIndex(
                name: "IX_ClosureSchedules_ParkingLotId_StartsAtUtc_EndsAtUtc",
                table: "ClosureSchedules",
                columns: new[] { "ParkingLotId", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClosureSchedules_TargetType_TargetId",
                table: "ClosureSchedules",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalParkingLots_Latitude_Longitude",
                table: "ExternalParkingLots",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_LotAmenities_Code",
                table: "LotAmenities",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_LotAmenities_ParkingLotId_Code",
                table: "LotAmenities",
                columns: new[] { "ParkingLotId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotIntegrations_ApiKeyPrefix",
                table: "LotIntegrations",
                column: "ApiKeyPrefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotIntegrations_ParkingLotId",
                table: "LotIntegrations",
                column: "ParkingLotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotOperatingHours_ParkingLotId_DayOfWeek",
                table: "LotOperatingHours",
                columns: new[] { "ParkingLotId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotPhotos_ParkingLotId_SortOrder",
                table: "LotPhotos",
                columns: new[] { "ParkingLotId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_LotPhotos_OneCover",
                table: "LotPhotos",
                column: "ParkingLotId",
                unique: true,
                filter: "[IsCover] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SlotStateLogs_SlotId_CreatedAtUtc",
                table: "SlotStateLogs",
                columns: new[] { "SlotId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SlotStateLogs_Source_CreatedAtUtc",
                table: "SlotStateLogs",
                columns: new[] { "Source", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClosureSchedules");

            migrationBuilder.DropTable(
                name: "ExternalParkingLots");

            migrationBuilder.DropTable(
                name: "LotAmenities");

            migrationBuilder.DropTable(
                name: "LotIntegrations");

            migrationBuilder.DropTable(
                name: "LotOperatingHours");

            migrationBuilder.DropTable(
                name: "LotPhotos");

            migrationBuilder.DropTable(
                name: "SlotStateLogs");

            migrationBuilder.DropIndex(
                name: "IX_ParkingLots_City_District",
                table: "ParkingLots");

            migrationBuilder.DropColumn(
                name: "City",
                table: "ParkingLots");

            migrationBuilder.DropColumn(
                name: "District",
                table: "ParkingLots");
        }
    }
}
