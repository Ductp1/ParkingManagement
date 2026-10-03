using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_PaymentId",
                table: "Invoices");

            migrationBuilder.AlterColumn<int>(
                name: "PaymentId",
                table: "Invoices",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "SettlementId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Invoices",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ParkingFee");

            migrationBuilder.CreateTable(
                name: "CompensationVouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    ComplaintId = table.Column<int>(type: "int", nullable: true),
                    ChargedToOwnerProfileId = table.Column<int>(type: "int", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RedeemedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RedeemedPaymentId = table.Column<int>(type: "int", nullable: true),
                    IssuedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensationVouchers", x => x.Id);
                    table.CheckConstraint("CK_CompensationVouchers_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_CompensationVouchers_Payments_RedeemedPaymentId",
                        column: x => x.RedeemedPaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentCallbackLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", maxLength: 500, nullable: false),
                    SignatureValid = table.Column<bool>(type: "bit", nullable: false),
                    IsDuplicate = table.Column<bool>(type: "bit", nullable: false),
                    ResultCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProcessingError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourceIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentCallbackLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentCallbackLogs_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromotionRedemptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromotionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsReverted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionRedemptions", x => x.Id);
                    table.CheckConstraint("CK_PromotionRedemptions_Amount", "[DiscountAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_PromotionRedemptions_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromotionRedemptions_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Holidays",
                columns: new[] { "Id", "City", "CreatedAtUtc", "Date", "Name", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2027, 1, 1), "Tết Dương lịch", null },
                    { 2, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2027, 2, 5), "Tết Nguyên đán (29 Tết)", null },
                    { 3, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2027, 2, 6), "Tết Nguyên đán (Mùng 1)", null },
                    { 4, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2027, 2, 7), "Tết Nguyên đán (Mùng 2)", null },
                    { 5, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2027, 2, 8), "Tết Nguyên đán (Mùng 3)", null },
                    { 6, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2027, 2, 9), "Tết Nguyên đán (Mùng 4)", null },
                    { 7, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2027, 4, 16), "Giỗ Tổ Hùng Vương", null },
                    { 8, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2027, 4, 30), "Ngày Giải phóng miền Nam", null },
                    { 9, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2027, 5, 1), "Quốc tế Lao động", null },
                    { 10, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2027, 9, 2), "Quốc khánh", null },
                    { 11, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2027, 9, 3), "Quốc khánh (nghỉ liền kề)", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PaymentId",
                table: "Invoices",
                column: "PaymentId",
                unique: true,
                filter: "[PaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SettlementId",
                table: "Invoices",
                column: "SettlementId",
                unique: true,
                filter: "[SettlementId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Source",
                table: "Invoices",
                sql: "([Type] = N'ParkingFee' AND [PaymentId] IS NOT NULL) OR ([Type] = N'PlatformCommission' AND [SettlementId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationVouchers_ChargedToOwnerProfileId",
                table: "CompensationVouchers",
                column: "ChargedToOwnerProfileId",
                filter: "[ChargedToOwnerProfileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationVouchers_Code",
                table: "CompensationVouchers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompensationVouchers_RedeemedPaymentId",
                table: "CompensationVouchers",
                column: "RedeemedPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationVouchers_UserId_Status_ExpiresAtUtc",
                table: "CompensationVouchers",
                columns: new[] { "UserId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_Date_City",
                table: "Holidays",
                columns: new[] { "Date", "City" },
                unique: true,
                filter: "[City] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCallbackLogs_PaymentId_CreatedAtUtc",
                table: "PaymentCallbackLogs",
                columns: new[] { "PaymentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCallbackLogs_Provider_ProviderTransactionId",
                table: "PaymentCallbackLogs",
                columns: new[] { "Provider", "ProviderTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRedemptions_BookingId",
                table: "PromotionRedemptions",
                column: "BookingId",
                unique: true,
                filter: "[IsReverted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRedemptions_PaymentId",
                table: "PromotionRedemptions",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRedemptions_PromotionId_UserId",
                table: "PromotionRedemptions",
                columns: new[] { "PromotionId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Settlements_SettlementId",
                table: "Invoices",
                column: "SettlementId",
                principalTable: "Settlements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Settlements_SettlementId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "CompensationVouchers");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "PaymentCallbackLogs");

            migrationBuilder.DropTable(
                name: "PromotionRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PaymentId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SettlementId",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Source",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SettlementId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Invoices");

            migrationBuilder.AlterColumn<int>(
                name: "PaymentId",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PaymentId",
                table: "Invoices",
                column: "PaymentId",
                unique: true);
        }
    }
}
