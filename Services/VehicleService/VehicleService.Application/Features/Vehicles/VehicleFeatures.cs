using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;

namespace VehicleService.Application.Features.Vehicles;

// ===== DTO =====
public sealed record VehicleDto(int Id, int UserId, string PlateNumber, string PlateDisplay, string VehicleType, string FuelType,
    string? Brand, string? Model, string? Color, int HeightCm, bool IsDefault);

// ===== PORT =====
public interface IVehicleQueries
{
    Task<IReadOnlyList<VehicleDto>> ListByUserAsync(int userId, CancellationToken cancellationToken);
    Task<VehicleDto?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken);
}

// ===== USE CASE =====
public interface IGetVehiclesByUserUseCase
{
    Task<IReadOnlyList<VehicleDto>> ExecuteAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>UC-07: Garage của tài xế (xe mặc định đứng đầu).</summary>
public sealed class GetVehiclesByUserUseCase(IVehicleQueries queries) : IGetVehiclesByUserUseCase
{
    public Task<IReadOnlyList<VehicleDto>> ExecuteAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0) throw new ValidationException("userId phải là số nguyên dương.");
        return queries.ListByUserAsync(userId, cancellationToken);
    }
}

public interface IFindVehicleByPlateUseCase
{
    Task<VehicleDto> ExecuteAsync(string plate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tra xe theo biển số – GateService gọi khi camera đọc được biển.
/// Chấp nhận mọi kiểu nhập: "51f 123.45", "51F-12345"... đều chuẩn hoá về "51F12345".
/// </summary>
public sealed class FindVehicleByPlateUseCase(IVehicleQueries queries) : IFindVehicleByPlateUseCase
{
    public async Task<VehicleDto> ExecuteAsync(string plate, CancellationToken cancellationToken = default)
    {
        var normalized = PlateNormalizer.Normalize(plate);
        if (!PlateNormalizer.IsValid(normalized))
            throw new ValidationException($"Biển số '{plate}' không đúng định dạng Thông tư 01/2021/TT-BCA.");

        return await queries.FindByPlateAsync(normalized, cancellationToken)
            ?? throw new NotFoundException("Xe có biển số", PlateNormalizer.Format(normalized));
    }
}
