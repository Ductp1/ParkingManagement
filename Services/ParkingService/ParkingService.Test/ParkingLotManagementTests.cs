using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.Floors;
using ParkingService.Application.Features.ParkingLots;
using ParkingService.Application.Features.Slots;
using ParkingService.Application.Features.Zones;
using ParkingService.Domain.Entities;

namespace ParkingService.Test;

public class ParkingLotManagementTests
{
    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);
        return entity;
    }

    // ==================== PARKING LOT USE CASES ====================

    [Fact]
    public async Task CreateParkingLot_WithValidData_ReturnsNewIdAndPendingApprovalStatus()
    {
        var repo = new FakeLotManagementRepo();
        var useCase = new CreateParkingLotUseCase(repo, NullLogger<CreateParkingLotUseCase>.Instance);

        var command = new CreateParkingLotCommand(
            OwnerProfileId: 1,
            Name: "Bãi xe Bitexco",
            Address: "2 Hải Triều, Bến Nghé, Quận 1",
            City: "TP.HCM",
            District: "Quận 1",
            Latitude: 10.7719,
            Longitude: 106.7044,
            TotalSlots: 80,
            MaxHeightCm: 210,
            OpenTime: new TimeOnly(6, 0),
            CloseTime: new TimeOnly(22, 0));

        var lotId = await useCase.ExecuteAsync(command);

        Assert.True(lotId > 0);
        var created = await repo.GetByIdAsync(lotId);
        Assert.NotNull(created);
        Assert.Equal("Bãi xe Bitexco", created.Name);
        Assert.Equal(ParkingLotStatus.PendingApproval, created.Status);
        Assert.Equal(80, created.TotalSlots);
    }

    [Theory]
    [InlineData("", 10.77, 106.70, 50, 200)]      // Tên rỗng
    [InlineData("Bãi xe", 95.0, 106.70, 50, 200)]  // Vĩ độ > 90
    [InlineData("Bãi xe", 10.77, 106.70, 0, 200)]   // TotalSlots = 0
    [InlineData("Bãi xe", 10.77, 106.70, 50, -10)]  // Chiều cao <= 0
    public async Task CreateParkingLot_WithInvalidData_ThrowsValidationException(
        string name, double lat, double lng, int totalSlots, int heightCm)
    {
        var repo = new FakeLotManagementRepo();
        var useCase = new CreateParkingLotUseCase(repo, NullLogger<CreateParkingLotUseCase>.Instance);

        var command = new CreateParkingLotCommand(
            OwnerProfileId: 1,
            Name: name,
            Address: "Địa chỉ hợp lệ",
            City: "TP.HCM",
            District: null,
            Latitude: lat,
            Longitude: lng,
            TotalSlots: totalSlots,
            MaxHeightCm: heightCm,
            OpenTime: new TimeOnly(6, 0),
            CloseTime: new TimeOnly(22, 0));

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task CreateParkingLot_WithDuplicateName_ThrowsConflictException()
    {
        var repo = new FakeLotManagementRepo();
        var existingLot = new ParkingLot(1, "Bãi xe Sài Gòn", "Q.1", 10.77, 106.70, 50, 50, 210,
            new TimeOnly(6, 0), new TimeOnly(22, 0), ParkingLotStatus.Active, ownerProfileId: 1);
        await repo.AddAsync(existingLot);

        var useCase = new CreateParkingLotUseCase(repo, NullLogger<CreateParkingLotUseCase>.Instance);

        var command = new CreateParkingLotCommand(
            OwnerProfileId: 1,
            Name: "Bãi xe Sài Gòn", // Trùng tên của cùng chủ bãi 1
            Address: "Khác địa chỉ",
            City: "TP.HCM",
            District: null,
            Latitude: 10.78,
            Longitude: 106.71,
            TotalSlots: 30,
            MaxHeightCm: 200,
            OpenTime: new TimeOnly(6, 0),
            CloseTime: new TimeOnly(22, 0));

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateParkingLot_WithDuplicateName_ThrowsConflictException()
    {
        var repo = new FakeLotManagementRepo();
        var lot1 = new ParkingLot(1, "Bãi xe Bitexco", "Q.1", 10.77, 106.70, 50, 50, 210,
            new TimeOnly(6, 0), new TimeOnly(22, 0), ParkingLotStatus.Active, ownerProfileId: 1);
        var lot2 = new ParkingLot(2, "Bãi xe Saigon Centre", "Q.1", 10.77, 106.70, 50, 50, 210,
            new TimeOnly(6, 0), new TimeOnly(22, 0), ParkingLotStatus.Active, ownerProfileId: 1);
        await repo.AddAsync(lot1);
        await repo.AddAsync(lot2);

        var useCase = new UpdateParkingLotUseCase(repo, NullLogger<UpdateParkingLotUseCase>.Instance);

        var command = new UpdateParkingLotCommand(
            Id: 2,
            OwnerProfileId: 1,
            Name: "Bãi xe Bitexco", // Trùng tên với Lot 1 của cùng owner 1
            Address: "65 Lê Lợi",
            City: "TP.HCM",
            District: "Quận 1",
            MaxHeightCm: 220,
            OpenTime: new TimeOnly(6, 0),
            CloseTime: new TimeOnly(22, 0));

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateParkingLot_WithSameName_Succeeds()
    {
        var repo = new FakeLotManagementRepo();
        var lot = new ParkingLot(1, "Bãi xe Bitexco", "2 Hải Triều", 10.77, 106.70, 50, 50, 210,
            new TimeOnly(6, 0), new TimeOnly(22, 0), ParkingLotStatus.Active, ownerProfileId: 1);
        await repo.AddAsync(lot);

        var useCase = new UpdateParkingLotUseCase(repo, NullLogger<UpdateParkingLotUseCase>.Instance);

        var command = new UpdateParkingLotCommand(
            Id: 1,
            OwnerProfileId: 1,
            Name: "Bãi xe Bitexco", // Giữ nguyên tên
            Address: "2 Hải Triều (Cập nhật)",
            City: "TP.HCM",
            District: "Quận 1",
            MaxHeightCm: 220,
            OpenTime: new TimeOnly(6, 0),
            CloseTime: new TimeOnly(23, 0));

        await useCase.ExecuteAsync(command);

        var updated = await repo.GetByIdAsync(1);
        Assert.NotNull(updated);
        Assert.Equal("2 Hải Triều (Cập nhật)", updated.Address);
    }

    [Fact]
    public async Task GetParkingLotHierarchy_ReturnsFullTree_ForContract4()
    {
        // Khởi tạo cây Lot -> Zone -> Floor -> Slot
        var lot = new ParkingLot(10, "Vincom Center", "72 Lê Thánh Tôn", 10.778, 106.702, 100, 80, 210,
            new TimeOnly(6, 0), new TimeOnly(23, 0), ParkingLotStatus.Active, ownerProfileId: 1);

        var zone = WithId(new Zone { ParkingLotId = 10, Code = "ZONE-A", Name = "Khu Tháp A", SortOrder = 1 }, 1);
        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng B2", Level = -2, GridColumns = 20, GridRows = 10 }, 101);
        var slot = WithId(new Slot
        {
            FloorId = 101,
            Code = "B2-01",
            SlotType = SlotType.Standard,
            MaxVehicleType = VehicleType.Suv,
            GridX = 0,
            GridY = 0,
            State = SlotState.Available,
            IsActive = true
        }, 1001);

        floor.Slots.Add(slot);
        zone.Floors.Add(floor);
        lot.Zones.Add(zone);

        var repo = new FakeLotManagementRepo();
        await repo.AddAsync(lot);

        var useCase = new GetParkingLotHierarchyUseCase(repo);
        var hierarchy = await useCase.ExecuteAsync(new GetParkingLotHierarchyQuery(10));

        Assert.Equal(10, hierarchy.Id);
        Assert.Equal("Vincom Center", hierarchy.Name);
        Assert.Single(hierarchy.Zones);
        Assert.Equal("ZONE-A", hierarchy.Zones[0].Code);
        Assert.Single(hierarchy.Zones[0].Floors);
        Assert.Equal("Tầng B2", hierarchy.Zones[0].Floors[0].Name);
        Assert.Single(hierarchy.Zones[0].Floors[0].Slots);
        Assert.Equal("B2-01", hierarchy.Zones[0].Floors[0].Slots[0].Code);
        Assert.Equal("Available", hierarchy.Zones[0].Floors[0].Slots[0].State);
    }

    // ==================== ZONE USE CASES ====================

    [Fact]
    public async Task UpdateZone_WithDuplicateCode_ThrowsConflictException()
    {
        var zoneRepo = new FakeZoneRepo();
        await zoneRepo.AddAsync(WithId(new Zone { ParkingLotId = 1, Code = "ZONE-A", Name = "Khu A" }, 1));
        await zoneRepo.AddAsync(WithId(new Zone { ParkingLotId = 1, Code = "ZONE-B", Name = "Khu B" }, 2));

        var useCase = new UpdateZoneUseCase(zoneRepo, NullLogger<UpdateZoneUseCase>.Instance);

        // Đổi Zone 2 thành mã ZONE-A (đã tồn tại trong bãi 1)
        var command = new UpdateZoneCommand(Id: 2, Code: "ZONE-A", Name: "Khu B Đổi Tên", IsOutdoor: false, IsClosed: false, ClosedReason: null, SortOrder: 2);
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateZone_WithSameCode_Succeeds()
    {
        var zoneRepo = new FakeZoneRepo();
        await zoneRepo.AddAsync(WithId(new Zone { ParkingLotId = 1, Code = "ZONE-A", Name = "Khu A" }, 1));

        var useCase = new UpdateZoneUseCase(zoneRepo, NullLogger<UpdateZoneUseCase>.Instance);

        // Giữ nguyên Code = ZONE-A, cập nhật Name
        var command = new UpdateZoneCommand(Id: 1, Code: "ZONE-A", Name: "Khu A VIP", IsOutdoor: true, IsClosed: false, ClosedReason: null, SortOrder: 1);
        var result = await useCase.ExecuteAsync(command);

        Assert.Equal("ZONE-A", result.Code);
        Assert.Equal("Khu A VIP", result.Name);
        Assert.True(result.IsOutdoor);
    }

    // ==================== FLOOR USE CASES ====================

    [Fact]
    public async Task UpdateFloor_WithDuplicateName_ThrowsConflictException()
    {
        var floorRepo = new FakeFloorRepo();
        await floorRepo.AddAsync(WithId(new Floor { ZoneId = 1, Name = "Tầng B1", Level = -1 }, 1));
        await floorRepo.AddAsync(WithId(new Floor { ZoneId = 1, Name = "Tầng B2", Level = -2 }, 2));

        var useCase = new UpdateFloorUseCase(floorRepo, NullLogger<UpdateFloorUseCase>.Instance);

        // Đổi tên Floor 2 thành "Tầng B1" (đã tồn tại trong Zone 1)
        var command = new UpdateFloorCommand(Id: 2, Name: "Tầng B1", MaxHeightCm: 220, MaxWeightKg: 3000, GridColumns: 10, GridRows: 10, IsClosed: false);
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateFloor_WithSameName_Succeeds()
    {
        var floorRepo = new FakeFloorRepo();
        await floorRepo.AddAsync(WithId(new Floor { ZoneId = 1, Name = "Tầng B1", Level = -1, GridColumns = 10, GridRows = 10 }, 1));

        var useCase = new UpdateFloorUseCase(floorRepo, NullLogger<UpdateFloorUseCase>.Instance);

        // Giữ nguyên Name = "Tầng B1", cập nhật GridColumns và MaxHeightCm
        var command = new UpdateFloorCommand(Id: 1, Name: "Tầng B1", MaxHeightCm: 230, MaxWeightKg: 3500, GridColumns: 15, GridRows: 12, IsClosed: false);
        var result = await useCase.ExecuteAsync(command);

        Assert.Equal("Tầng B1", result.Name);
        Assert.Equal(230, result.MaxHeightCm);
        Assert.Equal(15, result.GridColumns);
    }

    // ==================== SLOT USE CASES ====================

    [Fact]
    public async Task CreateSlot_WithDuplicateCode_ThrowsConflictException()
    {
        var floorRepo = new FakeFloorRepo();
        await floorRepo.AddAsync(WithId(new Floor { Name = "B1", GridColumns = 10, GridRows = 10 }, 1));

        var slotRepo = new FakeSlotRepo();
        await slotRepo.AddAsync(WithId(new Slot { FloorId = 1, Code = "A-01", GridX = 0, GridY = 0 }, 1));

        var useCase = new CreateSlotUseCase(slotRepo, floorRepo, NullLogger<CreateSlotUseCase>.Instance);

        var command = new CreateSlotCommand(FloorId: 1, Code: "A-01", GridX: 1, GridY: 1);
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task CreateSlot_WithOccupiedGridCoordinates_ThrowsConflictException()
    {
        var floorRepo = new FakeFloorRepo();
        await floorRepo.AddAsync(WithId(new Floor { Name = "B1", GridColumns = 10, GridRows = 10 }, 1));

        var slotRepo = new FakeSlotRepo();
        await slotRepo.AddAsync(WithId(new Slot { FloorId = 1, Code = "A-01", GridX = 2, GridY = 3 }, 1));

        var useCase = new CreateSlotUseCase(slotRepo, floorRepo, NullLogger<CreateSlotUseCase>.Instance);

        var command = new CreateSlotCommand(FloorId: 1, Code: "A-02", GridX: 2, GridY: 3); // Trùng tọa độ (2, 3)
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Theory]
    [InlineData(SlotState.Reserved)]
    [InlineData(SlotState.Occupied)]
    public async Task DeleteSlot_WhenSlotIsReservedOrOccupied_ThrowsConflictException(SlotState state)
    {
        var slotRepo = new FakeSlotRepo();
        var slot = WithId(new Slot { FloorId = 1, Code = "VIP-01", State = state }, 5);
        await slotRepo.AddAsync(slot);

        var useCase = new DeleteSlotUseCase(slotRepo, NullLogger<DeleteSlotUseCase>.Instance);
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(5));
    }

    [Fact]
    public async Task DeleteSlot_WhenSlotIsAvailable_DeletesSuccessfully()
    {
        var slotRepo = new FakeSlotRepo();
        var slot = WithId(new Slot { FloorId = 1, Code = "STD-01", State = SlotState.Available }, 5);
        await slotRepo.AddAsync(slot);

        var useCase = new DeleteSlotUseCase(slotRepo, NullLogger<DeleteSlotUseCase>.Instance);
        await useCase.ExecuteAsync(5);

        var remaining = await slotRepo.GetByIdAsync(5);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task BatchCreateSlots_GeneratesCorrectCodesAndCoordinates()
    {
        var floorRepo = new FakeFloorRepo();
        await floorRepo.AddAsync(WithId(new Floor { Name = "B2", GridColumns = 20, GridRows = 10 }, 1));

        var slotRepo = new FakeSlotRepo();
        var useCase = new BatchCreateSlotsUseCase(slotRepo, floorRepo, NullLogger<BatchCreateSlotsUseCase>.Instance);

        var count = await useCase.ExecuteAsync(new BatchCreateSlotsCommand(
            FloorId: 1,
            Prefix: "B2",
            Count: 5,
            StartIndex: 1,
            StartGridX: 0,
            StartGridY: 0));

        Assert.Equal(5, count);
        var slots = await slotRepo.GetByFloorIdAsync(1);
        Assert.Equal(5, slots.Count);
        Assert.Equal(["B2-01", "B2-02", "B2-03", "B2-04", "B2-05"], slots.Select(s => s.Code));
    }

    [Fact]
    public async Task UpdateSlot_WithDuplicateCode_ThrowsConflictException()
    {
        var slotRepo = new FakeSlotRepo();
        await slotRepo.AddAsync(WithId(new Slot { FloorId = 1, Code = "A-01", GridX = 0, GridY = 0 }, 1));
        await slotRepo.AddAsync(WithId(new Slot { FloorId = 1, Code = "A-02", GridX = 1, GridY = 0 }, 2));

        var useCase = new UpdateSlotUseCase(slotRepo, NullLogger<UpdateSlotUseCase>.Instance);

        // Đổi mã Slot 2 thành A-01 (đã có trên Floor 1)
        var command = new UpdateSlotCommand(Id: 2, Code: "A-01", SlotType: SlotType.Standard, MaxVehicleType: VehicleType.Suv, GridX: 1, GridY: 0, WidthCells: 1, HeightCells: 1, IsActive: true);
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateSlot_WithDuplicateGrid_ThrowsConflictException()
    {
        var slotRepo = new FakeSlotRepo();
        await slotRepo.AddAsync(WithId(new Slot { FloorId = 1, Code = "A-01", GridX = 0, GridY = 0 }, 1));
        await slotRepo.AddAsync(WithId(new Slot { FloorId = 1, Code = "A-02", GridX = 1, GridY = 0 }, 2));

        var useCase = new UpdateSlotUseCase(slotRepo, NullLogger<UpdateSlotUseCase>.Instance);

        // Đổi tọa độ Slot 2 sang (0, 0) (đã bị Slot 1 chiếm giữ)
        var command = new UpdateSlotCommand(Id: 2, Code: "A-02", SlotType: SlotType.Standard, MaxVehicleType: VehicleType.Suv, GridX: 0, GridY: 0, WidthCells: 1, HeightCells: 1, IsActive: true);
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateSlot_WithSameCodeAndGrid_Succeeds()
    {
        var slotRepo = new FakeSlotRepo();
        await slotRepo.AddAsync(WithId(new Slot { FloorId = 1, Code = "A-01", GridX = 0, GridY = 0, SlotType = SlotType.Standard }, 1));

        var useCase = new UpdateSlotUseCase(slotRepo, NullLogger<UpdateSlotUseCase>.Instance);

        // Giữ nguyên Code = A-01 và Grid (0, 0), cập nhật SlotType = EvCharging
        var command = new UpdateSlotCommand(Id: 1, Code: "A-01", SlotType: SlotType.EvCharging, MaxVehicleType: VehicleType.Suv, GridX: 0, GridY: 0, WidthCells: 1, HeightCells: 1, IsActive: true);
        var result = await useCase.ExecuteAsync(command);

        Assert.Equal("A-01", result.Code);
        Assert.Equal("EvCharging", result.SlotType);
    }

    // ==================== FAKE REPOSITORIES FOR TESTING ====================

    private sealed class FakeLotManagementRepo : IParkingLotManagementRepository
    {
        private readonly List<ParkingLot> _lots = [];
        private int _nextId = 1;

        public Task<ParkingLot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_lots.FirstOrDefault(l => l.Id == id));

        public Task<ParkingLot?> GetHierarchyAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_lots.FirstOrDefault(l => l.Id == id));

        public Task<IReadOnlyList<ParkingLot>> GetByOwnerProfileIdAsync(int ownerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ParkingLot>>(_lots.Where(l => l.OwnerProfileId == ownerProfileId).ToList());

        public Task<bool> ExistsByNameAsync(int ownerProfileId, string name, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_lots.Any(l => l.OwnerProfileId == ownerProfileId && l.Name == name && (!excludeId.HasValue || l.Id != excludeId.Value)));

        public Task AddAsync(ParkingLot lot, CancellationToken cancellationToken = default)
        {
            if (lot.Id == 0)
            {
                WithId(lot, _nextId++);
            }
            _lots.Add(lot);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ParkingLot lot, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeFloorRepo : IFloorRepository
    {
        private readonly List<Floor> _floors = [];

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

    private sealed class FakeZoneRepo : IZoneRepository
    {
        private readonly List<Zone> _zones = [];
        private int _nextId = 1;

        public Task<Zone?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_zones.FirstOrDefault(z => z.Id == id));

        public Task<IReadOnlyList<Zone>> GetByParkingLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Zone>>(_zones.Where(z => z.ParkingLotId == parkingLotId).OrderBy(z => z.SortOrder).ToList());

        public Task<bool> ExistsCodeAsync(int parkingLotId, string code, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_zones.Any(z => z.ParkingLotId == parkingLotId && z.Code == code && (!excludeId.HasValue || z.Id != excludeId.Value)));

        public Task AddAsync(Zone zone, CancellationToken cancellationToken = default)
        {
            if (zone.Id == 0) WithId(zone, _nextId++);
            _zones.Add(zone);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Zone zone, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Zone zone, CancellationToken cancellationToken = default)
        {
            _zones.Remove(zone);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
