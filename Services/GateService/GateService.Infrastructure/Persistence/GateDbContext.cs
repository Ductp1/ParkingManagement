using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.ServiceDefaults.Persistence;

namespace GateService.Infrastructure.Persistence;

/// <summary>Database riêng của GateService: pm_gate. Lượt xe vào/ra, nhật ký sự kiện cổng (OCR, mở barie), ca trực.</summary>
public sealed class GateDbContext(DbContextOptions<GateDbContext> options) : ServiceDbContext(options)
{
    public DbSet<ParkingSession> ParkingSessions => Set<ParkingSession>();
    public DbSet<GateEvent> GateEvents => Set<GateEvent>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<GateDevice> GateDevices => Set<GateDevice>();
    // US-066: cấu hình sức chứa, bộ đếm runtime (T/V) và audit log cấu hình.
    public DbSet<LotCapacityConfig> LotCapacityConfigs => Set<LotCapacityConfig>();
    public DbSet<LotAdmissionCounter> LotAdmissionCounters => Set<LotAdmissionCounter>();
    public DbSet<LotCapacityAuditLog> LotCapacityAuditLogs => Set<LotCapacityAuditLog>();
    // US-052: hàng chờ sự kiện PMS không khớp booking (UC-51 E1).
    public DbSet<PmsUnmatchedEvent> PmsUnmatchedEvents => Set<PmsUnmatchedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // US-052: bảng InboxReceipts chống xử lý trùng sự kiện PMS (idempotency theo EventId).
        modelBuilder.ConfigureInbox();
    }
}
