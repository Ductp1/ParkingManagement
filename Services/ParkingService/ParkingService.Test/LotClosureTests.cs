using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.Closures;
using ParkingService.Domain.Entities;

namespace ParkingService.Test;

public class LotClosureTests
{
    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);
        return entity;
    }

    private static ParkingLot CreateTestLot(int id = 1, int totalSlots = 20, int availableSlots = 20)
    {
        return new ParkingLot(
            id: id,
            name: "Bãi xe Test",
            address: "123 Test Street",
            latitude: 10.77,
            longitude: 106.70,
            totalSlots: totalSlots,
            availableSlots: availableSlots,
            maxHeightCm: 220,
            openTime: new TimeOnly(6, 0),
            closeTime: new TimeOnly(22, 0),
            status: ParkingLotStatus.Active,
            ownerProfileId: 1);
    }

    // ==================== CREATE CLOSURE TESTS ====================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateClosure_WithInvalidLotId_ThrowsValidationException(int lotId)
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new CreateClosureScheduleUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<CreateClosureScheduleUseCase>.Instance);

        var command = new CreateClosureScheduleCommand(lotId, ClosureTargetType.Zone, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), "Bảo trì");
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task CreateClosure_WithEmptyReason_ThrowsValidationException()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new CreateClosureScheduleUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<CreateClosureScheduleUseCase>.Instance);

        var command = new CreateClosureScheduleCommand(1, ClosureTargetType.Zone, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), "");
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task CreateClosure_WhenEndsAtBeforeStartsAt_ThrowsValidationException()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new CreateClosureScheduleUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<CreateClosureScheduleUseCase>.Instance);

        var starts = DateTime.UtcNow.AddHours(2);
        var ends = DateTime.UtcNow.AddHours(1);
        var command = new CreateClosureScheduleCommand(1, ClosureTargetType.Zone, 1, starts, ends, "Bảo trì");
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task CreateClosure_WhenLotNotFound_ThrowsNotFoundException()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new CreateClosureScheduleUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<CreateClosureScheduleUseCase>.Instance);

        var command = new CreateClosureScheduleCommand(999, ClosureTargetType.Zone, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), "Bảo trì");
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task CreateClosure_WhenTargetNotFound_ThrowsNotFoundException()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new CreateClosureScheduleUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<CreateClosureScheduleUseCase>.Instance);

        await lotRepo.AddAsync(CreateTestLot(1));

        var command = new CreateClosureScheduleCommand(1, ClosureTargetType.Zone, 999, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), "Bảo trì");
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task CreateClosure_WhenActiveClosureOverlaps_ThrowsConflictException()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new CreateClosureScheduleUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<CreateClosureScheduleUseCase>.Instance);

        await lotRepo.AddAsync(CreateTestLot(1));
        var zone = WithId(new Zone { ParkingLotId = 1, Name = "Khu A", Code = "ZA" }, 1);
        await zoneRepo.AddAsync(zone);

        var existing = WithId(new ClosureSchedule
        {
            ParkingLotId = 1,
            TargetType = ClosureTargetType.Zone,
            TargetId = 1,
            StartsAtUtc = DateTime.UtcNow,
            EndsAtUtc = DateTime.UtcNow.AddHours(4),
            Reason = "Lịch đóng cũ",
            IsEmergency = false
        }, 1);
        await closureRepo.AddAsync(existing);

        // Tạo lịch trùng lặp
        var command = new CreateClosureScheduleCommand(
            1, ClosureTargetType.Zone, 1, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3), "Lịch mới đè lên");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("trùng lặp", ex.Message);
    }

    [Fact]
    public async Task CreateClosure_WhenOccupiedSlotPresentInNonEmergency_ThrowsConflictException()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new CreateClosureScheduleUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<CreateClosureScheduleUseCase>.Instance);

        await lotRepo.AddAsync(CreateTestLot(1));
        var zone = WithId(new Zone { ParkingLotId = 1, Name = "Khu A", Code = "ZA" }, 1);
        await zoneRepo.AddAsync(zone);
        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1" }, 1);
        await floorRepo.AddAsync(floor);

        // Slot đang có xe Occupied
        var slot1 = WithId(new Slot { FloorId = 1, Code = "A-01", State = SlotState.Occupied, IsActive = true }, 101);
        await slotRepo.AddAsync(slot1);

        var command = new CreateClosureScheduleCommand(
            1, ClosureTargetType.Floor, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), "Bảo trì sàn", IsEmergency: false);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("A-01", ex.Message);
        Assert.Contains("phương tiện đỗ", ex.Message);
    }

    [Fact]
    public async Task CreateClosure_WhenReservedSlotPresentInNonEmergency_ThrowsConflictException()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new CreateClosureScheduleUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<CreateClosureScheduleUseCase>.Instance);

        await lotRepo.AddAsync(CreateTestLot(1));
        var zone = WithId(new Zone { ParkingLotId = 1, Name = "Khu A", Code = "ZA" }, 1);
        await zoneRepo.AddAsync(zone);
        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1" }, 1);
        await floorRepo.AddAsync(floor);

        // Slot đang được giữ chỗ Reserved
        var slot1 = WithId(new Slot { FloorId = 1, Code = "A-02", State = SlotState.Reserved, IsActive = true }, 102);
        await slotRepo.AddAsync(slot1);

        var command = new CreateClosureScheduleCommand(
            1, ClosureTargetType.Slot, 102, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), "Sơn lại ô đỗ", IsEmergency: false);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("A-02", ex.Message);
        Assert.Contains("giữ chỗ", ex.Message);
    }

    [Fact]
    public async Task CreateClosure_WhenEmergencyClosure_SucceedsEvenIfOccupiedSlotsPresent()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new CreateClosureScheduleUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<CreateClosureScheduleUseCase>.Instance);

        await lotRepo.AddAsync(CreateTestLot(1, totalSlots: 10, availableSlots: 8));
        var zone = WithId(new Zone { ParkingLotId = 1, Name = "Khu B", Code = "ZB" }, 2);
        await zoneRepo.AddAsync(zone);
        var floor = WithId(new Floor { ZoneId = 2, Name = "Tầng B1" }, 3);
        await floorRepo.AddAsync(floor);

        // A-01 đang có xe Occupied, A-02 Available
        var slot1 = WithId(new Slot { FloorId = 3, Code = "B-01", State = SlotState.Occupied, IsActive = true }, 201);
        var slot2 = WithId(new Slot { FloorId = 3, Code = "B-02", State = SlotState.Available, IsActive = true }, 202);
        await slotRepo.AddAsync(slot1);
        await slotRepo.AddAsync(slot2);

        // Đóng khẩn cấp (IsEmergency = true, EndsAtUtc = null)
        var command = new CreateClosureScheduleCommand(
            1, ClosureTargetType.Floor, 3, DateTime.UtcNow, null, "Sự cố chập điện hầm B1", IsEmergency: true, UserId: 99);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.True(result.IsEmergency);
        Assert.Equal("Active", result.Status);
        Assert.Equal("Tầng Tầng B1", result.TargetName);

        // Floor phải IsClosed = true
        var updatedFloor = await floorRepo.GetByIdAsync(3);
        Assert.True(updatedFloor!.IsClosed);

        // Slot trống B-02 phải chuyển sang Maintenance, B-01 giữ nguyên Occupied
        var updatedSlot2 = await slotRepo.GetByIdAsync(202);
        Assert.Equal(SlotState.Maintenance, updatedSlot2!.State);

        var updatedSlot1 = await slotRepo.GetByIdAsync(201);
        Assert.Equal(SlotState.Occupied, updatedSlot1!.State);

        // AvailableSlots của bãi phải giảm đi 1 (từ 8 xuống 7)
        var updatedLot = await lotRepo.GetByIdAsync(1);
        Assert.Equal(7, updatedLot!.AvailableSlots);
    }

    [Fact]
    public async Task CreateClosure_WhenSafeImmediateClosure_TransitionsAvailableSlotsToMaintenance()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new CreateClosureScheduleUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<CreateClosureScheduleUseCase>.Instance);

        await lotRepo.AddAsync(CreateTestLot(1, totalSlots: 10, availableSlots: 10));
        var zone = WithId(new Zone { ParkingLotId = 1, Name = "Khu C", Code = "ZC" }, 3);
        await zoneRepo.AddAsync(zone);
        var floor = WithId(new Floor { ZoneId = 3, Name = "Tầng 2" }, 4);
        await floorRepo.AddAsync(floor);

        var slot1 = WithId(new Slot { FloorId = 4, Code = "C-01", State = SlotState.Available, IsActive = true }, 301);
        var slot2 = WithId(new Slot { FloorId = 4, Code = "C-02", State = SlotState.Available, IsActive = true }, 302);
        await slotRepo.AddAsync(slot1);
        await slotRepo.AddAsync(slot2);

        var command = new CreateClosureScheduleCommand(
            1, ClosureTargetType.Zone, 3, DateTime.UtcNow, DateTime.UtcNow.AddHours(3), "Vệ sinh toàn khu C", IsEmergency: false, UserId: 10);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal("Active", result.Status);

        // Zone phải IsClosed = true
        var updatedZone = await zoneRepo.GetByIdAsync(3);
        Assert.True(updatedZone!.IsClosed);
        Assert.Equal("Vệ sinh toàn khu C", updatedZone.ClosedReason);

        // Cả 2 slot phải sang Maintenance
        var updatedSlot1 = await slotRepo.GetByIdAsync(301);
        var updatedSlot2 = await slotRepo.GetByIdAsync(302);
        Assert.Equal(SlotState.Maintenance, updatedSlot1!.State);
        Assert.Equal(SlotState.Maintenance, updatedSlot2!.State);

        // AvailableSlots giảm từ 10 xuống 8
        var updatedLot = await lotRepo.GetByIdAsync(1);
        Assert.Equal(8, updatedLot!.AvailableSlots);

        // Phải ghi nhật ký SlotStateLog
        Assert.Equal(2, closureRepo.StateLogsCount);
    }

    // ==================== IMPACT CHECK TESTS ====================

    [Fact]
    public async Task ValidateClosureImpact_WhenOccupiedSlotsPresent_ReturnsCannotSafelyCloseAndListsAtRiskSlots()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new ValidateClosureImpactUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<ValidateClosureImpactUseCase>.Instance);

        await lotRepo.AddAsync(CreateTestLot(1));
        var zone = WithId(new Zone { ParkingLotId = 1, Name = "Khu A", Code = "ZA" }, 1);
        await zoneRepo.AddAsync(zone);
        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1" }, 1);
        await floorRepo.AddAsync(floor);

        await slotRepo.AddAsync(WithId(new Slot { FloorId = 1, Code = "A-01", State = SlotState.Occupied, IsActive = true }, 101));
        await slotRepo.AddAsync(WithId(new Slot { FloorId = 1, Code = "A-02", State = SlotState.Available, IsActive = true }, 102));

        var command = new ClosureImpactCheckCommand(1, ClosureTargetType.Floor, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), IsEmergency: false);
        var result = await useCase.ExecuteAsync(command);

        Assert.False(result.CanSafelyClose);
        Assert.Equal(2, result.TotalSlotsInTarget);
        Assert.Equal(1, result.OccupiedSlotsCount);
        Assert.Equal(1, result.AvailableSlotsCount);
        Assert.Contains("A-01", result.AtRiskSlotCodes);
        Assert.NotEmpty(result.Blockers);
    }

    [Fact]
    public async Task ValidateClosureImpact_WhenAllSlotsAvailable_ReturnsCanSafelyCloseTrue()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new ValidateClosureImpactUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<ValidateClosureImpactUseCase>.Instance);

        await lotRepo.AddAsync(CreateTestLot(1));
        var zone = WithId(new Zone { ParkingLotId = 1, Name = "Khu A", Code = "ZA" }, 1);
        await zoneRepo.AddAsync(zone);
        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1" }, 1);
        await floorRepo.AddAsync(floor);

        await slotRepo.AddAsync(WithId(new Slot { FloorId = 1, Code = "A-01", State = SlotState.Available, IsActive = true }, 101));
        await slotRepo.AddAsync(WithId(new Slot { FloorId = 1, Code = "A-02", State = SlotState.Available, IsActive = true }, 102));

        var command = new ClosureImpactCheckCommand(1, ClosureTargetType.Floor, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), IsEmergency: false);
        var result = await useCase.ExecuteAsync(command);

        Assert.True(result.CanSafelyClose);
        Assert.Equal(0, result.OccupiedSlotsCount);
        Assert.Equal(2, result.AvailableSlotsCount);
        Assert.Empty(result.Blockers);
    }

    // ==================== CANCEL / REOPEN CLOSURE TESTS ====================

    [Fact]
    public async Task CancelClosure_WhenActiveSchedule_RestoresMaintenanceSlotsToAvailableAndReopensTarget()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new CancelClosureScheduleUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<CancelClosureScheduleUseCase>.Instance);

        await lotRepo.AddAsync(CreateTestLot(1, totalSlots: 10, availableSlots: 8));
        var zone = WithId(new Zone { ParkingLotId = 1, Name = "Khu A", Code = "ZA", IsClosed = true, ClosedReason = "Bảo trì" }, 1);
        await zoneRepo.AddAsync(zone);
        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1", IsClosed = true }, 1);
        await floorRepo.AddAsync(floor);

        var slot1 = WithId(new Slot { FloorId = 1, Code = "A-01", State = SlotState.Maintenance, IsActive = true }, 101);
        var slot2 = WithId(new Slot { FloorId = 1, Code = "A-02", State = SlotState.Maintenance, IsActive = true }, 102);
        await slotRepo.AddAsync(slot1);
        await slotRepo.AddAsync(slot2);

        var schedule = WithId(new ClosureSchedule
        {
            ParkingLotId = 1,
            TargetType = ClosureTargetType.Floor,
            TargetId = 1,
            StartsAtUtc = DateTime.UtcNow.AddHours(-1),
            EndsAtUtc = DateTime.UtcNow.AddHours(2),
            Reason = "Bảo trì định kỳ",
            IsEmergency = false
        }, 1);
        await closureRepo.AddAsync(schedule);

        var command = new CancelClosureScheduleCommand(1, 1, "Hoàn tất bảo trì sớm hơn dự kiến", UserId: 5);
        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal("Cancelled", result.Status);

        // Floor phải mở lại IsClosed = false
        var updatedFloor = await floorRepo.GetByIdAsync(1);
        Assert.False(updatedFloor!.IsClosed);

        // Slots phải về Available
        var updatedSlot1 = await slotRepo.GetByIdAsync(101);
        var updatedSlot2 = await slotRepo.GetByIdAsync(102);
        Assert.Equal(SlotState.Available, updatedSlot1!.State);
        Assert.Equal(SlotState.Available, updatedSlot2!.State);

        // AvailableSlots tăng từ 8 lên 10
        var updatedLot = await lotRepo.GetByIdAsync(1);
        Assert.Equal(10, updatedLot!.AvailableSlots);
    }

    [Fact]
    public async Task CancelClosure_WhenAlreadyCancelled_ThrowsConflictException()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new CancelClosureScheduleUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<CancelClosureScheduleUseCase>.Instance);

        await lotRepo.AddAsync(CreateTestLot(1));
        var schedule = WithId(new ClosureSchedule
        {
            ParkingLotId = 1,
            TargetType = ClosureTargetType.Slot,
            TargetId = 101,
            StartsAtUtc = DateTime.UtcNow,
            CancelledAtUtc = DateTime.UtcNow.AddMinutes(-10),
            Reason = "Đã hủy"
        }, 1);
        await closureRepo.AddAsync(schedule);

        var command = new CancelClosureScheduleCommand(1, 1);
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    // ==================== GET CLOSURES TESTS ====================

    [Fact]
    public async Task GetLotClosureSchedules_ReturnsSchedulesWithComputedStatusAndTargetNames()
    {
        var lotRepo = new FakeLotManagementRepo();
        var zoneRepo = new FakeZoneRepo();
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var closureRepo = new FakeClosureScheduleRepo();
        var useCase = new GetLotClosureSchedulesUseCase(lotRepo, zoneRepo, floorRepo, slotRepo, closureRepo, NullLogger<GetLotClosureSchedulesUseCase>.Instance);

        await lotRepo.AddAsync(CreateTestLot(1));
        var zone = WithId(new Zone { ParkingLotId = 1, Name = "Khu VIP", Code = "VIP" }, 1);
        await zoneRepo.AddAsync(zone);

        var activeSchedule = WithId(new ClosureSchedule
        {
            ParkingLotId = 1,
            TargetType = ClosureTargetType.Zone,
            TargetId = 1,
            StartsAtUtc = DateTime.UtcNow.AddMinutes(-30),
            EndsAtUtc = DateTime.UtcNow.AddHours(2),
            Reason = "Đang đóng"
        }, 1);

        var upcomingSchedule = WithId(new ClosureSchedule
        {
            ParkingLotId = 1,
            TargetType = ClosureTargetType.Zone,
            TargetId = 1,
            StartsAtUtc = DateTime.UtcNow.AddDays(1),
            EndsAtUtc = DateTime.UtcNow.AddDays(2),
            Reason = "Sắp đóng ngày mai"
        }, 2);

        await closureRepo.AddAsync(activeSchedule);
        await closureRepo.AddAsync(upcomingSchedule);

        var result = await useCase.ExecuteAsync(1);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Status == "Active");
        Assert.Contains(result, s => s.Status == "Upcoming");
    }

    // ==================== FAKE REPOSITORIES FOR TESTING ====================

    private sealed class FakeClosureScheduleRepo : IClosureScheduleRepository
    {
        private readonly List<ClosureSchedule> _schedules = [];
        private readonly List<SlotStateLog> _logs = [];
        private int _nextId = 1;

        public int StateLogsCount => _logs.Count;

        public Task<ClosureSchedule?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_schedules.FirstOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<ClosureSchedule>> GetByParkingLotIdAsync(
            int parkingLotId, ClosureTargetType? targetType = null, bool? activeOnly = null, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var query = _schedules.Where(s => s.ParkingLotId == parkingLotId);
            if (targetType.HasValue) query = query.Where(s => s.TargetType == targetType.Value);
            if (activeOnly == true)
                query = query.Where(s => s.CancelledAtUtc == null && s.StartsAtUtc <= now && (s.EndsAtUtc == null || s.EndsAtUtc.Value > now));

            return Task.FromResult<IReadOnlyList<ClosureSchedule>>(query.OrderByDescending(s => s.StartsAtUtc).ToList());
        }

        public Task<bool> HasOverlappingClosureAsync(
            int parkingLotId, ClosureTargetType targetType, int targetId, DateTime startsAtUtc, DateTime? endsAtUtc, int? excludeId = null, CancellationToken cancellationToken = default)
        {
            var maxTime = DateTime.MaxValue;
            var effectiveEnd = endsAtUtc ?? maxTime;

            var overlap = _schedules.Any(
                s => s.ParkingLotId == parkingLotId
                     && s.TargetType == targetType
                     && s.TargetId == targetId
                     && s.CancelledAtUtc == null
                     && (!excludeId.HasValue || s.Id != excludeId.Value)
                     && startsAtUtc < (s.EndsAtUtc ?? maxTime)
                     && effectiveEnd > s.StartsAtUtc);

            return Task.FromResult(overlap);
        }

        public Task AddAsync(ClosureSchedule schedule, CancellationToken cancellationToken = default)
        {
            if (schedule.Id == 0) WithId(schedule, _nextId++);
            _schedules.Add(schedule);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ClosureSchedule schedule, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddStateLogsAsync(IEnumerable<SlotStateLog> logs, CancellationToken cancellationToken = default)
        {
            _logs.AddRange(logs);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeLotManagementRepo : IParkingLotManagementRepository
    {
        private readonly List<ParkingLot> _lots = [];

        public Task<ParkingLot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_lots.FirstOrDefault(p => p.Id == id && !p.IsDeleted));

        public Task<ParkingLot?> GetHierarchyAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_lots.FirstOrDefault(p => p.Id == id && !p.IsDeleted));

        public Task<IReadOnlyList<ParkingLot>> GetByOwnerProfileIdAsync(int ownerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ParkingLot>>(_lots.Where(p => p.OwnerProfileId == ownerProfileId && !p.IsDeleted).ToList());

        public Task<bool> ExistsByNameAsync(int ownerProfileId, string name, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_lots.Any(p => p.OwnerProfileId == ownerProfileId && p.Name == name && (!excludeId.HasValue || p.Id != excludeId.Value)));

        public Task AddAsync(ParkingLot lot, CancellationToken cancellationToken = default)
        {
            _lots.Add(lot);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ParkingLot lot, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeZoneRepo : IZoneRepository
    {
        private readonly List<Zone> _zones = [];
        private int _nextId = 1;

        public Task<Zone?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_zones.FirstOrDefault(z => z.Id == id));

        public Task<IReadOnlyList<Zone>> GetByParkingLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Zone>>(_zones.Where(z => z.ParkingLotId == parkingLotId).ToList());

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
