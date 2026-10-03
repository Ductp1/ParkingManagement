using Microsoft.EntityFrameworkCore;
using ParkingManagement.ServiceDefaults.Persistence;
using VehicleService.Domain.Entities;

namespace VehicleService.Infrastructure.Persistence;

/// <summary>Database riêng của VehicleService: PM_VehicleDb. Garage xe của tài xế.</summary>
public sealed class VehicleDbContext(DbContextOptions<VehicleDbContext> options) : ServiceDbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleShare> VehicleShares => Set<VehicleShare>();
}
