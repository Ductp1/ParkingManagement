using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingService.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// US-068 (gộp integration): cột IsDataStale/LastHeartbeatAtUtc đã được tạo từ trước bởi
    /// Us067068069_LotOperations (thế hệ LotOperations US-067/068/069). Migration này giữ ID
    /// để khớp lịch sử đã áp dụng trên DB dev hiện có, nội dung rỗng (no-op) — nếu không sẽ
    /// lỗi 42701 "column already exists" khi migrate DB mới/CI.
    /// </summary>
    public partial class AddLotHeartbeatStale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: IsDataStale + LastHeartbeatAtUtc đã được Us067068069_LotOperations tạo.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: không drop — Us067068069_LotOperations.Down chịu trách nhiệm drop 2 cột này.
        }
    }
}
