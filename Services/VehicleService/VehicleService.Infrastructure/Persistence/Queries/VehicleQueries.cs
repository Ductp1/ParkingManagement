using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VehicleService.Application.DTOs;
using VehicleService.Application.Interfaces;
using VehicleService.Domain.Entities;

namespace VehicleService.Infrastructure.Persistence.Queries;

public sealed class VehicleQueries(VehicleDbContext db) : IVehicleQueries
{
    private static readonly Expression<Func<Vehicle, VehicleDto>> ToDto = v => new VehicleDto(
        v.Id, v.UserId, v.PlateNumber, v.PlateDisplay, v.VehicleType, v.FuelType,
        v.Brand, v.Model, v.Color, v.HeightCm, v.IsDefault, v.LengthCm, v.WidthCm);

    public async Task<IReadOnlyList<VehicleDto>> ListByUserAsync(int userId, CancellationToken cancellationToken)
        => await db.Vehicles.AsNoTracking()
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.IsDefault).ThenBy(v => v.Id)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

    public Task<VehicleDto?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken)
        => db.Vehicles.AsNoTracking()
            .Where(v => v.PlateNumber == normalizedPlate)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountByUserAsync(int userId, CancellationToken cancellationToken)
        => db.Vehicles.CountAsync(v => v.UserId == userId, cancellationToken);

    public Task<bool> ExistsPlateInGarageAsync(int userId, string normalizedPlate, CancellationToken cancellationToken)
        => db.Vehicles.AnyAsync(v => v.UserId == userId && v.PlateNumber == normalizedPlate, cancellationToken);

    public async Task ClearDefaultForUserAsync(int userId, CancellationToken cancellationToken)
    {
        var defaultVehicles = await db.Vehicles
            .Where(v => v.UserId == userId && v.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var v in defaultVehicles)
        {
            v.IsDefault = false;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<VehicleDto> CreateAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(cancellationToken);

        return new VehicleDto(
            vehicle.Id,
            vehicle.UserId,
            vehicle.PlateNumber,
            vehicle.PlateDisplay,
            vehicle.VehicleType,
            vehicle.FuelType,
            vehicle.Brand,
            vehicle.Model,
            vehicle.Color,
            vehicle.HeightCm,
            vehicle.IsDefault,
            vehicle.LengthCm,
            vehicle.WidthCm
        );
    }

    public async Task<VehicleDto?> SetDefaultAsync(int vehicleId, int userId, CancellationToken cancellationToken)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && v.UserId == userId, cancellationToken);
        if (vehicle is null)
            return null;

        var currentDefaults = await db.Vehicles
            .Where(v => v.UserId == userId && v.IsDefault && v.Id != vehicleId)
            .ToListAsync(cancellationToken);

        foreach (var v in currentDefaults)
        {
            v.IsDefault = false;
        }

        vehicle.IsDefault = true;
        await db.SaveChangesAsync(cancellationToken);

        return new VehicleDto(
            vehicle.Id,
            vehicle.UserId,
            vehicle.PlateNumber,
            vehicle.PlateDisplay,
            vehicle.VehicleType,
            vehicle.FuelType,
            vehicle.Brand,
            vehicle.Model,
            vehicle.Color,
            vehicle.HeightCm,
            vehicle.IsDefault,
            vehicle.LengthCm,
            vehicle.WidthCm
        );
    }

    public async Task<VehicleDto?> UpdateAsync(int vehicleId, UpdateVehicleRequestDto request, CancellationToken cancellationToken)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && v.UserId == request.UserId, cancellationToken);
        if (vehicle is null)
            return null;

        vehicle.VehicleType = request.VehicleType;
        vehicle.FuelType = request.FuelType;
        vehicle.Brand = request.Brand?.Trim();
        vehicle.Model = request.Model?.Trim();
        vehicle.Color = request.Color?.Trim();

        if (request.HeightCm.HasValue)
        {
            vehicle.HeightCm = request.HeightCm.Value;
        }

        vehicle.LengthCm = request.LengthCm;
        vehicle.WidthCm = request.WidthCm;

        await db.SaveChangesAsync(cancellationToken);

        return new VehicleDto(
            vehicle.Id,
            vehicle.UserId,
            vehicle.PlateNumber,
            vehicle.PlateDisplay,
            vehicle.VehicleType,
            vehicle.FuelType,
            vehicle.Brand,
            vehicle.Model,
            vehicle.Color,
            vehicle.HeightCm,
            vehicle.IsDefault,
            vehicle.LengthCm,
            vehicle.WidthCm
        );
    }

    public Task<bool> ExistsByIdAndUserAsync(int vehicleId, int userId, CancellationToken cancellationToken)
        => db.Vehicles.AnyAsync(v => v.Id == vehicleId && v.UserId == userId, cancellationToken);

    public async Task<bool> SoftDeleteAsync(int vehicleId, int userId, CancellationToken cancellationToken)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && v.UserId == userId, cancellationToken);
        if (vehicle is null)
            return false;

        var wasDefault = vehicle.IsDefault;
        vehicle.IsDeleted = true;
        vehicle.DeletedAtUtc = DateTime.UtcNow;
        vehicle.IsDefault = false;

        if (wasDefault)
        {
            var nextDefaultVehicle = await db.Vehicles
                .Where(v => v.UserId == userId && v.Id != vehicleId)
                .OrderBy(v => v.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextDefaultVehicle is not null)
            {
                nextDefaultVehicle.IsDefault = true;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
