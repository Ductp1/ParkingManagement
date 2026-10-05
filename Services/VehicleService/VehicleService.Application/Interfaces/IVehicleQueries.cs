using VehicleService.Application.DTOs;
using VehicleService.Domain.Entities;

namespace VehicleService.Application.Interfaces;

/// <summary>
/// Port kết nối xuống Database (được tầng Infrastructure triển khai).
/// </summary>
public interface IVehicleQueries
{
    Task<IReadOnlyList<VehicleDto>> ListByUserAsync(int userId, CancellationToken cancellationToken);
    Task<VehicleDto?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken);
    Task<int> CountByUserAsync(int userId, CancellationToken cancellationToken);
    Task<bool> ExistsPlateInGarageAsync(int userId, string normalizedPlate, CancellationToken cancellationToken);
    Task<VehicleDto> CreateAsync(Vehicle vehicle, CancellationToken cancellationToken);
    Task ClearDefaultForUserAsync(int userId, CancellationToken cancellationToken);
}
