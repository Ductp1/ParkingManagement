using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Capacity;

public interface IGetLotCapacityConfigUseCase
{
    Task<LotCapacityConfigDto> ExecuteAsync(int lotId, int? ownerProfileId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lấy thông tin cấu hình sức chứa & vùng đệm khách vãng lai (UI-26).
/// Tự động khởi tạo cấu hình an toàn mặc định (Auto-Provisioning) nếu bãi xe mới chưa có cấu hình.
/// </summary>
public sealed class GetLotCapacityConfigUseCase(
    IParkingLotManagementRepository parkingLotRepository,
    ILotCapacityConfigRepository capacityConfigRepository,
    ILogger<GetLotCapacityConfigUseCase> logger) : IGetLotCapacityConfigUseCase
{
    public async Task<LotCapacityConfigDto> ExecuteAsync(int lotId, int? ownerProfileId = null, CancellationToken cancellationToken = default)
    {
        if (lotId <= 0)
            throw new ValidationException("Mã bãi đỗ (lotId) không hợp lệ.");

        var lot = await parkingLotRepository.GetByIdAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
        {
            throw new ConflictException("Bạn không có quyền truy cập cấu hình của bãi đỗ xe này.");
        }

        var config = await capacityConfigRepository.GetByParkingLotIdAsync(lotId, trackChanges: true, cancellationToken);

        if (config == null)
        {
            // Auto-provisioning cấu hình mặc định an toàn dựa trên IntegrationTier
            var isManual = lot.IntegrationTier == IntegrationTier.Manual;
            var defaultBufferPercent = isManual ? 30 : 10;
            var defaultBufferSlots = (int)Math.Round(lot.TotalSlots * (defaultBufferPercent / 100.0));
            var defaultOnlineQuota = Math.Max(0, lot.TotalSlots - defaultBufferSlots);
            var defaultLockThreshold = isManual ? 5 : 3;

            config = new LotCapacityConfig
            {
                ParkingLotId = lot.Id,
                OnlineQuota = defaultOnlineQuota,
                WalkInBufferPercent = defaultBufferPercent,
                OnlineLockThreshold = defaultLockThreshold,
                InstantBookingPaused = false,
                IsEmergencyStopped = lot.Status == ParkingLotStatus.Suspended,
                EmergencyReason = lot.Status == ParkingLotStatus.Suspended ? "Bãi đỗ đang bị tạm ngưng hoạt động" : null
            };

            await capacityConfigRepository.AddAsync(config, cancellationToken);
            await capacityConfigRepository.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Đã tự động khởi tạo LotCapacityConfig mặc định cho bãi xe {LotId} ({LotName})", lot.Id, lot.Name);
        }

        var calculatedBufferSlots = (int)Math.Round(lot.TotalSlots * (config.WalkInBufferPercent / 100.0));
        var maxAllowedOnlineQuota = Math.Max(0, lot.TotalSlots - calculatedBufferSlots);

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
            UpdatedAtUtc: config.UpdatedAtUtc ?? config.CreatedAtUtc);
    }
}
