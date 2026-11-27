using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Application.Features.GateDevices;

// ===== DTO =====
public sealed record GateDeviceDto(int Id, int ParkingLotId, string Code, string Name, string DeviceType, string? Position,
    string Status, DateTime? LastSeenAtUtc, string? FirmwareVersion, bool IsActive, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

public sealed record CreateGateDeviceRequest(int ParkingLotId, string Code, string Name, GateDeviceType DeviceType,
    string? Position = null, GateDeviceStatus Status = GateDeviceStatus.Online, string? FirmwareVersion = null, bool IsActive = true);

public sealed record UpdateGateDeviceRequest(int ParkingLotId, string Code, string Name, GateDeviceType DeviceType,
    string? Position = null, GateDeviceStatus Status = GateDeviceStatus.Online, string? FirmwareVersion = null, bool IsActive = true);

// ===== PORT (đọc) =====
public interface IGateDeviceQueries
{
    Task<IReadOnlyList<GateDeviceDto>> ListAsync(int? parkingLotId, GateDeviceStatus? status, CancellationToken cancellationToken);
    Task<GateDeviceDto?> FindByIdAsync(int id, CancellationToken cancellationToken);
}

// ===== PORT (ghi) =====
public interface IGateDeviceRepository
{
    /// <summary>Tìm theo Id, entity có tracking để cập nhật/xoá.</summary>
    Task<GateDevice?> FindTrackedByIdAsync(int id, CancellationToken cancellationToken);
    /// <summary>Mã thiết bị là duy nhất toàn hệ thống; excludeId dùng khi update (bỏ qua chính nó).</summary>
    Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(GateDevice device, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
    Task RemoveAsync(GateDevice device, CancellationToken cancellationToken);
}

// ===== USE CASE (đọc) =====
public interface IGetGateDevicesUseCase
{
    Task<IReadOnlyList<GateDeviceDto>> ExecuteAsync(int? parkingLotId, GateDeviceStatus? status, CancellationToken cancellationToken = default);
}

/// <summary>T-703: Danh sách thiết bị cổng của một bãi (lọc theo bãi / trạng thái).</summary>
public sealed class GetGateDevicesUseCase(IGateDeviceQueries queries) : IGetGateDevicesUseCase
{
    public Task<IReadOnlyList<GateDeviceDto>> ExecuteAsync(int? parkingLotId, GateDeviceStatus? status, CancellationToken cancellationToken = default)
    {
        if (parkingLotId is <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        return queries.ListAsync(parkingLotId, status, cancellationToken);
    }
}

public interface IGetGateDeviceByIdUseCase
{
    Task<GateDeviceDto> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class GetGateDeviceByIdUseCase(IGateDeviceQueries queries) : IGetGateDeviceByIdUseCase
{
    public async Task<GateDeviceDto> ExecuteAsync(int id, CancellationToken cancellationToken = default)
        => await queries.FindByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Thiết bị cổng", id);
}

// ===== USE CASE (ghi) =====
public interface ICreateGateDeviceUseCase
{
    Task<GateDeviceDto> ExecuteAsync(CreateGateDeviceRequest request, CancellationToken cancellationToken = default);
}

/// <summary>T-703: Đăng ký thiết bị mới tại một bãi (mã thiết bị duy nhất).</summary>
public sealed class CreateGateDeviceUseCase(IGateDeviceRepository repository) : ICreateGateDeviceUseCase
{
    public async Task<GateDeviceDto> ExecuteAsync(CreateGateDeviceRequest request, CancellationToken cancellationToken = default)
    {
        var (parkingLotId, code, name, deviceType, position, status, firmwareVersion, isActive) = request;
        ValidateCore(parkingLotId, code, name, position, firmwareVersion, deviceType, status);

        if (await repository.CodeExistsAsync(code, cancellationToken: cancellationToken))
            throw new ConflictException($"Mã thiết bị '{code}' đã tồn tại.");

        var device = new GateDevice
        {
            ParkingLotId = parkingLotId,
            Code = code.Trim(),
            Name = name.Trim(),
            DeviceType = deviceType,
            Position = GateDeviceMapper.NullIfEmpty(position),
            Status = status,
            FirmwareVersion = GateDeviceMapper.NullIfEmpty(firmwareVersion),
            IsActive = isActive,
        };
        await repository.AddAsync(device, cancellationToken);
        return GateDeviceMapper.ToDto(device);
    }

    private static void ValidateCore(int parkingLotId, string code, string name, string? position, string? firmwareVersion,
        GateDeviceType deviceType, GateDeviceStatus status)
    {
        if (parkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(code)) throw new ValidationException("Mã thiết bị không được để trống.");
        if (code.Trim().Length > 40) throw new ValidationException("Mã thiết bị tối đa 40 ký tự.");
        if (string.IsNullOrWhiteSpace(name)) throw new ValidationException("Tên thiết bị không được để trống.");
        if (name.Trim().Length > 100) throw new ValidationException("Tên thiết bị tối đa 100 ký tự.");
        if (position?.Trim().Length > 40) throw new ValidationException("Vị trí tối đa 40 ký tự.");
        if (firmwareVersion?.Trim().Length > 40) throw new ValidationException("FirmwareVersion tối đa 40 ký tự.");
        if (!Enum.IsDefined(deviceType)) throw new ValidationException("Loại thiết bị không hợp lệ.");
        if (!Enum.IsDefined(status)) throw new ValidationException("Trạng thái thiết bị không hợp lệ.");
    }
}

public interface IUpdateGateDeviceUseCase
{
    Task<GateDeviceDto> ExecuteAsync(int id, UpdateGateDeviceRequest request, CancellationToken cancellationToken = default);
}

/// <summary>T-703: Cập nhật thông tin thiết bị (PUT – thay toàn bộ các trường nghiệp vụ).</summary>
public sealed class UpdateGateDeviceUseCase(IGateDeviceRepository repository) : IUpdateGateDeviceUseCase
{
    public async Task<GateDeviceDto> ExecuteAsync(int id, UpdateGateDeviceRequest request, CancellationToken cancellationToken = default)
    {
        var device = await repository.FindTrackedByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Thiết bị cổng", id);

        var (parkingLotId, code, name, deviceType, position, status, firmwareVersion, isActive) = request;
        if (parkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(code)) throw new ValidationException("Mã thiết bị không được để trống.");
        code = code.Trim();
        if (code.Length > 40) throw new ValidationException("Mã thiết bị tối đa 40 ký tự.");
        if (string.IsNullOrWhiteSpace(name)) throw new ValidationException("Tên thiết bị không được để trống.");
        if (name.Trim().Length > 100) throw new ValidationException("Tên thiết bị tối đa 100 ký tự.");
        if (position?.Trim().Length > 40) throw new ValidationException("Vị trí tối đa 40 ký tự.");
        if (firmwareVersion?.Trim().Length > 40) throw new ValidationException("FirmwareVersion tối đa 40 ký tự.");
        if (!Enum.IsDefined(deviceType)) throw new ValidationException("Loại thiết bị không hợp lệ.");
        if (!Enum.IsDefined(status)) throw new ValidationException("Trạng thái thiết bị không hợp lệ.");

        if (!string.Equals(device.Code, code, StringComparison.Ordinal)
            && await repository.CodeExistsAsync(code, excludeId: id, cancellationToken: cancellationToken))
            throw new ConflictException($"Mã thiết bị '{code}' đã tồn tại.");

        device.ParkingLotId = parkingLotId;
        device.Code = code;
        device.Name = name.Trim();
        device.DeviceType = deviceType;
        device.Position = GateDeviceMapper.NullIfEmpty(position);
        device.Status = status;
        device.FirmwareVersion = GateDeviceMapper.NullIfEmpty(firmwareVersion);
        device.IsActive = isActive;
        await repository.SaveAsync(cancellationToken);
        return GateDeviceMapper.ToDto(device);
    }
}

public interface IDeleteGateDeviceUseCase
{
    Task ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>T-703: Gỡ thiết bị khỏi hệ thống (GateDevice không dùng soft delete).</summary>
public sealed class DeleteGateDeviceUseCase(IGateDeviceRepository repository) : IDeleteGateDeviceUseCase
{
    public async Task ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var device = await repository.FindTrackedByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Thiết bị cổng", id);
        await repository.RemoveAsync(device, cancellationToken);
    }
}

public interface IRecordGateDeviceHeartbeatUseCase
{
    Task<GateDeviceDto> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// T-703: Heartbeat – chỉ đóng dấu mốc báo sống (LastSeenAtUtc – cột heartbeat có sẵn của entity).
/// Không tự đặt ra ngưỡng Online/Offline; việc đánh giá thiết bị còn "sống" hay không sẽ do
/// nghiệp vụ/monitoring quyết định dựa trên mốc thời gian này.
/// </summary>
public sealed class RecordGateDeviceHeartbeatUseCase(IGateDeviceRepository repository, TimeProvider timeProvider) : IRecordGateDeviceHeartbeatUseCase
{
    public async Task<GateDeviceDto> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var device = await repository.FindTrackedByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Thiết bị cổng", id);
        device.LastSeenAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await repository.SaveAsync(cancellationToken);
        return GateDeviceMapper.ToDto(device);
    }
}

/// <summary>Map entity → DTO dùng chung cho Application + Infrastructure queries.</summary>
public static class GateDeviceMapper
{
    public static GateDeviceDto ToDto(GateDevice d) => new(
        d.Id, d.ParkingLotId, d.Code, d.Name, d.DeviceType.ToString(), d.Position,
        d.Status.ToString(), d.LastSeenAtUtc, d.FirmwareVersion, d.IsActive, d.CreatedAtUtc, d.UpdatedAtUtc);

    internal static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
