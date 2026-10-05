using VehicleService.Application.DTOs;

namespace VehicleService.Application.Interfaces;

/// <summary>
/// Interface Service xử lý nghiệp vụ chính của xe.
/// </summary>
public interface IVehicleService
{
    Task<IReadOnlyList<VehicleDto>> GetVehiclesByUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<VehicleDto> FindVehicleByPlateAsync(string plate, CancellationToken cancellationToken = default);
    Task<VehicleDto> CreateVehicleAsync(CreateVehicleRequestDto request, CancellationToken cancellationToken = default);
    Task<VehicleDto> SetDefaultVehicleAsync(int vehicleId, int userId, CancellationToken cancellationToken = default);
}
