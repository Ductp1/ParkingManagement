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
    public async Task Suspended_lot_is_hidden_from_drivers()
    {
        var useCase = new GetParkingLotByIdUseCase(new FakeRepo([Lot(5, ParkingLotStatus.Suspended, 10.795, 106.72)]),
            TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<GetParkingLotByIdUseCase>.Instance);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(new GetParkingLotByIdQuery(5)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-99)]
    public async Task GetParkingLotById_WithNonPositiveId_ThrowsValidationException(int invalidId)
    {
        var useCase = new GetParkingLotByIdUseCase(new FakeRepo([]),
            TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<GetParkingLotByIdUseCase>.Instance);
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(new GetParkingLotByIdQuery(invalidId)));
    }

    [Fact]
    public async Task GetParkingLotById_WhenLotDoesNotExist_ThrowsNotFoundException()
    {
        var useCase = new GetParkingLotByIdUseCase(new FakeRepo([Lot(1, ParkingLotStatus.Active, 10.7781, 106.7019)]),
            TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<GetParkingLotByIdUseCase>.Instance);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(new GetParkingLotByIdQuery(999)));
    }

    [Fact] // US-018: Xem chi tiết bãi đỗ với valid ID (AC1, AC2, AC3)
    public async Task GetParkingLotById_WithValidId_ReturnsCompleteDetailsWithHotlineAndAmenities_WhenActive()
    {
        var lot = new ParkingLot(1, "Bãi xe Vincom Đồng Khởi", "72 Lê Thánh Tôn", 10.7781, 106.7019, 100, 42, 210,
            new TimeOnly(6, 0), new TimeOnly(23, 0), ParkingLotStatus.Active, 1, IntegrationTier.PmsApi,
            "Mô tả bãi Vincom", "02838000001");
        lot.Amenities.Add(new LotAmenity { ParkingLotId = 1, Code = "EV", Name = "Trụ sạc xe điện" });
        lot.Photos.Add(new LotPhoto { ParkingLotId = 1, Url = "https://example.com/photo2.jpg", SortOrder = 2 });
        lot.Photos.Add(new LotPhoto { ParkingLotId = 1, Url = "https://example.com/photo1.jpg", SortOrder = 1 });

        var useCase = new GetParkingLotByIdUseCase(new FakeRepo([lot]),
            TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<GetParkingLotByIdUseCase>.Instance);

        var dto = await useCase.ExecuteAsync(new GetParkingLotByIdQuery(1));

        Assert.Equal(1, dto.Id);
        Assert.Equal("Bãi xe Vincom Đồng Khởi", dto.Name);
        Assert.Equal("72 Lê Thánh Tôn", dto.Address);
        Assert.Equal(100, dto.TotalSlots);
        Assert.Equal(42, dto.AvailableSlots);
        Assert.Equal(58.0, dto.OccupancyRate);
        Assert.Equal("06:00 - 23:00", dto.OpeningHours);
        Assert.Equal("Active", dto.Status);
        Assert.Equal("02838000001", dto.HotlinePhone); // AC2: Hotline chung của bãi
        Assert.Equal("Mô tả bãi Vincom", dto.Description);
        Assert.True(dto.CanBook); // AC3: Bãi Active được phép đặt chỗ
        Assert.Contains("Trụ sạc xe điện", dto.Amenities!); // AC1: Tiện ích bãi
        Assert.Equal(["https://example.com/photo1.jpg", "https://example.com/photo2.jpg"], dto.Photos);
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
    [Fact] // US-014: AC1 & AC2 - Tra ve day du thong tin toa do, khoang cach, thoi gian du kien va tien ich
    public async Task Search_Returns_Complete_Details_For_Nearby_Active_Lot()
    {
        var lot = new ParkingLot(1, "Bai xe Vincom Dong Khoi", "72 Le Thanh Ton", 10.7781, 106.7019, 100, 42, 210,
            new TimeOnly(6, 0), new TimeOnly(23, 0), ParkingLotStatus.Active, 1, IntegrationTier.PmsApi,
            "Mo ta bai Vincom", "02838000001");
        lot.Amenities.Add(new LotAmenity { ParkingLotId = 1, Code = "EV", Name = "Tru sac xe dien" });

        var repo = new FakeRepo([lot]);
        var result = await new SearchParkingLotsUseCase(repo, TimeProvider.System)
            .ExecuteAsync(new SearchParkingLotsQuery(10.7770, 106.7010, RadiusKm: 5));

        Assert.Single(result);
        var item = result[0];
        Assert.Equal(1, item.Id);
        Assert.Equal("Bai xe Vincom Dong Khoi", item.Name);
        Assert.Equal("72 Le Thanh Ton", item.Address);
        Assert.Equal(10.7781, item.Latitude);
        Assert.Equal(106.7019, item.Longitude);
        Assert.InRange(item.DistanceKm, 0.1, 0.2);
        Assert.True(item.EstimatedTravelTimeMinutes >= 1);
        Assert.Equal(42, item.AvailableSlots);
        Assert.Equal(100, item.TotalSlots);
        Assert.Equal(210, item.MaxHeightCm);
        Assert.True(item.IsOpenNow);
        Assert.Contains("Tru sac xe dien", item.Amenities!);
    }

    [Fact] // US-014: AC4 - Khong co bai trong ban kinh tra ve danh sach rong
    public async Task Search_When_No_Lots_In_Radius_Returns_Empty_List()
    {
        var repo = new FakeRepo([Lot(2, ParkingLotStatus.Active, 10.8136, 106.6621)]);
        var result = await new SearchParkingLotsUseCase(repo, TimeProvider.System)
            .ExecuteAsync(new SearchParkingLotsQuery(10.7770, 106.7010, RadiusKm: 2));

        Assert.Empty(result);
    }

    [Theory] // US-014: Validation toa do va ban kinh
    [InlineData(95, 106, 5)]
     [InlineData(-95, 106, 5)]
    [InlineData(10, 185, 5)]
     [InlineData(10, -185, 5)]
    [InlineData(10, 106, 0)]
     [InlineData(10, 106, 55)]
    public async Task Search_With_Invalid_Params_Throws_ValidationException(double lat, double lng, double radiusKm)
    {
        var repo = new FakeRepo([]);
        var useCase = new SearchParkingLotsUseCase(repo, TimeProvider.System);

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new SearchParkingLotsQuery(lat, lng, radiusKm)));
    }


    [Theory] // US-015: TC-SEARCH-02 Hard Height Clearance - xe cao H cm thi tran phai >= H + 10 cm
    [InlineData(220, false)] // Ford Transit 220 cm + 10 margin = 230 > tran ham 210 -> Bi loai (HARD-BLOCK)
    [InlineData(201, false)] // 201 cm + 10 margin = 211 > tran ham 210 -> Bi loai
    [InlineData(200, true)]  // 200 cm + 10 margin = 210 <= tran ham 210 -> Xuat hien hop le
    [InlineData(145, true)]  // Sedan 145 cm + 10 margin = 155 <= tran ham 210 -> Xuat hien hop le
    public async Task Search_WithVehicleHeight_AppliesHardHeightClearanceRule(int vehicleHeightCm, bool shouldAppear)
    {
        var lot = Lot(1, ParkingLotStatus.Active, 10.7781, 106.7019, height: 210); // Tran ham 210 cm
        var repo = new FakeRepo([lot]);
        var result = await new SearchParkingLotsUseCase(repo, TimeProvider.System)
            .ExecuteAsync(new SearchParkingLotsQuery(10.7770, 106.7010, RadiusKm: 5, VehicleHeightCm: vehicleHeightCm));

        if (shouldAppear)
        {
            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
        }
        else
        {
            Assert.Empty(result);
        }
    }

    [Fact] // US-015: Top-N Limit - lay dung so luong limit toi da
    public async Task Search_Respects_Limit_Parameter()
    {
        var lots = new List<ParkingLot>
        {
            Lot(1, ParkingLotStatus.Active, 10.7781, 106.7019),
            Lot(2, ParkingLotStatus.Active, 10.7782, 106.7018),
            Lot(3, ParkingLotStatus.Active, 10.7783, 106.7017),
            Lot(4, ParkingLotStatus.Active, 10.7784, 106.7016),
            Lot(5, ParkingLotStatus.Active, 10.7785, 106.7015),
        };
        var repo = new FakeRepo(lots);
        var result = await new SearchParkingLotsUseCase(repo, TimeProvider.System)
            .ExecuteAsync(new SearchParkingLotsQuery(10.7770, 106.7010, RadiusKm: 5, Limit: 3));

        Assert.Equal(3, result.Count);
    }

    [Theory] // US-015: Validation cho chieu cao xe va limit
    [InlineData(40, 20)]   // Chieu cao < 50 cm
    [InlineData(550, 20)]  // Chieu cao > 500 cm
    [InlineData(150, 0)]   // Limit <= 0
    [InlineData(150, 105)] // Limit > 100
    public async Task Search_WithInvalidVehicleHeightOrLimit_ThrowsValidationException(int vehicleHeight, int limit)
    {
        var repo = new FakeRepo([]);
        var useCase = new SearchParkingLotsUseCase(repo, TimeProvider.System);

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new SearchParkingLotsQuery(10.7770, 106.7010, RadiusKm: 5, VehicleHeightCm: vehicleHeight, Limit: limit)));
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
