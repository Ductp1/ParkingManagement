using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.ParkingLots;
using ParkingService.Domain.Entities;
using Xunit;

namespace ParkingService.Test;

public class OwnerLotExitTests
{
    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);
        return entity;
    }

    private static ParkingLot CreateLot(int id, int ownerProfileId, string name, ParkingLotStatus status, int totalSlots = 50, int availableSlots = 20)
    {
        var lot = new ParkingLot(
            id: id,
            name: name,
            address: "123 Lê Lợi, Q1",
            latitude: 10.77,
            longitude: 106.70,
            totalSlots: totalSlots,
            availableSlots: availableSlots,
            maxHeightCm: 220,
            openTime: new TimeOnly(6, 0),
            closeTime: new TimeOnly(22, 0),
            status: status,
            ownerProfileId: ownerProfileId);
        return WithId(lot, id);
    }

    [Fact]
    public async Task GetOwnerExitImpact_ShouldReturnAccurateLotsCountAndTotals()
    {
        var lot1 = CreateLot(1, 10, "Bãi xe Vincom", ParkingLotStatus.Active, totalSlots: 80, availableSlots: 30);
        var lot2 = CreateLot(2, 10, "Bãi xe Bitexco", ParkingLotStatus.Suspended, totalSlots: 50, availableSlots: 0);
        var lot3 = CreateLot(3, 10, "Bãi xe Landmark", ParkingLotStatus.Active, totalSlots: 70, availableSlots: 10);
        var otherOwnerLot = CreateLot(4, 99, "Bãi người khác", ParkingLotStatus.Active, totalSlots: 100, availableSlots: 50);

        var repo = new FakeLotManagementRepo([lot1, lot2, lot3, otherOwnerLot]);
        var useCase = new GetOwnerExitImpactUseCase(repo);

        var result = await useCase.ExecuteAsync(10);

        Assert.NotNull(result);
        Assert.Equal(10, result.OwnerProfileId);
        Assert.Equal(3, result.TotalLots);
        Assert.Equal(2, result.ActiveLots);
        Assert.Equal(1, result.SuspendedLots);
        Assert.Equal(200, result.TotalSlots); // 80 + 50 + 70
        Assert.Equal(3, result.Lots.Count);
        Assert.Contains(result.Lots, l => l.LotId == 1 && l.Status == "Active");
        Assert.Contains(result.Lots, l => l.LotId == 2 && l.Status == "Suspended");
    }

    [Fact]
    public async Task GetOwnerExitImpact_WhenNoLots_ShouldReturnZeros()
    {
        var repo = new FakeLotManagementRepo([]);
        var useCase = new GetOwnerExitImpactUseCase(repo);

        var result = await useCase.ExecuteAsync(10);

        Assert.Equal(10, result.OwnerProfileId);
        Assert.Equal(0, result.TotalLots);
        Assert.Equal(0, result.ActiveLots);
        Assert.Equal(0, result.SuspendedLots);
        Assert.Equal(0, result.TotalSlots);
        Assert.Empty(result.Lots);
    }

    [Fact]
    public async Task SuspendOwnerLots_ShouldSuspendAllNonSuspendedLots_AndSave()
    {
        var now = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(now);

        var lot1 = CreateLot(1, 10, "Bãi xe Vincom", ParkingLotStatus.Active);
        var lot2 = CreateLot(2, 10, "Bãi xe Bitexco", ParkingLotStatus.Suspended);
        var lot3 = CreateLot(3, 10, "Bãi xe Landmark", ParkingLotStatus.Active);

        var repo = new FakeLotManagementRepo([lot1, lot2, lot3]);
        var useCase = new SuspendOwnerLotsUseCase(repo, timeProvider, NullLogger<SuspendOwnerLotsUseCase>.Instance);

        var result = await useCase.ExecuteAsync(10);

        Assert.Equal(10, result.OwnerProfileId);
        Assert.Equal(2, result.TotalLotsSuspended); // lot1 và lot3
        Assert.Equal(now.UtcDateTime, result.SuspendedAtUtc);
        Assert.Contains(1, result.SuspendedLotIds);
        Assert.Contains(3, result.SuspendedLotIds);
        Assert.DoesNotContain(2, result.SuspendedLotIds);

        Assert.Equal(ParkingLotStatus.Suspended, lot1.Status);
        Assert.Equal(ParkingLotStatus.Suspended, lot2.Status);
        Assert.Equal(ParkingLotStatus.Suspended, lot3.Status);
        Assert.True(repo.SavedChanges);
    }

    [Fact]
    public async Task SuspendOwnerLots_WhenAllAlreadySuspended_ShouldNotSuspendAgain()
    {
        var now = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(now);

        var lot1 = CreateLot(1, 10, "Bãi xe Vincom", ParkingLotStatus.Suspended);
        var repo = new FakeLotManagementRepo([lot1]);
        var useCase = new SuspendOwnerLotsUseCase(repo, timeProvider, NullLogger<SuspendOwnerLotsUseCase>.Instance);

        var result = await useCase.ExecuteAsync(10);

        Assert.Equal(0, result.TotalLotsSuspended);
        Assert.Empty(result.SuspendedLotIds);
        Assert.False(repo.SavedChanges);
    }

    private sealed class FakeLotManagementRepo(IEnumerable<ParkingLot> initialLots) : IParkingLotManagementRepository
    {
        private readonly List<ParkingLot> _lots = [.. initialLots];
        public bool SavedChanges { get; private set; }

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
            _lots.Add(lot);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ParkingLot lot, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SavedChanges = true;
            return Task.CompletedTask;
        }
    }
}
