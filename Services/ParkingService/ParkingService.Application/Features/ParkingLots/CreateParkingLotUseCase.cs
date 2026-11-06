using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.ParkingLots;

public interface ICreateParkingLotUseCase
{
    Task<int> ExecuteAsync(CreateParkingLotCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Đăng ký bãi đỗ xe mới (US-053).
/// Khởi tạo trạng thái PendingApproval chờ nộp hồ sơ KYB.
/// </summary>
public sealed class CreateParkingLotUseCase(
    IParkingLotManagementRepository repository,
    ILogger<CreateParkingLotUseCase> logger) : ICreateParkingLotUseCase
{
    public async Task<int> ExecuteAsync(CreateParkingLotCommand command, CancellationToken cancellationToken = default)
    {
        if (command.OwnerProfileId <= 0)
            throw new ValidationException("OwnerProfileId không hợp lệ.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Tên bãi đỗ không được để trống.");

        if (string.IsNullOrWhiteSpace(command.Address))
            throw new ValidationException("Địa chỉ không được để trống.");

        if (command.Latitude is < -90 or > 90 || command.Longitude is < -180 or > 180)
            throw new ValidationException("Tọa độ GPS không hợp lệ.");

        if (command.TotalSlots <= 0)
            throw new ValidationException("Tổng số chỗ đỗ phải lớn hơn 0.");

        if (command.MaxHeightCm <= 0)
            throw new ValidationException("Chiều cao thông thủy phải lớn hơn 0.");

        // Kiểm tra xem chủ bãi đã có bãi nào trùng tên chưa
        if (await repository.ExistsByNameAsync(command.OwnerProfileId, command.Name.Trim(), cancellationToken: cancellationToken))
            throw new ConflictException($"Chủ bãi đã có bãi đỗ mang tên '{command.Name.Trim()}'.");

        var lot = new ParkingLot(
            id: 0,
            name: command.Name.Trim(),
            address: command.Address.Trim(),
            latitude: command.Latitude,
            longitude: command.Longitude,
            totalSlots: command.TotalSlots,
            availableSlots: 0, // Mới tạo chưa được duyệt và chưa có slot hoạt động
            maxHeightCm: command.MaxHeightCm,
            openTime: command.OpenTime,
            closeTime: command.CloseTime,
            status: ParkingLotStatus.PendingApproval,
            ownerProfileId: command.OwnerProfileId,
            integrationTier: command.IntegrationTier,
            description: command.Description?.Trim(),
            hotlinePhone: command.HotlinePhone?.Trim());

        lot.SetLocation(command.City.Trim(), command.District?.Trim());

        await repository.AddAsync(lot, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Chủ bãi {OwnerProfileId} đã tạo bãi mới: {Name} (Id={Id})",
            command.OwnerProfileId, lot.Name, lot.Id);

        return lot.Id;
    }
}
