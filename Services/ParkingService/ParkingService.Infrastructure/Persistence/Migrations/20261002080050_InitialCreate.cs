using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingService.Infrastructure.Persistence.Migrations
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
                name: "ParkingLots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerProfileId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HotlinePhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    TotalSlots = table.Column<int>(type: "int", nullable: false),
                    AvailableSlots = table.Column<int>(type: "int", nullable: false),
                    MaxHeightCm = table.Column<int>(type: "int", nullable: false),
                    OpenTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    CloseTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IntegrationTier = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RatingAverage = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    RatingCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingLots", x => x.Id);
                    table.CheckConstraint("CK_ParkingLots_Latitude", "[Latitude] BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_ParkingLots_Longitude", "[Longitude] BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_ParkingLots_Slots", "[AvailableSlots] >= 0 AND [AvailableSlots] <= [TotalSlots]");
                });

            migrationBuilder.CreateTable(
                name: "KybApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParkingLotId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BusinessLicenseUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SitePhotoUrlsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 500, nullable: true),
                    PhotoLatitude = table.Column<double>(type: "float", nullable: true),
                    PhotoLongitude = table.Column<double>(type: "float", nullable: true),
                    FireSafetyCertificateUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    FieldSurveyRequired = table.Column<bool>(type: "bit", nullable: false),
                    FieldSurveyAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FieldSurveyNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KybApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KybApplications_ParkingLots_ParkingLotId",
                        column: x => x.ParkingLotId,
                        principalTable: "ParkingLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotCapacityConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParkingLotId = table.Column<int>(type: "int", nullable: false),
                    OnlineQuota = table.Column<int>(type: "int", nullable: false),
                    WalkInBufferPercent = table.Column<int>(type: "int", nullable: false),
                    OnlineLockThreshold = table.Column<int>(type: "int", nullable: false),
                    InstantBookingPaused = table.Column<bool>(type: "bit", nullable: false),
                    IsEmergencyStopped = table.Column<bool>(type: "bit", nullable: false),
                    EmergencyReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastHeartbeatAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsecutiveHeartbeatFailures = table.Column<int>(type: "int", nullable: false),
                    IsStale = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotCapacityConfigs", x => x.Id);
                    table.CheckConstraint("CK_LotCapacity_Buffer", "[WalkInBufferPercent] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_LotCapacityConfigs_ParkingLots_ParkingLotId",
                        column: x => x.ParkingLotId,
                        principalTable: "ParkingLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Zones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParkingLotId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsOutdoor = table.Column<bool>(type: "bit", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    ClosedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Zones_ParkingLots_ParkingLotId",
                        column: x => x.ParkingLotId,
                        principalTable: "ParkingLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Floors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ZoneId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    MaxHeightCm = table.Column<int>(type: "int", nullable: true),
                    MaxWeightKg = table.Column<int>(type: "int", nullable: true),
                    GridColumns = table.Column<int>(type: "int", nullable: false),
                    GridRows = table.Column<int>(type: "int", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Floors", x => x.Id);
                    table.CheckConstraint("CK_Floors_Grid", "[GridColumns] > 0 AND [GridRows] > 0");
                    table.ForeignKey(
                        name: "FK_Floors_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LayoutVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FloorId = table.Column<int>(type: "int", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    LayoutJson = table.Column<string>(type: "nvarchar(max)", maxLength: 500, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    ChangeNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LayoutVersions_Floors_FloorId",
                        column: x => x.FloorId,
                        principalTable: "Floors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Slots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FloorId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SlotType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MaxVehicleType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GridX = table.Column<int>(type: "int", nullable: false),
                    GridY = table.Column<int>(type: "int", nullable: false),
                    WidthCells = table.Column<int>(type: "int", nullable: false),
                    HeightCells = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StateChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DedicatedVehicleId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Slots_Floors_FloorId",
                        column: x => x.FloorId,
                        principalTable: "Floors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Floors_ZoneId_Name",
                table: "Floors",
                columns: new[] { "ZoneId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KybApplications_ParkingLotId_Status",
                table: "KybApplications",
                columns: new[] { "ParkingLotId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_KybApplications_Status",
                table: "KybApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutVersions_FloorId_VersionNo",
                table: "LayoutVersions",
                columns: new[] { "FloorId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotCapacityConfigs_ParkingLotId",
                table: "LotCapacityConfigs",
                column: "ParkingLotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAtUtc", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingLots_Latitude_Longitude",
                table: "ParkingLots",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingLots_OwnerProfileId",
                table: "ParkingLots",
                column: "OwnerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingLots_Status",
                table: "ParkingLots",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Slots_DedicatedVehicleId",
                table: "Slots",
                column: "DedicatedVehicleId",
                filter: "[DedicatedVehicleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Slots_FloorId_Code",
                table: "Slots",
                columns: new[] { "FloorId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Slots_FloorId_GridX_GridY",
                table: "Slots",
                columns: new[] { "FloorId", "GridX", "GridY" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Slots_FloorId_State",
                table: "Slots",
                columns: new[] { "FloorId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Zones_ParkingLotId_Code",
                table: "Zones",
                columns: new[] { "ParkingLotId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KybApplications");

            migrationBuilder.DropTable(
                name: "LayoutVersions");

            migrationBuilder.DropTable(
                name: "LotCapacityConfigs");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "Slots");

            migrationBuilder.DropTable(
                name: "Floors");

            migrationBuilder.DropTable(
                name: "Zones");

            migrationBuilder.DropTable(
                name: "ParkingLots");
        }
    }
}
