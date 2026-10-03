using Microsoft.EntityFrameworkCore;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence;

/// <summary>Database riêng của ParkingService: pm_parking. Bãi, cây Zone → Floor → Slot, layout, KYB, sức chứa.</summary>
public sealed class ParkingDbContext(DbContextOptions<ParkingDbContext> options) : ServiceDbContext(options)
{
    public DbSet<ParkingLot> ParkingLots => Set<ParkingLot>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Slot> Slots => Set<Slot>();
    public DbSet<LayoutVersion> LayoutVersions => Set<LayoutVersion>();
    public DbSet<KybApplication> KybApplications => Set<KybApplication>();
    public DbSet<LotCapacityConfig> LotCapacityConfigs => Set<LotCapacityConfig>();
    public DbSet<LotOperatingHour> LotOperatingHours => Set<LotOperatingHour>();
    public DbSet<ClosureSchedule> ClosureSchedules => Set<ClosureSchedule>();
    public DbSet<LotAmenity> LotAmenities => Set<LotAmenity>();
    public DbSet<LotPhoto> LotPhotos => Set<LotPhoto>();
    public DbSet<LotIntegration> LotIntegrations => Set<LotIntegration>();
    public DbSet<SlotStateLog> SlotStateLogs => Set<SlotStateLog>();
    public DbSet<ExternalParkingLot> ExternalParkingLots => Set<ExternalParkingLot>();
}
