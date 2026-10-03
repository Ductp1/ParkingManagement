using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VehicleService.Application.Features.Vehicles;
using VehicleService.Domain.Entities;

namespace VehicleService.Infrastructure.Persistence.Queries;

public sealed class VehicleQueries(VehicleDbContext db) : IVehicleQueries
{
    private static readonly Expression<Func<Vehicle, VehicleDto>> ToDto = v => new VehicleDto(
        v.Id, v.UserId, v.PlateNumber, v.PlateDisplay, v.VehicleType.ToString(), v.FuelType.ToString(),
        v.Brand, v.Model, v.Color, v.HeightCm, v.IsDefault);

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
}
