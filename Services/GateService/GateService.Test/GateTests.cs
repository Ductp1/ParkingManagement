using GateService.Application.Features;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Test;

public class GateTests
{
    [Fact]
    public async Task Ocr_mistakes_are_corrected_before_lookup()
    {
        var queries = new FakeQueries();
        await new LookupVehicleAtGateUseCase(queries, TimeProvider.System).ExecuteAsync(2, "S1A-999.99"); // OCR đọc nhầm 5 thành S
        Assert.Equal("51A99999", queries.LastPlate);
    }

    [Fact]
    public async Task Unreadable_plate_asks_staff_to_type_manually()
        => await Assert.ThrowsAsync<ValidationException>(() =>
            new LookupVehicleAtGateUseCase(new FakeQueries(), TimeProvider.System).ExecuteAsync(2, "???"));

    [Fact]
    public async Task Lot_id_must_be_positive()
        => await Assert.ThrowsAsync<ValidationException>(() =>
            new ListLotSessionsUseCase(new FakeQueries(), TimeProvider.System).ExecuteAsync(0, null));

    private sealed class FakeQueries : IParkingSessionQueries
    {
        public string? LastPlate { get; private set; }

        public Task<IReadOnlyList<ParkingSessionDto>> ListByLotAsync(int parkingLotId, ParkingSessionStatus? status, DateTime nowUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ParkingSessionDto>>([]);

        public Task<ParkingSessionDto?> FindActiveByPlateAsync(int parkingLotId, string normalizedPlate, DateTime nowUtc, CancellationToken cancellationToken)
        {
            LastPlate = normalizedPlate;
            return Task.FromResult<ParkingSessionDto?>(null);
        }
    }
}
