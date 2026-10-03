using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using VehicleService.Domain.Entities;

namespace VehicleService.Infrastructure.Persistence.Seeding;

/// <summary>3 xe demo: 2 xe của driver1 (UserId 5), 1 xe điện của driver2 (UserId 6).</summary>
public sealed class VehicleDataSeeder(ILogger<VehicleDataSeeder> logger) : IDataSeeder<VehicleDbContext>
{
    public async Task SeedAsync(VehicleDbContext db, CancellationToken cancellationToken)
    {
        await SeedCoreAsync(db, cancellationToken);
        await SeedExtrasAsync(db, cancellationToken);
    }

    /// <summary>Dữ liệu demo ban đầu (migration InitialCreate).</summary>
    private async Task SeedCoreAsync(VehicleDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Vehicles.IgnoreQueryFilters().AnyAsync(cancellationToken)) return;

        Vehicle[] vehicles =
        [
            new() { UserId = DemoIds.Driver1User, PlateNumber = "51F12345", PlateDisplay = "51F-123.45", VehicleType = VehicleType.Sedan,
                    Brand = "Toyota", Model = "Vios", Color = "Trắng", HeightCm = 147, LengthCm = 442, WidthCm = 173, IsDefault = true },
            new() { UserId = DemoIds.Driver1User, PlateNumber = "30A67890", PlateDisplay = "30A-678.90", VehicleType = VehicleType.Suv,
                    Brand = "Mazda", Model = "CX-5", Color = "Đỏ", HeightCm = 168, LengthCm = 455, WidthCm = 184 },
            new() { UserId = DemoIds.Driver2User, PlateNumber = "51H91991", PlateDisplay = "51H-919.91", VehicleType = VehicleType.Sedan,
                    FuelType = FuelType.Electric, Brand = "VinFast", Model = "VF 6", Color = "Xanh", HeightCm = 159, IsDefault = true },
        ];
        foreach (var v in vehicles)
        {
            db.Vehicles.Add(v);
            await db.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation("Seed VehicleService xong: {Count} xe.", vehicles.Length);
    }

    /// <summary>Dữ liệu demo cho các bảng thêm ở migration AddDocumentCoverage.</summary>
    private static async Task SeedExtrasAsync(VehicleDbContext db, CancellationToken cancellationToken)
    {
        if (await db.VehicleShares.AnyAsync(cancellationToken)) return;

        // Phase 2 – driver1 cho driver2 dùng chung chiếc SUV 30A-678.90.
        db.VehicleShares.Add(new VehicleShare
        {
            VehicleId = DemoIds.Driver1Suv, OwnerUserId = DemoIds.Driver1User, SharedWithUserId = DemoIds.Driver2User,
            Status = VehicleShareStatus.Active, ExpiresAtUtc = DateTime.UtcNow.AddMonths(6)
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
