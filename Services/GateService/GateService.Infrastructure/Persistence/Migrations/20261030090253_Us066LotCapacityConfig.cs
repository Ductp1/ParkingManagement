using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GateService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Us066LotCapacityConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdmissionType",
                table: "ParkingSessions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "LotAdmissionCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false),
                    Transient = table.Column<int>(type: "integer", nullable: false),
                    Subscriber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotAdmissionCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LotCapacityAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OldCapacity = table.Column<int>(type: "integer", nullable: false),
                    OldTransientBuffer = table.Column<int>(type: "integer", nullable: false),
                    OldSubscriberQuota = table.Column<int>(type: "integer", nullable: false),
                    NewCapacity = table.Column<int>(type: "integer", nullable: false),
                    NewTransientBuffer = table.Column<int>(type: "integer", nullable: false),
                    NewSubscriberQuota = table.Column<int>(type: "integer", nullable: false),
                    OldVersion = table.Column<long>(type: "bigint", nullable: false),
                    NewVersion = table.Column<long>(type: "bigint", nullable: false),
                    WarningsJson = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotCapacityAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LotCapacityConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    TransientBuffer = table.Column<int>(type: "integer", nullable: false),
                    SubscriberQuota = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastChangedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotCapacityConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotAdmissionCounters_ParkingLotId",
                table: "LotAdmissionCounters",
                column: "ParkingLotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotCapacityAuditLogs_ParkingLotId_ChangedAtUtc",
                table: "LotCapacityAuditLogs",
                columns: new[] { "ParkingLotId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LotCapacityConfigs_ParkingLotId",
                table: "LotCapacityConfigs",
                column: "ParkingLotId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotAdmissionCounters");

            migrationBuilder.DropTable(
                name: "LotCapacityAuditLogs");

            migrationBuilder.DropTable(
                name: "LotCapacityConfigs");

            migrationBuilder.DropColumn(
                name: "AdmissionType",
                table: "ParkingSessions");
        }
    }
}
