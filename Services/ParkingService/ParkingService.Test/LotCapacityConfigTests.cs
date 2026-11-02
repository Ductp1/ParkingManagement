using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.API.Controllers;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.Capacity;
using ParkingService.Domain.Entities;

namespace ParkingService.Test;

public class LotCapacityConfigTests
{
    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);
        return entity;
    }

    private static ParkingLot CreateTestLot(
        int id = 1,
        int ownerProfileId = 10,
        int totalSlots = 100,
        IntegrationTier tier = IntegrationTier.Manual,
        ParkingLotStatus status = ParkingLotStatus.Active)
    {
        var lot = new ParkingLot(
            id: id,
            name: "Bãi xe Test",
            address: "123 Nguyễn Huệ, Quận 1",
            latitude: 10.7769,
            longitude: 106.7009,
            totalSlots: totalSlots,
            availableSlots: totalSlots,
            maxHeightCm: 220,
            openTime: new TimeOnly(6, 0),
            closeTime: new TimeOnly(23, 0),
            status: status,
            ownerProfileId: ownerProfileId,
            integrationTier: tier);

        return WithId(lot, id);
    }

    // ==================== 1. GET LOT CAPACITY CONFIG USE CASE ====================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetLotCapacityConfig_WithInvalidLotId_ThrowsValidationException(int lotId)
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var useCase = new GetLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<GetLotCapacityConfigUseCase>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(lotId));
    }

    [Fact]
    public async Task GetLotCapacityConfig_WhenLotNotFound_ThrowsNotFoundException()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var useCase = new GetLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<GetLotCapacityConfigUseCase>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(999));
    }

    [Fact]
    public async Task GetLotCapacityConfig_WhenCallerNotOwner_ThrowsConflictException()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10);
        await lotRepo.AddAsync(lot);

        var useCase = new GetLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<GetLotCapacityConfigUseCase>.Instance);

        // Caller has ownerProfileId = 99, but lot belongs to 10
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(1, ownerProfileId: 99));
    }

    [Fact]
    public async Task GetLotCapacityConfig_WhenConfigExists_ReturnsExistingConfigWithCalculatedFields()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, totalSlots: 100, tier: IntegrationTier.Manual);
        await lotRepo.AddAsync(lot);

        var existingConfig = WithId(new LotCapacityConfig
        {
            ParkingLotId = 1,
            WalkInBufferPercent = 20,
            OnlineQuota = 70,
            OnlineLockThreshold = 5,
            InstantBookingPaused = false,
            IsEmergencyStopped = false
        }, 1);
        await configRepo.AddAsync(existingConfig);

        var useCase = new GetLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<GetLotCapacityConfigUseCase>.Instance);

        var result = await useCase.ExecuteAsync(1, ownerProfileId: 10);

        Assert.NotNull(result);
        Assert.Equal(1, result.ParkingLotId);
        Assert.Equal("Bãi xe Test", result.ParkingLotName);
        Assert.Equal(100, result.TotalSlots);
        Assert.Equal(IntegrationTier.Manual, result.IntegrationTier);
        Assert.Equal(20, result.WalkInBufferPercent);
        Assert.Equal(20, result.CalculatedWalkInBufferSlots); // 100 * 20% = 20
        Assert.Equal(80, result.MaxAllowedOnlineQuota);       // 100 - 20 = 80
        Assert.Equal(70, result.OnlineQuota);
        Assert.Equal(5, result.OnlineLockThreshold);
        Assert.False(result.InstantBookingPaused);
        Assert.False(result.IsEmergencyStopped);
    }

    [Fact]
    public async Task GetLotCapacityConfig_WhenConfigDoesNotExist_AutoProvisions_ManualTier()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, totalSlots: 100, tier: IntegrationTier.Manual);
        await lotRepo.AddAsync(lot);

        var useCase = new GetLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<GetLotCapacityConfigUseCase>.Instance);

        var result = await useCase.ExecuteAsync(1, ownerProfileId: 10);

        Assert.NotNull(result);
        Assert.Equal(1, result.ParkingLotId);
        Assert.Equal(30, result.WalkInBufferPercent);           // Mặc định bãi Mức 0 (Manual): 30%
        Assert.Equal(30, result.CalculatedWalkInBufferSlots);   // 100 * 30% = 30
        Assert.Equal(70, result.MaxAllowedOnlineQuota);         // 100 - 30 = 70
        Assert.Equal(70, result.OnlineQuota);                  // Tự động gán = MaxAllowedOnlineQuota
        Assert.Equal(5, result.OnlineLockThreshold);           // Mức 0: threshold = 5

        // Xác nhận đã được tự động persist vào repository
        var savedInDb = await configRepo.GetByParkingLotIdAsync(1);
        Assert.NotNull(savedInDb);
        Assert.Equal(30, savedInDb.WalkInBufferPercent);
        Assert.Equal(70, savedInDb.OnlineQuota);
        Assert.Equal(5, savedInDb.OnlineLockThreshold);
    }

    [Fact]
    public async Task GetLotCapacityConfig_WhenConfigDoesNotExist_AutoProvisions_EdgeIotTier()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 2, ownerProfileId: 10, totalSlots: 200, tier: IntegrationTier.EdgeIot);
        await lotRepo.AddAsync(lot);

        var useCase = new GetLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<GetLotCapacityConfigUseCase>.Instance);

        var result = await useCase.ExecuteAsync(2, ownerProfileId: 10);

        Assert.NotNull(result);
        Assert.Equal(IntegrationTier.EdgeIot, result.IntegrationTier);
        Assert.Equal(10, result.WalkInBufferPercent);           // Mức 2 (EdgeIot): 10%
        Assert.Equal(20, result.CalculatedWalkInBufferSlots);   // 200 * 10% = 20
        Assert.Equal(180, result.MaxAllowedOnlineQuota);        // 200 - 20 = 180
        Assert.Equal(180, result.OnlineQuota);
        Assert.Equal(3, result.OnlineLockThreshold);            // Mức 1-2: threshold = 3
    }

    [Fact]
    public async Task GetLotCapacityConfig_WhenConfigDoesNotExist_AutoProvisions_PmsApiTier()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 3, ownerProfileId: 10, totalSlots: 150, tier: IntegrationTier.PmsApi);
        await lotRepo.AddAsync(lot);

        var useCase = new GetLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<GetLotCapacityConfigUseCase>.Instance);

        var result = await useCase.ExecuteAsync(3);

        Assert.NotNull(result);
        Assert.Equal(IntegrationTier.PmsApi, result.IntegrationTier);
        Assert.Equal(10, result.WalkInBufferPercent);           // Mức 1 (PmsApi): 10%
        Assert.Equal(15, result.CalculatedWalkInBufferSlots);   // 150 * 10% = 15
        Assert.Equal(135, result.MaxAllowedOnlineQuota);        // 150 - 15 = 135
        Assert.Equal(135, result.OnlineQuota);
        Assert.Equal(3, result.OnlineLockThreshold);
    }

    [Fact]
    public async Task GetLotCapacityConfig_WhenLotIsSuspended_AutoProvisionsWithEmergencyStop()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 4, ownerProfileId: 10, totalSlots: 100, status: ParkingLotStatus.Suspended);
        await lotRepo.AddAsync(lot);

        var useCase = new GetLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<GetLotCapacityConfigUseCase>.Instance);

        var result = await useCase.ExecuteAsync(4, ownerProfileId: 10);

        Assert.True(result.IsEmergencyStopped);
        Assert.NotNull(result.EmergencyReason);
    }

    // ==================== 2. UPDATE LOT CAPACITY CONFIG USE CASE ====================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateLotCapacityConfig_WithInvalidLotId_ThrowsValidationException(int lotId)
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var useCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);

        var command = new UpdateLotCapacityConfigCommand(lotId, 10, 50, 20, 5);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateLotCapacityConfig_WhenLotNotFound_ThrowsNotFoundException()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var useCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);

        var command = new UpdateLotCapacityConfigCommand(999, 10, 50, 20, 5);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateLotCapacityConfig_WhenCallerNotOwner_ThrowsConflictException()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10);
        await lotRepo.AddAsync(lot);

        var useCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);

        var command = new UpdateLotCapacityConfigCommand(1, OwnerProfileId: 99, 50, 20, 5);

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(150)]
    public async Task UpdateLotCapacityConfig_WhenBufferPercentOutOfBounds_ThrowsValidationException(int bufferPercent)
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, totalSlots: 100);
        await lotRepo.AddAsync(lot);

        var useCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);

        var command = new UpdateLotCapacityConfigCommand(1, 10, OnlineQuota: 50, WalkInBufferPercent: bufferPercent, OnlineLockThreshold: 5);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task UpdateLotCapacityConfig_WhenOnlineQuotaOutOfTotalSlots_ThrowsValidationException(int quota)
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, totalSlots: 100);
        await lotRepo.AddAsync(lot);

        var useCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);

        var command = new UpdateLotCapacityConfigCommand(1, 10, OnlineQuota: quota, WalkInBufferPercent: 10, OnlineLockThreshold: 0);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateLotCapacityConfig_WhenOnlineQuotaExceedsAllowedSlotsAfterBuffer_ThrowsValidationException()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, totalSlots: 100);
        await lotRepo.AddAsync(lot);

        var useCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);

        // Buffer = 30% -> calculatedBufferSlots = 30, maxAllowedOnlineQuota = 70.
        // OnlineQuota = 75 (> 70) => Vi phạm ràng buộc bảo vệ vùng đệm vãng lai
        var command = new UpdateLotCapacityConfigCommand(1, 10, OnlineQuota: 75, WalkInBufferPercent: 30, OnlineLockThreshold: 5);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("vượt quá mức cho phép tối đa", ex.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task UpdateLotCapacityConfig_WhenOnlineLockThresholdNegativeOrExceedsTotalSlots_ThrowsValidationException(int threshold)
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, totalSlots: 100);
        await lotRepo.AddAsync(lot);

        var useCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);

        var command = new UpdateLotCapacityConfigCommand(1, 10, OnlineQuota: 70, WalkInBufferPercent: 20, OnlineLockThreshold: threshold);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateLotCapacityConfig_WhenOnlineLockThresholdExceedsQuota_ThrowsValidationException()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, totalSlots: 100);
        await lotRepo.AddAsync(lot);

        var useCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);

        // Quota = 50, nhưng Threshold = 55 (> Quota) -> phi logic vì chạm ngưỡng trước khi mở bán
        var command = new UpdateLotCapacityConfigCommand(1, 10, OnlineQuota: 50, WalkInBufferPercent: 20, OnlineLockThreshold: 55);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("không được lớn hơn hạn ngạch trực tuyến", ex.Message);
    }

    [Fact]
    public async Task UpdateLotCapacityConfig_WhenConfigExists_UpdatesAndPersistsConfig()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, totalSlots: 100);
        await lotRepo.AddAsync(lot);

        var existingConfig = WithId(new LotCapacityConfig
        {
            ParkingLotId = 1,
            WalkInBufferPercent = 30,
            OnlineQuota = 70,
            OnlineLockThreshold = 5,
            InstantBookingPaused = false
        }, 1);
        await configRepo.AddAsync(existingConfig);

        var useCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);

        var command = new UpdateLotCapacityConfigCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            OnlineQuota: 60,
            WalkInBufferPercent: 25,
            OnlineLockThreshold: 10,
            InstantBookingPaused: true);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal(60, result.OnlineQuota);
        Assert.Equal(25, result.WalkInBufferPercent);
        Assert.Equal(25, result.CalculatedWalkInBufferSlots); // 100 * 25% = 25
        Assert.Equal(75, result.MaxAllowedOnlineQuota);        // 100 - 25 = 75
        Assert.Equal(10, result.OnlineLockThreshold);
        Assert.True(result.InstantBookingPaused);

        // Xác nhận trong repository đã cập nhật
        var updatedInDb = await configRepo.GetByParkingLotIdAsync(1);
        Assert.NotNull(updatedInDb);
        Assert.Equal(60, updatedInDb.OnlineQuota);
        Assert.Equal(25, updatedInDb.WalkInBufferPercent);
        Assert.Equal(10, updatedInDb.OnlineLockThreshold);
        Assert.True(updatedInDb.InstantBookingPaused);
    }

    [Fact]
    public async Task UpdateLotCapacityConfig_WhenConfigDoesNotExist_CreatesAndPersistsConfig()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, totalSlots: 80);
        await lotRepo.AddAsync(lot);

        var useCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);

        var command = new UpdateLotCapacityConfigCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            OnlineQuota: 50,
            WalkInBufferPercent: 20,
            OnlineLockThreshold: 4,
            InstantBookingPaused: false);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal(80, result.TotalSlots);
        Assert.Equal(50, result.OnlineQuota);
        Assert.Equal(20, result.WalkInBufferPercent);
        Assert.Equal(16, result.CalculatedWalkInBufferSlots); // 80 * 20% = 16
        Assert.Equal(64, result.MaxAllowedOnlineQuota);        // 80 - 16 = 64
        Assert.Equal(4, result.OnlineLockThreshold);

        var createdInDb = await configRepo.GetByParkingLotIdAsync(1);
        Assert.NotNull(createdInDb);
        Assert.Equal(50, createdInDb.OnlineQuota);
        Assert.Equal(20, createdInDb.WalkInBufferPercent);
    }

    // ==================== 3. REST CONTROLLER INTEGRATION TESTS ====================

    [Fact]
    public async Task Controller_Get_WhenCurrentUserIsOwner_ReturnsOkWithConfig()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, totalSlots: 100);
        await lotRepo.AddAsync(lot);

        var getUseCase = new GetLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<GetLotCapacityConfigUseCase>.Instance);
        var updateUseCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);
        var currentUser = new FakeCurrentUser { OwnerProfileId = 10, IsAuthenticated = true };

        var controller = new LotCapacityController(getUseCase, updateUseCase, currentUser);

        var actionResult = await controller.Get(lotId: 1, ownerProfileId: null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<LotCapacityConfigDto>(okResult.Value);
        Assert.Equal(1, dto.ParkingLotId);
        Assert.Equal(100, dto.TotalSlots);
    }

    [Fact]
    public async Task Controller_Get_WhenOwnerProfileIdSpecifiedInQuery_OverridesCurrentUser()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 25, totalSlots: 100);
        await lotRepo.AddAsync(lot);

        var getUseCase = new GetLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<GetLotCapacityConfigUseCase>.Instance);
        var updateUseCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);
        var currentUser = new FakeCurrentUser { OwnerProfileId = 999 }; // User khác

        var controller = new LotCapacityController(getUseCase, updateUseCase, currentUser);

        // Gửi ownerProfileId = 25 thông qua query parameter
        var actionResult = await controller.Get(lotId: 1, ownerProfileId: 25, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<LotCapacityConfigDto>(okResult.Value);
        Assert.Equal(1, dto.ParkingLotId);
    }

    [Fact]
    public async Task Controller_Update_WhenValidRequest_ReturnsOkWithUpdatedConfig()
    {
        var lotRepo = new FakeParkingLotRepo();
        var configRepo = new FakeCapacityConfigRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, totalSlots: 100);
        await lotRepo.AddAsync(lot);

        var getUseCase = new GetLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<GetLotCapacityConfigUseCase>.Instance);
        var updateUseCase = new UpdateLotCapacityConfigUseCase(lotRepo, configRepo, NullLogger<UpdateLotCapacityConfigUseCase>.Instance);
        var currentUser = new FakeCurrentUser { OwnerProfileId = 10 };

        var controller = new LotCapacityController(getUseCase, updateUseCase, currentUser);

        var request = new UpdateLotCapacityConfigRequest(
            OwnerProfileId: null, // Để controller fallback về currentUser.OwnerProfileId
            OnlineQuota: 65,
            WalkInBufferPercent: 20,
            OnlineLockThreshold: 5,
            InstantBookingPaused: false);

        var actionResult = await controller.Update(lotId: 1, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<LotCapacityConfigDto>(okResult.Value);
        Assert.Equal(65, dto.OnlineQuota);
        Assert.Equal(20, dto.WalkInBufferPercent);
    }

    // ==================== FAKE REPOSITORIES & DEPENDENCIES ====================

    private sealed class FakeParkingLotRepo : IParkingLotManagementRepository
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
            if (lot.Id == 0) WithId(lot, _nextId++);
            _lots.Add(lot);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ParkingLot lot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCapacityConfigRepo : ILotCapacityConfigRepository
    {
        private readonly List<LotCapacityConfig> _configs = [];
        private int _nextId = 1;

        public Task<LotCapacityConfig?> GetByParkingLotIdAsync(int parkingLotId, bool trackChanges = false, CancellationToken cancellationToken = default)
            => Task.FromResult(_configs.FirstOrDefault(c => c.ParkingLotId == parkingLotId));

        public Task AddAsync(LotCapacityConfig config, CancellationToken cancellationToken = default)
        {
            if (config.Id == 0) WithId(config, _nextId++);
            _configs.Add(config);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LotCapacityConfig config, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public int? UserId { get; set; } = 1;
        public string? Email { get; set; } = "test@owner.com";
        public IReadOnlyList<string> Roles { get; set; } = ["ParkingLotOwner"];
        public int? OwnerProfileId { get; set; } = 10;
        public bool IsAuthenticated { get; set; } = true;
    }
}
