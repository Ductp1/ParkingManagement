using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.Layouts;
using ParkingService.Domain.Entities;

namespace ParkingService.Test;

public class FloorLayoutTests
{
    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);
        return entity;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetFloorLayout_WithInvalidFloorId_ThrowsValidationException(int floorId)
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(floorId));
    }

    [Fact]
    public async Task GetFloorLayout_WhenFloorNotFound_ThrowsNotFoundException()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(999));
    }

    [Fact]
    public async Task GetFloorLayout_WhenPublishedLayoutExists_ReturnsLayoutAndEnrichesRealtimeSlotStates()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor
        {
            ZoneId = 1,
            Name = "Tầng B1",
            Level = -1,
            GridColumns = 20,
            GridRows = 10
        }, 1);
        await floorRepo.AddAsync(floor);

        // Ô đỗ trong DB có trạng thái realtime: A-01 đang Occupied, A-02 đang Reserved
        var slot1 = WithId(new Slot
        {
            FloorId = 1,
            Code = "A-01",
            GridX = 0,
            GridY = 0,
            SlotType = SlotType.Standard,
            MaxVehicleType = VehicleType.Suv,
            State = SlotState.Occupied,
            IsActive = true
        }, 101);

        var slot2 = WithId(new Slot
        {
            FloorId = 1,
            Code = "A-02",
            GridX = 1,
            GridY = 0,
            SlotType = SlotType.EvCharging,
            MaxVehicleType = VehicleType.Suv,
            State = SlotState.Reserved,
            IsActive = true
        }, 102);

        await slotRepo.AddAsync(slot1);
        await slotRepo.AddAsync(slot2);

        // Layout version đã xuất bản (lưu cell tĩnh nhưng không có trạng thái realtime)
        var layoutJson = JsonSerializer.Serialize(new
        {
            columns = 20,
            rows = 10,
            cells = new object[]
            {
                new { x = 0, y = 0, type = "slot", code = "A-01" },
                new { x = 1, y = 0, type = "slot", code = "A-02" },
                new { x = 0, y = 1, type = "road" },
                new { x = 19, y = 9, type = "exit" }
            }
        });

        var layoutVersion = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
            LayoutJson = layoutJson
        }, 1);
        await layoutRepo.AddAsync(layoutVersion);

        // Act
        var result = await useCase.ExecuteAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.FloorId);
        Assert.Equal("Tầng B1", result.FloorName);
        Assert.Equal(-1, result.Level);
        Assert.Equal(1, result.VersionNo);
        Assert.True(result.IsPublished);
        Assert.NotNull(result.PublishedAtUtc);
        Assert.Equal(20, result.Columns);
        Assert.Equal(10, result.Rows);
        Assert.Equal(4, result.Cells.Count);

        var cellA01 = result.Cells.FirstOrDefault(c => c.Code == "A-01");
        Assert.NotNull(cellA01);
        Assert.Equal("slot", cellA01.Type);
        Assert.Equal("Occupied", cellA01.State); // Đã được enrich trạng thái thực tế từ DB

        var cellA02 = result.Cells.FirstOrDefault(c => c.Code == "A-02");
        Assert.NotNull(cellA02);
        Assert.Equal("slot", cellA02.Type);
        Assert.Equal("Reserved", cellA02.State); // Đã được enrich trạng thái thực tế từ DB
        Assert.Equal("EvCharging", cellA02.SlotType);

        var roadCell = result.Cells.FirstOrDefault(c => c.Type == "road");
        Assert.NotNull(roadCell);
        Assert.Equal(0, roadCell.X);
        Assert.Equal(1, roadCell.Y);

        var exitCell = result.Cells.FirstOrDefault(c => c.Type == "exit");
        Assert.NotNull(exitCell);
        Assert.Equal(19, exitCell.X);
        Assert.Equal(9, exitCell.Y);
    }

    [Fact]
    public async Task GetFloorLayout_WhenNoLayoutVersionExists_SynthesizesLayoutFromDbSlots()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor
        {
            ZoneId = 1,
            Name = "Tầng Trệt",
            Level = 0,
            GridColumns = 15,
            GridRows = 8
        }, 2);
        await floorRepo.AddAsync(floor);

        // Tầng chưa có LayoutVersion nào, nhưng đã có 2 slots vật lý
        var slot1 = WithId(new Slot
        {
            FloorId = 2,
            Code = "MD-01",
            GridX = 2,
            GridY = 3,
            SlotType = SlotType.Standard,
            MaxVehicleType = VehicleType.Sedan,
            State = SlotState.Available,
            IsActive = true
        }, 201);

        var slot2 = WithId(new Slot
        {
            FloorId = 2,
            Code = "MD-02",
            GridX = 4,
            GridY = 5,
            SlotType = SlotType.Vip,
            MaxVehicleType = VehicleType.Suv,
            State = SlotState.Maintenance,
            IsActive = true
        }, 202);

        await slotRepo.AddAsync(slot1);
        await slotRepo.AddAsync(slot2);

        // Act
        var result = await useCase.ExecuteAsync(2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.FloorId);
        Assert.Equal("Tầng Trệt", result.FloorName);
        Assert.Equal(0, result.Level);
        Assert.Equal(0, result.VersionNo);
        Assert.False(result.IsPublished);
        Assert.Null(result.PublishedAtUtc);
        Assert.Equal(15, result.Columns);
        Assert.Equal(8, result.Rows);
        Assert.Equal(2, result.Cells.Count);

        var cell1 = result.Cells.FirstOrDefault(c => c.Code == "MD-01");
        Assert.NotNull(cell1);
        Assert.Equal(2, cell1.X);
        Assert.Equal(3, cell1.Y);
        Assert.Equal("Available", cell1.State);

        var cell2 = result.Cells.FirstOrDefault(c => c.Code == "MD-02");
        Assert.NotNull(cell2);
        Assert.Equal(4, cell2.X);
        Assert.Equal(5, cell2.Y);
        Assert.Equal("Vip", cell2.SlotType);
        Assert.Equal("Maintenance", cell2.State);
    }

    [Fact]
    public async Task GetFloorLayout_WhenLayoutJsonIsCorrupted_GracefullyFallsBackToSynthesis()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor
        {
            ZoneId = 1,
            Name = "Tầng 2",
            Level = 2,
            GridColumns = 10,
            GridRows = 10
        }, 3);
        await floorRepo.AddAsync(floor);

        var slot = WithId(new Slot
        {
            FloorId = 3,
            Code = "T2-01",
            GridX = 1,
            GridY = 1,
            State = SlotState.Available,
            IsActive = true
        }, 301);
        await slotRepo.AddAsync(slot);

        // LayoutVersion chứa JSON lỗi
        var brokenLayout = WithId(new LayoutVersion
        {
            FloorId = 3,
            VersionNo = 1,
            IsPublished = true,
            LayoutJson = "BROKEN_CORRUPTED_JSON{{"
        }, 10);
        await layoutRepo.AddAsync(brokenLayout);

        // Act - Không bị ném lỗi mà tự động fallback synthesis
        var result = await useCase.ExecuteAsync(3);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.VersionNo);
        Assert.False(result.IsPublished);
        Assert.Single(result.Cells);
        Assert.Equal("T2-01", result.Cells[0].Code);
    }

    // ==================== FAKE REPOSITORIES FOR TESTING ====================

    private sealed class FakeLayoutVersionRepo : ILayoutVersionRepository
    {
        private readonly List<LayoutVersion> _versions = [];
        private int _nextId = 1;

        public Task<LayoutVersion?> GetLatestPublishedByFloorIdAsync(int floorId, CancellationToken cancellationToken = default)
            => Task.FromResult(_versions
                .Where(v => v.FloorId == floorId && v.IsPublished)
                .OrderByDescending(v => v.VersionNo)
                .FirstOrDefault());

        public Task<LayoutVersion?> GetByFloorIdAndVersionAsync(int floorId, int versionNo, CancellationToken cancellationToken = default)
            => Task.FromResult(_versions.FirstOrDefault(v => v.FloorId == floorId && v.VersionNo == versionNo));

        public Task<IReadOnlyList<LayoutVersion>> GetAllVersionsByFloorIdAsync(int floorId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LayoutVersion>>(_versions
                .Where(v => v.FloorId == floorId)
                .OrderByDescending(v => v.VersionNo)
                .ToList());

        public Task<int> GetNextVersionNoAsync(int floorId, CancellationToken cancellationToken = default)
        {
            var max = _versions.Where(v => v.FloorId == floorId).Select(v => v.VersionNo).DefaultIfEmpty(0).Max();
            return Task.FromResult(max + 1);
        }

        public Task AddAsync(LayoutVersion layoutVersion, CancellationToken cancellationToken = default)
        {
            if (layoutVersion.Id == 0) WithId(layoutVersion, _nextId++);
            _versions.Add(layoutVersion);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LayoutVersion layoutVersion, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeFloorRepo : IFloorRepository
    {
        private readonly List<Floor> _floors = [];
        private int _nextId = 1;

        public Task<Floor?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_floors.FirstOrDefault(f => f.Id == id));

        public Task<Floor?> GetWithSlotsAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_floors.FirstOrDefault(f => f.Id == id));

        public Task<IReadOnlyList<Floor>> GetByZoneIdAsync(int zoneId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Floor>>(_floors.Where(f => f.ZoneId == zoneId).ToList());

        public Task<bool> ExistsNameAsync(int zoneId, string name, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_floors.Any(f => f.ZoneId == zoneId && f.Name == name && (!excludeId.HasValue || f.Id != excludeId.Value)));

        public Task AddAsync(Floor floor, CancellationToken cancellationToken = default)
        {
            if (floor.Id == 0) WithId(floor, _nextId++);
            _floors.Add(floor);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Floor floor, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Floor floor, CancellationToken cancellationToken = default)
        {
            _floors.Remove(floor);
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSlotRepo : ISlotRepository
    {
        private readonly List<Slot> _slots = [];
        private int _nextId = 1;

        public Task<Slot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_slots.FirstOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<Slot>> GetByFloorIdAsync(int floorId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Slot>>(_slots.Where(s => s.FloorId == floorId).ToList());

        public Task<bool> ExistsCodeAsync(int floorId, string code, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_slots.Any(s => s.FloorId == floorId && s.Code == code && (!excludeId.HasValue || s.Id != excludeId.Value)));

        public Task<bool> IsGridCellOccupiedAsync(int floorId, int gridX, int gridY, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_slots.Any(s => s.FloorId == floorId && s.GridX == gridX && s.GridY == gridY && (!excludeId.HasValue || s.Id != excludeId.Value)));

        public Task AddAsync(Slot slot, CancellationToken cancellationToken = default)
        {
            if (slot.Id == 0) WithId(slot, _nextId++);
            _slots.Add(slot);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Slot> slots, CancellationToken cancellationToken = default)
        {
            foreach (var s in slots)
            {
                if (s.Id == 0) WithId(s, _nextId++);
                _slots.Add(s);
            }
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Slot slot, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Slot slot, CancellationToken cancellationToken = default)
        {
            _slots.Remove(slot);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
