using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AdminService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceService = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OldValuesJson = table.Column<string>(type: "jsonb", maxLength: 500, nullable: true),
                    NewValuesJson = table.Column<string>(type: "jsonb", maxLength: 500, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFlags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", maxLength: 500, nullable: false),
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
                name: "Sanctions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerProfileId = table.Column<int>(type: "integer", nullable: false),
                    ParkingLotId = table.Column<int>(type: "integer", nullable: true),
                    Level = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EvidenceJson = table.Column<string>(type: "jsonb", maxLength: 500, nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PenaltyAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IssuedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sanctions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskFlags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubjectType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Severity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EvidenceJson = table.Column<string>(type: "jsonb", maxLength: 500, nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedToUserId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SanctionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskFlags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskFlags_Sanctions_SanctionId",
                        column: x => x.SanctionId,
                        principalTable: "Sanctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Id", "CreatedAtUtc", "Description", "IsEnabled", "Key", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Nhận diện biển số OCR tại cổng (có trong demo MVP)", true, "FEATURE_AI_LPR", null, null },
                    { 2, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sơ đồ 2.5D/3D – stretch", false, "FEATURE_3D_MAP", null, null },
                    { 3, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Tìm bãi bằng giọng nói – stretch", false, "FEATURE_AI_VOICE_SEARCH", null, null },
                    { 4, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Chatbot FAQ – stretch", false, "FEATURE_AI_CHATBOT", null, null },
                    { 5, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Bãi quảng cáo trên kết quả tìm kiếm – Phase 2", false, "FEATURE_SPONSORED_LISTING", null, null },
                    { 6, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Vé tháng – Phase 2", false, "FEATURE_MONTHLY_PASS", null, null },
                    { 7, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Thanh toán online qua VNPAY sandbox", true, "FEATURE_VNPAY", null, null },
                    { 8, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Thanh toán VietQR động tại cổng ra", true, "FEATURE_VIETQR_AT_GATE", null, null }
                });

            migrationBuilder.InsertData(
                table: "SystemConfigs",
                columns: new[] { "Id", "CreatedAtUtc", "DataType", "Description", "Key", "UpdatedAtUtc", "UpdatedByUserId", "Value" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Thời gian giữ chỗ chờ thanh toán", "BOOKING_HOLD_MINUTES", null, null, "15" },
                    { 2, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Ân hạn chờ tài xế đến trễ", "CHECKIN_GRACE_PERIOD_MINUTES", null, null, "15" },
                    { 3, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Ân hạn miễn phí tính cước mỗi lượt", "PARKING_BILLING_GRACE_PERIOD_MINUTES", null, null, "15" },
                    { 4, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Hủy trước ≥ 60 phút hoàn 100%, sau đó hoàn 0%", "CANCELLATION_WINDOW_MINUTES", null, null, "60" },
                    { 5, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Staff được hủy no-show sau mốc này", "NO_SHOW_CANCEL_AFTER_MINUTES", null, null, "30" },
                    { 6, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Bãi Mức 0 phải duyệt booking trong thời gian này", "MANUAL_LOT_APPROVAL_MINUTES", null, null, "10" },
                    { 7, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", "Hoa hồng mặc định trên giao dịch hoàn tất", "DEFAULT_COMMISSION_RATE", null, null, "0.10" },
                    { 8, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "string", "Chu kỳ quyết toán cho chủ bãi", "SETTLEMENT_CYCLE", null, null, "WEEKLY" },
                    { 9, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Hiệu lực mã OTP", "OTP_EXPIRY_SECONDS", null, null, "300" },
                    { 10, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Số lần nhập sai OTP trước khi khóa", "OTP_MAX_ATTEMPTS", null, null, "3" },
                    { 11, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Thời gian khóa sau khi sai OTP", "OTP_LOCK_MINUTES", null, null, "15" },
                    { 12, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Hiệu lực access token", "JWT_ACCESS_TOKEN_HOURS", null, null, "24" },
                    { 13, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Hiệu lực refresh token", "JWT_REFRESH_TOKEN_DAYS", null, null, "7" },
                    { 14, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Số xe tối đa trong Garage cá nhân", "MAX_VEHICLES_PER_USER", null, null, "10" },
                    { 15, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", "Bán kính tìm bãi mặc định", "SEARCH_RADIUS_KM", null, null, "5" },
                    { 16, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Biên an toàn chiều cao xe so với trần", "HEIGHT_CLEARANCE_MARGIN_CM", null, null, "10" },
                    { 17, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Chu kỳ kiểm tra kết nối bãi", "HEARTBEAT_INTERVAL_SECONDS", null, null, "60" },
                    { 18, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Số lần mất kết nối trước khi đánh dấu Stale", "HEARTBEAT_MAX_FAILURES", null, null, "3" },
                    { 19, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", "Ngưỡng tự mở barie theo kết quả OCR", "OCR_AUTO_OPEN_MIN_CONFIDENCE", null, null, "0.90" },
                    { 20, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Hạn chủ bãi phản hồi tranh chấp", "OWNER_DISPUTE_RESPONSE_HOURS", null, null, "48" },
                    { 21, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Hạn gửi khiếu nại sau sự việc", "COMPLAINT_WINDOW_DAYS", null, null, "7" },
                    { 22, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Hạn cập nhật sơ đồ sau thay đổi thực tế", "LAYOUT_UPDATE_DEADLINE_HOURS", null, null, "48" },
                    { 23, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", "Phí chiếm dụng trụ sạc sau 30 phút sạc đầy", "EV_IDLE_FEE_PER_15_MINUTES", null, null, "20000" },
                    { 24, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "int", "Giới hạn request/phút cho mỗi client", "API_RATE_LIMIT_PER_MINUTE", null, null, "100" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityName_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OwnerProfileId_CreatedAtUtc",
                table: "AuditLogs",
                columns: new[] { "OwnerProfileId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_CreatedAtUtc",
                table: "AuditLogs",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_Key",
                table: "FeatureFlags",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAtUtc", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskFlags_SanctionId",
                table: "RiskFlags",
                column: "SanctionId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskFlags_Status_Severity_DueAtUtc",
                table: "RiskFlags",
                columns: new[] { "Status", "Severity", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskFlags_SubjectType_SubjectId",
                table: "RiskFlags",
                columns: new[] { "SubjectType", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sanctions_OwnerProfileId_Status",
                table: "Sanctions",
                columns: new[] { "OwnerProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sanctions_ParkingLotId",
                table: "Sanctions",
                column: "ParkingLotId",
                filter: "[ParkingLotId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigs_Key",
                table: "SystemConfigs",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "FeatureFlags");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "RiskFlags");

            migrationBuilder.DropTable(
                name: "SystemConfigs");

            migrationBuilder.DropTable(
                name: "Sanctions");
        }
    }
}
