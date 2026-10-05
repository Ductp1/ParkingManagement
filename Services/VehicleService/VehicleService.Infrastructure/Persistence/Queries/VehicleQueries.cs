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
}
