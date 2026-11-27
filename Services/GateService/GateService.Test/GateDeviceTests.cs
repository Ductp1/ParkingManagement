using GateService.Application.Features.GateDevices;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Test;

// T-703: CRUD + heartbeat của GateDevices (unit test với fake repository, không cần database).
public class GateDeviceTests
{
    private static readonly UpdateGateDeviceRequest UpdateRequest =
        new(2, "VC-CAM-OUT-02", "Webcam cổng ra (mới)", GateDeviceType.Webcam, "Exit", GateDeviceStatus.Offline, "2.0.0", false);

    [Fact]
    public async Task Create_gate_device_persists_minimum_info()
    {
        var repo = new FakeRepo();
        var dto = await new CreateGateDeviceUseCase(repo).ExecuteAsync(
            new CreateGateDeviceRequest(1, "  VC-QR-IN-09 ", "Máy quét QR cổng vào", GateDeviceType.QrScanner, " Entry "));

        var device = Assert.Single(repo.Devices);
        Assert.Equal("VC-QR-IN-09", device.Code);        // trim
        Assert.Equal("Máy quét QR cổng vào", device.Name);
        Assert.Equal("Entry", device.Position);          // trim
        Assert.Equal(GateDeviceStatus.Online, device.Status);
        Assert.True(device.IsActive);
        Assert.Null(device.LastSeenAtUtc);               // chưa heartbeat
        Assert.Equal(dto.Code, device.Code);
    }

    [Fact]
    public async Task Create_with_duplicate_code_conflicts()
    {
        var repo = new FakeRepo();
        repo.Devices.Add(new GateDevice { ParkingLotId = 1, Code = "VC-QR-IN-09", Name = "Đã tồn tại" });

        await Assert.ThrowsAsync<ConflictException>(() => new CreateGateDeviceUseCase(repo).ExecuteAsync(
            new CreateGateDeviceRequest(1, "VC-QR-IN-09", "Trùng mã", GateDeviceType.QrScanner)));
    }

    [Theory]
    [InlineData(0, "VC-01", "Tên")]
    [InlineData(1, "  ", "Tên")]
    [InlineData(1, "VC-01", "  ")]
    public async Task Create_with_invalid_input_is_rejected(int parkingLotId, string code, string name)
        => await Assert.ThrowsAsync<ValidationException>(() => new CreateGateDeviceUseCase(new FakeRepo()).ExecuteAsync(
            new CreateGateDeviceRequest(parkingLotId, code, name, GateDeviceType.QrScanner)));

    [Fact]
    public async Task Update_gate_device_changes_fields()
    {
        var repo = new FakeRepo();
        repo.Devices.Add(new GateDevice { ParkingLotId = 1, Code = "VC-CAM-OUT-02", Name = "Webcam cổng ra", DeviceType = GateDeviceType.Webcam });

        var dto = await new UpdateGateDeviceUseCase(repo).ExecuteAsync(0, UpdateRequest);

        var device = repo.Devices.Single();
        Assert.Equal(2, device.ParkingLotId);
        Assert.Equal("Webcam cổng ra (mới)", device.Name);
        Assert.Equal(GateDeviceStatus.Offline, device.Status);
        Assert.False(device.IsActive);
        Assert.Equal(dto.Status, device.Status.ToString());
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Update_non_existing_device_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(
            () => new UpdateGateDeviceUseCase(new FakeRepo()).ExecuteAsync(999, UpdateRequest));

    [Fact]
    public async Task Delete_gate_device_removes_it()
    {
        var repo = new FakeRepo();
        repo.Devices.Add(new GateDevice { ParkingLotId = 1, Code = "VC-BAR-01", Name = "Barie" });

        await new DeleteGateDeviceUseCase(repo).ExecuteAsync(0);

        Assert.Empty(repo.Devices);
    }

    [Fact]
    public async Task Delete_non_existing_device_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(
            () => new DeleteGateDeviceUseCase(new FakeRepo()).ExecuteAsync(999));

    [Fact]
    public async Task Get_gate_devices_lists_all_devices()
    {
        var queries = new FakeQueries(
            new GateDeviceDto(1, 1, "VC-QR-IN-01", "Máy quét QR", "QrScanner", "Entry", "Online", null, null, true, DateTime.UtcNow, null),
            new GateDeviceDto(2, 2, "TSN-ANPR-IN-01", "Camera ANPR", "AnprCamera", "Entry", "Offline", DateTime.UtcNow, "4.2.1", true, DateTime.UtcNow, null));

        var devices = await new GetGateDevicesUseCase(queries).ExecuteAsync(null, null);

        Assert.Equal(2, devices.Count);
    }

    [Fact]
    public async Task Get_gate_devices_with_invalid_lot_id_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(
            () => new GetGateDevicesUseCase(new FakeQueries()).ExecuteAsync(-1, null));

    [Fact]
    public async Task Get_device_by_non_existing_id_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(
            () => new GetGateDeviceByIdUseCase(new FakeQueries()).ExecuteAsync(999));

    [Fact]
    public async Task Heartbeat_updates_last_heartbeat_timestamp()
    {
        var repo = new FakeRepo();
        repo.Devices.Add(new GateDevice { ParkingLotId = 1, Code = "VC-QR-IN-01", Name = "Máy quét QR" });
        var now = new DateTimeOffset(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);
        var useCase = new RecordGateDeviceHeartbeatUseCase(repo, new FixedTimeProvider(now));

        var dto = await useCase.ExecuteAsync(0);

        Assert.Equal(now.UtcDateTime, dto.LastSeenAtUtc); // mốc heartbeat được đóng dấu đúng thời gian
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Heartbeat_non_existing_device_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(
            () => new RecordGateDeviceHeartbeatUseCase(new FakeRepo(), TimeProvider.System).ExecuteAsync(999));

    private sealed class FakeRepo : IGateDeviceRepository
    {
        public List<GateDevice> Devices { get; } = [];
        public int SaveCount { get; private set; }

        public Task<GateDevice?> FindTrackedByIdAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult(Devices.FirstOrDefault(d => d.Id == id));

        public Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Devices.Any(d => d.Code == code && (excludeId == null || d.Id != excludeId)));

        public Task AddAsync(GateDevice device, CancellationToken cancellationToken)
        {
            // BaseEntity.Id có setter protected → fake giữ Id mặc định (0); test "not found" dùng Id 999.
            Devices.Add(device);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(GateDevice device, CancellationToken cancellationToken)
        {
            Devices.Remove(device);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeQueries(params GateDeviceDto[] devices) : IGateDeviceQueries
    {
        public Task<IReadOnlyList<GateDeviceDto>> ListAsync(int? parkingLotId, GateDeviceStatus? status, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GateDeviceDto>>(devices);

        public Task<GateDeviceDto?> FindByIdAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult(devices.FirstOrDefault(d => d.Id == id));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
