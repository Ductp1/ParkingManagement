using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.ParkingLots;
using ParkingService.Domain.Entities;
using ParkingService.Domain.Services;

namespace ParkingService.Test;

public class ParkingLotTests
{
    private static ParkingLot Lot(int id, ParkingLotStatus status, double lat, double lng, int height = 210, int available = 10) =>
        new(id, $"Bãi {id}", "TP.HCM", lat, lng, 100, available, height, new TimeOnly(6, 0), new TimeOnly(23, 0), status, 1);

    [Fact]
    public void Haversine_ben_thanh_to_vincom_is_about_700m()
        => Assert.InRange(GeoDistance.HaversineKm(10.7725, 106.6980, 10.7781, 106.7019), 0.6, 0.8);

    [Theory] // TC-SEARCH-02: xe cao 2,2 m (Ford Transit) bị chặn ở hầm trần 2,1 m
    [InlineData(220, 210, false)]
    [InlineData(200, 210, true)]
    [InlineData(201, 210, false)]  // 201 + 10 cm margin > 210
    public void Height_clearance_rule(int vehicleHeight, int lotHeight, bool fits)
        => Assert.Equal(fits, GeoDistance.FitsHeight(vehicleHeight, lotHeight));

    [Fact]
    public void Open_overnight_lot_handles_midnight()
    {
        var night = new ParkingLot(1, "Bãi đêm", "Q.1", 10, 106, 10, 5, 300, new TimeOnly(18, 0), new TimeOnly(7, 0), ParkingLotStatus.Active);
        Assert.True(night.IsOpenAt(new TimeOnly(2, 0)));
        Assert.False(night.IsOpenAt(new TimeOnly(12, 0)));
    }

    [Fact]
    public async Task Pending_kyb_lot_is_hidden_from_drivers()
    {
        var useCase = new GetParkingLotByIdUseCase(new FakeRepo([Lot(3, ParkingLotStatus.PendingApproval, 10.795, 106.72)]),
            TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<GetParkingLotByIdUseCase>.Instance);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(new GetParkingLotByIdQuery(3)));
    }

    [Fact]
    public async Task Search_sorts_by_distance_and_drops_lots_outside_radius()
    {
        var repo = new FakeRepo(
        [
            Lot(1, ParkingLotStatus.Active, 10.7781, 106.7019),   // Vincom ~0,1 km
            Lot(2, ParkingLotStatus.Active, 10.8136, 106.6621),   // Sân bay ~5,7 km
            Lot(4, ParkingLotStatus.Active, 10.7725, 106.6980),   // Bến Thành ~0,7 km
        ]);
        var result = await new SearchParkingLotsUseCase(repo, TimeProvider.System)
            .ExecuteAsync(new SearchParkingLotsQuery(10.7770, 106.7010, RadiusKm: 5));

        Assert.Equal([1, 4], result.Select(r => r.Id));
    }

    private sealed class FakeRepo(List<ParkingLot> lots) : IParkingLotRepository
    {
        public Task<ParkingLot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(lots.FirstOrDefault(l => l.Id == id));

        public Task<IReadOnlyList<ParkingLot>> FindActiveInBoxAsync(double minLat, double maxLat, double minLng, double maxLng,
            int minHeightCm, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ParkingLot>>(lots.Where(l => l.Status == ParkingLotStatus.Active && l.MaxHeightCm >= minHeightCm).ToList());
    }
}
