using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingInboxReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxReceipts",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxReceipts", x => x.EventId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxReceipts");
        }
    }
}
