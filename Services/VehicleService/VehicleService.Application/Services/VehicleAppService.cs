using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;
using VehicleService.Application.DTOs;
using VehicleService.Application.Interfaces;
using VehicleService.Domain.Entities;

namespace VehicleService.Application.Services;

/// <summary>
/// Service triển khai toàn bộ nghiệp vụ của xe.
/// </summary>
public sealed class VehicleAppService(IVehicleQueries queries) : IVehicleService
{
    /// <summary>
    /// Lấy danh sách xe trong Garage của tài xế (UC-07).
    /// </summary>
    public Task<IReadOnlyList<VehicleDto>> GetVehiclesByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ValidationException("userId phải là số nguyên dương.");

        return queries.ListByUserAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Tra cứu xe theo biển số (GateService gọi khi camera nhận diện).
    /// </summary>
    public async Task<VehicleDto> FindVehicleByPlateAsync(string plate, CancellationToken cancellationToken = default)
    {
        var normalized = PlateNormalizer.Normalize(plate);
        if (!PlateNormalizer.IsValid(normalized))
            throw new ValidationException($"Biển số '{plate}' không đúng định dạng Thông tư 01/2021/TT-BCA.");

        return await queries.FindByPlateAsync(normalized, cancellationToken)
            ?? throw new NotFoundException("Xe có biển số", PlateNormalizer.Format(normalized));
    }

    /// <summary>
    /// Thêm xe mới vào Garage với các kiểm tra nghiệp vụ (US-009, US-010).
    /// </summary>
    public async Task<VehicleDto> CreateVehicleAsync(CreateVehicleRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.UserId <= 0)
            throw new ValidationException("UserId phải là số nguyên dương.");

        if (request.HeightCm.HasValue && request.HeightCm.Value <= 0)
            throw new ValidationException("Chiều cao xe phải lớn hơn 0 cm.");

        if (request.LengthCm.HasValue && request.LengthCm.Value <= 0)
            throw new ValidationException("Chiều dài xe phải lớn hơn 0 cm.");

        if (request.WidthCm.HasValue && request.WidthCm.Value <= 0)
            throw new ValidationException("Chiều rộng xe phải lớn hơn 0 cm.");

        // 1. Chuẩn hóa và kiểm tra định dạng biển số TT 01/2021/TT-BCA
        var normalizedPlate = PlateNormalizer.Normalize(request.PlateNumber);
        if (!PlateNormalizer.IsValid(normalizedPlate))
            throw new ValidationException($"Biển số '{request.PlateNumber}' không đúng định dạng Thông tư 01/2021/TT-BCA.");

        // 2. Kiểm tra giới hạn tối đa 10 xe / tài khoản (US-010)
        var currentCount = await queries.CountByUserAsync(request.UserId, cancellationToken);
        if (currentCount >= 10)
            throw new ValidationException("Garage cá nhân tối đa 10 xe. Vui lòng liên hệ hỗ trợ gói doanh nghiệp B2B Fleet.");

        // 3. Kiểm tra trùng lặp biển số trong Garage của chính tài xế đó
        var isDuplicate = await queries.ExistsPlateInGarageAsync(request.UserId, normalizedPlate, cancellationToken);
        if (isDuplicate)
            throw new ValidationException($"Biển số '{PlateNormalizer.Format(normalizedPlate)}' đã tồn tại trong Garage của bạn.");

        // 4. Nếu xe mới đặt làm mặc định -> xóa cờ mặc định của các xe cũ
        if (request.IsDefault)
        {
            await queries.ClearDefaultForUserAsync(request.UserId, cancellationToken);
        }

        // Tự động gán làm mặc định nếu là xe đầu tiên trong Garage
        var isDefault = request.IsDefault || currentCount == 0;

        // 5. Chiều cao mặc định nếu người dùng không nhập
        var height = request.HeightCm ?? (request.VehicleType is VehicleType.Suv or VehicleType.Pickup ? 170 : 145);

        // 6. Tạo entity xe và lưu vào DB
        var vehicle = new Vehicle
        {
            UserId = request.UserId,
            PlateNumber = normalizedPlate,
            PlateDisplay = PlateNormalizer.Format(normalizedPlate),
            VehicleType = request.VehicleType,
            FuelType = request.FuelType,
            Brand = request.Brand?.Trim(),
            Model = request.Model?.Trim(),
            Color = request.Color?.Trim(),
            HeightCm = height,
            LengthCm = request.LengthCm,
            WidthCm = request.WidthCm,
            IsDefault = isDefault
        };

        return await queries.CreateAsync(vehicle, cancellationToken);
    }

    /// <summary>
    /// Đặt xe làm mặc định cho tài xế (Task P0.3, US-010).
    /// </summary>
    public async Task<VehicleDto> SetDefaultVehicleAsync(int vehicleId, int userId, CancellationToken cancellationToken = default)
    {
        if (vehicleId <= 0)
            throw new ValidationException("vehicleId phải là số nguyên dương.");

        if (userId <= 0)
            throw new ValidationException("userId phải là số nguyên dương.");

        var updated = await queries.SetDefaultAsync(vehicleId, userId, cancellationToken);
        return updated ?? throw new NotFoundException("Xe", vehicleId);
    }
}
