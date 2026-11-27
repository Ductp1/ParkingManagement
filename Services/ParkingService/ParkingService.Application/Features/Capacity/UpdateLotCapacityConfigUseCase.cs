using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Capacity;

public interface IUpdateLotCapacityConfigUseCase
{
    Task<LotCapacityConfigDto> ExecuteAsync(UpdateLotCapacityConfigCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Cập nhật cấu hình sức chứa & vùng đệm cho khách vãng lai của bãi xe (UI-26).
/// Thực thi các quy tắc kiểm tra ràng buộc toán học logic:
/// 1. WalkInBufferPercent nằm trong [0, 100]%.
/// 2. OnlineQuota <= TotalSlots - CalculatedWalkInBufferSlots.
/// 3. OnlineLockThreshold <= OnlineQuota.
/// </summary>
public sealed class UpdateLotCapacityConfigUseCase(
    IParkingLotManagementRepository parkingLotRepository,
    ILotCapacityConfigRepository capacityConfigRepository,
    ILogger<UpdateLotCapacityConfigUseCase> logger) : IUpdateLotCapacityConfigUseCase
{
    public async Task<LotCapacityConfigDto> ExecuteAsync(UpdateLotCapacityConfigCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0)
            throw new ValidationException("Mã bãi đỗ (ParkingLotId) không hợp lệ.");

        var lot = await parkingLotRepository.GetByIdAsync(command.ParkingLotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe", command.ParkingLotId);

        if (command.OwnerProfileId.HasValue && command.OwnerProfileId.Value > 0 && lot.OwnerProfileId != command.OwnerProfileId.Value)
        {
            throw new ConflictException("Bạn không có quyền chỉnh sửa cấu hình của bãi đỗ xe này.");
        }

        // 1. Kiểm tra tỷ lệ vùng đệm vãng lai
        if (command.WalkInBufferPercent is < 0 or > 100)
        {
            throw new ValidationException("Tỷ lệ vùng đệm vãng lai (WalkInBufferPercent) phải nằm trong khoảng từ 0% đến 100%.");
        }

        // 2. Tính toán sức chứa khả dụng tối đa cho kênh Online
        var calculatedBufferSlots = (int)Math.Round(lot.TotalSlots * (command.WalkInBufferPercent / 100.0));
        var maxAllowedOnlineQuota = Math.Max(0, lot.TotalSlots - calculatedBufferSlots);

        // 3. Kiểm tra hạn ngạch OnlineQuota
        if (command.OnlineQuota < 0)
        {
            throw new ValidationException("Hạn ngạch mở bán trực tuyến (OnlineQuota) không được âm.");
        }

        if (command.OnlineQuota > lot.TotalSlots)
        {
            throw new ValidationException($"Hạn ngạch mở bán trực tuyến ({command.OnlineQuota}) không được vượt quá tổng sức chứa của bãi ({lot.TotalSlots}).");
        }

        if (command.OnlineQuota > maxAllowedOnlineQuota)
        {
            throw new ValidationException(
                $"Hạn ngạch mở bán trực tuyến ({command.OnlineQuota}) vượt quá mức cho phép tối đa ({maxAllowedOnlineQuota}) " +
                $"sau khi trừ vùng đệm vãng lai ({command.WalkInBufferPercent}% tương đương {calculatedBufferSlots} chỗ).");
        }

        // 4. Kiểm tra ngưỡng khóa kênh trực tuyến
        if (command.OnlineLockThreshold < 0)
        {
            throw new ValidationException("Ngưỡng khóa trực tuyến (OnlineLockThreshold) không được âm.");
        }

        if (command.OnlineLockThreshold > lot.TotalSlots)
        {
            throw new ValidationException($"Ngưỡng khóa trực tuyến ({command.OnlineLockThreshold}) không được vượt quá tổng sức chứa của bãi ({lot.TotalSlots}).");
        }

        if (command.OnlineLockThreshold > command.OnlineQuota)
        {
            throw new ValidationException(
                $"Ngưỡng khóa trực tuyến ({command.OnlineLockThreshold}) không được lớn hơn hạn ngạch trực tuyến ({command.OnlineQuota}).");
        }

        var config = await capacityConfigRepository.GetByParkingLotIdAsync(command.ParkingLotId, trackChanges: true, cancellationToken);

        if (config == null)
        {
            config = new LotCapacityConfig
            {
                ParkingLotId = lot.Id,
                OnlineQuota = command.OnlineQuota,
                WalkInBufferPercent = command.WalkInBufferPercent,
                OnlineLockThreshold = command.OnlineLockThreshold,
                InstantBookingPaused = command.InstantBookingPaused,
                IsEmergencyStopped = lot.Status == ParkingLotStatus.Suspended,
                EmergencyReason = lot.Status == ParkingLotStatus.Suspended ? "Bãi đỗ đang bị tạm ngưng hoạt động" : null
            };
            await capacityConfigRepository.AddAsync(config, cancellationToken);
        }
        else
        {
            config.OnlineQuota = command.OnlineQuota;
            config.WalkInBufferPercent = command.WalkInBufferPercent;
            config.OnlineLockThreshold = command.OnlineLockThreshold;
            config.InstantBookingPaused = command.InstantBookingPaused;
            await capacityConfigRepository.UpdateAsync(config, cancellationToken);
        }

        await capacityConfigRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã cập nhật LotCapacityConfig cho bãi {LotId}: Quota={Quota}, Buffer={Buffer}%, LockThreshold={Threshold}, Paused={Paused}",
            lot.Id, config.OnlineQuota, config.WalkInBufferPercent, config.OnlineLockThreshold, config.InstantBookingPaused);

        return new LotCapacityConfigDto(
            ParkingLotId: lot.Id,
            ParkingLotName: lot.Name,
            TotalSlots: lot.TotalSlots,
            IntegrationTier: lot.IntegrationTier,
            OnlineQuota: config.OnlineQuota,
            WalkInBufferPercent: config.WalkInBufferPercent,
            CalculatedWalkInBufferSlots: calculatedBufferSlots,
            MaxAllowedOnlineQuota: maxAllowedOnlineQuota,
            OnlineLockThreshold: config.OnlineLockThreshold,
            InstantBookingPaused: config.InstantBookingPaused,
            IsEmergencyStopped: config.IsEmergencyStopped,
            EmergencyReason: config.EmergencyReason,
            UpdatedAtUtc: config.UpdatedAtUtc);
    }
}
