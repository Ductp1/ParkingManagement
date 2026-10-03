using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;
using VehicleService.Application.Features.Vehicles;

namespace VehicleService.Test;

public class VehicleTests
{
    [Theory] // TC-REG-04 / TC-REG-05
    [InlineData("51H-123.45", true)]
    [InlineData("30E-999.88", true)]
    [InlineData("51f 123.45", true)]
    [InlineData("999-ABCXYZ", false)]
    [InlineData("", false)]
    public void Plate_format_follows_circular_01_2021(string plate, bool valid)
        => Assert.Equal(valid, PlateNormalizer.IsValid(plate));

    [Fact]
    public void Plate_is_displayed_in_standard_format()
        => Assert.Equal("51F-123.45", PlateNormalizer.Format("51f12345"));

    [Fact]
    public async Task Lookup_normalizes_plate_before_querying()
    {
        var queries = new FakeQueries();
        await new FindVehicleByPlateUseCase(queries).ExecuteAsync("51f-123.45");
        Assert.Equal("51F12345", queries.LastPlate);
    }

    [Fact]
    public async Task Invalid_plate_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => new FindVehicleByPlateUseCase(new FakeQueries()).ExecuteAsync("999-ABCXYZ"));

    private sealed class FakeQueries : IVehicleQueries
    {
        public string? LastPlate { get; private set; }

        public Task<IReadOnlyList<VehicleDto>> ListByUserAsync(int userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<VehicleDto>>([]);

        public Task<VehicleDto?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken)
        {
            LastPlate = normalizedPlate;
            return Task.FromResult<VehicleDto?>(new VehicleDto(1, 5, normalizedPlate, "51F-123.45", "Sedan", "Gasoline", null, null, null, 147, true));
        }
    }
}
