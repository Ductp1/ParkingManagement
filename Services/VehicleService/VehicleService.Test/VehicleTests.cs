using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;
using VehicleService.Application.DTOs;
using VehicleService.Application.Interfaces;
using VehicleService.Application.Services;
using VehicleService.Domain.Entities;

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
        var service = new VehicleAppService(queries);
        await service.FindVehicleByPlateAsync("51f-123.45");
        Assert.Equal("51F12345", queries.LastPlate);
    }

    [Fact]
    public async Task Invalid_plate_is_rejected()
    {
        var service = new VehicleAppService(new FakeQueries());
        await Assert.ThrowsAsync<ValidationException>(() => service.FindVehicleByPlateAsync("999-ABCXYZ"));
    }

    [Fact]
    public async Task CreateVehicle_InvalidPlate_ThrowsValidationException()
    {
        var service = new VehicleAppService(new FakeQueries());
        var request = new CreateVehicleRequestDto(5, "999-INVALID");
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateVehicleAsync(request));
    }

    [Fact]
    public async Task CreateVehicle_Exceeds10Vehicles_ThrowsValidationException()
    {
        var queries = new FakeQueries { VehicleCount = 10 };
        var service = new VehicleAppService(queries);
        var request = new CreateVehicleRequestDto(5, "51H-999.99");
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateVehicleAsync(request));
    }

    [Fact]
    public async Task CreateVehicle_DuplicatePlateInGarage_ThrowsValidationException()
    {
        var queries = new FakeQueries { IsDuplicate = true };
        var service = new VehicleAppService(queries);
        var request = new CreateVehicleRequestDto(5, "51H-123.45");
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateVehicleAsync(request));
    }

    [Fact]
    public async Task CreateVehicle_ValidRequest_CreatesSuccessfully()
    {
        var queries = new FakeQueries();
        var service = new VehicleAppService(queries);
        var request = new CreateVehicleRequestDto(5, "51H-123.45", VehicleType.Suv, FuelType.Electric, "VinFast", "VF 8", "Đen", 168, 475, 193, true);

        var result = await service.CreateVehicleAsync(request);

        Assert.Equal("51H12345", result.PlateNumber);
        Assert.Equal("51H-123.45", result.PlateDisplay);
        Assert.Equal(VehicleType.Suv, result.VehicleType);
        Assert.Equal(FuelType.Electric, result.FuelType);
        Assert.True(result.IsDefault);
        Assert.True(queries.ClearDefaultCalled);
    }

    [Fact]
    public async Task CreateVehicle_InvalidUserId_ThrowsValidationException()
    {
        var service = new VehicleAppService(new FakeQueries());
        var request = new CreateVehicleRequestDto(0, "51H-123.45");
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateVehicleAsync(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CreateVehicle_InvalidHeight_ThrowsValidationException(int invalidHeight)
    {
        var service = new VehicleAppService(new FakeQueries());
        var request = new CreateVehicleRequestDto(5, "51H-123.45", HeightCm: invalidHeight);
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateVehicleAsync(request));
    }

    [Fact]
    public async Task CreateVehicle_DefaultHeightCalculatedCorrectly()
    {
        var queries = new FakeQueries();
        var service = new VehicleAppService(queries);

        // Sedan default height: 145cm
        var sedanReq = new CreateVehicleRequestDto(5, "51H-111.11", VehicleType: VehicleType.Sedan, HeightCm: null);
        var sedanResult = await service.CreateVehicleAsync(sedanReq);
        Assert.Equal(145, sedanResult.HeightCm);

        // Suv default height: 170cm
        var suvReq = new CreateVehicleRequestDto(5, "51H-222.22", VehicleType: VehicleType.Suv, HeightCm: null);
        var suvResult = await service.CreateVehicleAsync(suvReq);
        Assert.Equal(170, suvResult.HeightCm);
    }

    [Fact]
    public async Task CreateVehicle_FirstVehicle_AutomaticallySetAsDefault()
    {
        var queries = new FakeQueries { VehicleCount = 0 };
        var service = new VehicleAppService(queries);
        var request = new CreateVehicleRequestDto(5, "51H-123.45", IsDefault: false);

        var result = await service.CreateVehicleAsync(request);

        Assert.True(result.IsDefault);
    }

    [Fact] // US-012: Khai báo xe điện EV/PHEV
    public async Task CreateVehicle_ElectricVehicle_Success()
    {
        var queries = new FakeQueries();
        var service = new VehicleAppService(queries);
        var request = new CreateVehicleRequestDto(5, "51H-888.88", VehicleType.Suv, FuelType.Electric, "VinFast", "VF 9", "Xanh", 172, 511, 200, false);

        var result = await service.CreateVehicleAsync(request);

        Assert.Equal(FuelType.Electric, result.FuelType);
        Assert.Equal(VehicleType.Suv, result.VehicleType);
        Assert.Equal("VF 9", result.Model);
        Assert.False(result.IsEmergencyVehicle);
    }

    [Fact] // US-012: Khai báo xe quá khổ kèm kích thước Dài/Rộng/Cao
    public async Task CreateVehicle_OversizedVehicle_WithDimensions_Success()
    {
        var queries = new FakeQueries();
        var service = new VehicleAppService(queries);
        var request = new CreateVehicleRequestDto(5, "51D-123.45", VehicleType.Oversized, FuelType.Diesel, "Ford", "Transit Limousine", "Đen", 230, 598, 206, false);

        var result = await service.CreateVehicleAsync(request);

        Assert.Equal(VehicleType.Oversized, result.VehicleType);
        Assert.Equal(230, result.HeightCm);
        Assert.Equal(598, result.LengthCm);
        Assert.Equal(206, result.WidthCm);
    }

    [Fact] // US-012 & Security: Xe tạo qua garage cá nhân luôn mặc định IsEmergencyVehicle = false
    public async Task CreateVehicle_EmergencyVehicle_DefaultsToFalse()
    {
        var queries = new FakeQueries();
        var service = new VehicleAppService(queries);
        var request = new CreateVehicleRequestDto(5, "51A-999.99", VehicleType.Sedan, FuelType.Gasoline, "Toyota", "Camry", "Trắng", 145, 488, 184, false);

        var result = await service.CreateVehicleAsync(request);

        Assert.False(result.IsEmergencyVehicle);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    public async Task SetDefaultVehicle_InvalidIds_ThrowsValidationException(int vehicleId, int userId)
    {
        var service = new VehicleAppService(new FakeQueries());
        await Assert.ThrowsAsync<ValidationException>(() => service.SetDefaultVehicleAsync(vehicleId, userId));
    }

    [Fact]
    public async Task SetDefaultVehicle_VehicleNotFound_ThrowsNotFoundException()
    {
        var queries = new FakeQueries { VehicleExists = false };
        var service = new VehicleAppService(queries);
        await Assert.ThrowsAsync<NotFoundException>(() => service.SetDefaultVehicleAsync(999, 5));
    }

    [Fact]
    public async Task SetDefaultVehicle_ValidVehicle_UpdatesSuccessfully()
    {
        var queries = new FakeQueries { VehicleExists = true };
        var service = new VehicleAppService(queries);

        var result = await service.SetDefaultVehicleAsync(10, 5);

        Assert.Equal(10, result.Id);
        Assert.Equal(5, result.UserId);
        Assert.True(result.IsDefault);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    public async Task UpdateVehicle_InvalidIds_ThrowsValidationException(int vehicleId, int userId)
    {
        var service = new VehicleAppService(new FakeQueries());
        var request = new UpdateVehicleRequestDto(userId, VehicleType.Sedan, FuelType.Gasoline);
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateVehicleAsync(vehicleId, request));
    }

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(-5, null, null)]
    [InlineData(null, 0, null)]
    [InlineData(null, -10, null)]
    [InlineData(null, null, 0)]
    [InlineData(null, null, -1)]
    public async Task UpdateVehicle_InvalidDimensions_ThrowsValidationException(int? height, int? length, int? width)
    {
        var service = new VehicleAppService(new FakeQueries());
        var request = new UpdateVehicleRequestDto(5, VehicleType.Sedan, FuelType.Gasoline, HeightCm: height, LengthCm: length, WidthCm: width);
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateVehicleAsync(1, request));
    }

    [Fact]
    public async Task UpdateVehicle_VehicleNotFound_ThrowsNotFoundException()
    {
        var queries = new FakeQueries { VehicleExists = false };
        var service = new VehicleAppService(queries);
        var request = new UpdateVehicleRequestDto(5, VehicleType.Sedan, FuelType.Gasoline);
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateVehicleAsync(999, request));
    }

    [Fact]
    public async Task UpdateVehicle_ValidRequest_UpdatesSuccessfully()
    {
        var queries = new FakeQueries { VehicleExists = true };
        var service = new VehicleAppService(queries);
        var request = new UpdateVehicleRequestDto(5, VehicleType.Suv, FuelType.Electric, "VinFast", "VF 8", "Đỏ", 168, 475, 193);

        var result = await service.UpdateVehicleAsync(10, request);

        Assert.Equal(10, result.Id);
        Assert.Equal(5, result.UserId);
        Assert.Equal(VehicleType.Suv, result.VehicleType);
        Assert.Equal(FuelType.Electric, result.FuelType);
        Assert.Equal("VinFast", result.Brand);
        Assert.Equal("VF 8", result.Model);
        Assert.Equal("Đỏ", result.Color);
        Assert.Equal(168, result.HeightCm);
        Assert.Equal("51F12345", result.PlateNumber); // Biển số bất biến, giữ nguyên!
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    public async Task DeleteVehicle_InvalidIds_ThrowsValidationException(int vehicleId, int userId)
    {
        var service = new VehicleAppService(new FakeQueries());
        await Assert.ThrowsAsync<ValidationException>(() => service.DeleteVehicleAsync(vehicleId, userId));
    }

    [Fact]
    public async Task DeleteVehicle_NotFound_ThrowsNotFoundException()
    {
        var queries = new FakeQueries { VehicleExists = false };
        var service = new VehicleAppService(queries);
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteVehicleAsync(999, 5));
    }

    [Fact]
    public async Task DeleteVehicle_HasActiveBooking_ThrowsConflictException()
    {
        var queries = new FakeQueries { VehicleExists = true };
        var bookingIntegration = new FakeBookingIntegrationService(hasActiveBooking: true);
        var service = new VehicleAppService(queries, bookingIntegration);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.DeleteVehicleAsync(10, 5));
        Assert.Contains("lịch đặt chỗ", ex.Message);
        Assert.False(queries.SoftDeleteCalled);
    }

    [Fact]
    public async Task DeleteVehicle_NoActiveBooking_SoftDeletesSuccessfully()
    {
        var queries = new FakeQueries { VehicleExists = true };
        var bookingIntegration = new FakeBookingIntegrationService(hasActiveBooking: false);
        var service = new VehicleAppService(queries, bookingIntegration);

        await service.DeleteVehicleAsync(10, 5);

        Assert.True(queries.SoftDeleteCalled);
    }

    private sealed class FakeBookingIntegrationService(bool hasActiveBooking) : IBookingIntegrationService
    {
        public Task<bool> HasActiveBookingAsync(int vehicleId, CancellationToken cancellationToken = default)
            => Task.FromResult(hasActiveBooking);
    }

    private sealed class FakeQueries : IVehicleQueries
    {
        public string? LastPlate { get; private set; }
        public int VehicleCount { get; set; } = 0;
        public bool IsDuplicate { get; set; } = false;
        public bool ClearDefaultCalled { get; private set; } = false;
        public bool VehicleExists { get; set; } = true;
        public bool SoftDeleteCalled { get; private set; } = false;

        public Task<IReadOnlyList<VehicleDto>> ListByUserAsync(int userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<VehicleDto>>([]);

        public Task<VehicleDto?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken)
        {
            LastPlate = normalizedPlate;
            return Task.FromResult<VehicleDto?>(new VehicleDto(1, 5, normalizedPlate, "51F-123.45", VehicleType.Sedan, FuelType.Gasoline, null, null, null, 147, true));
        }

        public Task<int> CountByUserAsync(int userId, CancellationToken cancellationToken)
            => Task.FromResult(VehicleCount);

        public Task<bool> ExistsPlateInGarageAsync(int userId, string normalizedPlate, CancellationToken cancellationToken)
            => Task.FromResult(IsDuplicate);

        public Task ClearDefaultForUserAsync(int userId, CancellationToken cancellationToken)
        {
            ClearDefaultCalled = true;
            return Task.CompletedTask;
        }

        public Task<VehicleDto> CreateAsync(Vehicle vehicle, CancellationToken cancellationToken)
        {
            return Task.FromResult(new VehicleDto(
                10,
                vehicle.UserId,
                vehicle.PlateNumber,
                vehicle.PlateDisplay,
                vehicle.VehicleType,
                vehicle.FuelType,
                vehicle.Brand,
                vehicle.Model,
                vehicle.Color,
                vehicle.HeightCm,
                vehicle.IsDefault,
                vehicle.LengthCm,
                vehicle.WidthCm,
                vehicle.IsEmergencyVehicle
            ));
        }

        public Task<VehicleDto?> SetDefaultAsync(int vehicleId, int userId, CancellationToken cancellationToken)
        {
            if (!VehicleExists)
                return Task.FromResult<VehicleDto?>(null);

            return Task.FromResult<VehicleDto?>(new VehicleDto(
                vehicleId,
                userId,
                "51F12345",
                "51F-123.45",
                VehicleType.Sedan,
                FuelType.Gasoline,
                "Toyota",
                "Vios",
                "Trắng",
                147,
                true
            ));
        }

        public Task<VehicleDto?> UpdateAsync(int vehicleId, UpdateVehicleRequestDto request, CancellationToken cancellationToken)
        {
            if (!VehicleExists)
                return Task.FromResult<VehicleDto?>(null);

            return Task.FromResult<VehicleDto?>(new VehicleDto(
                vehicleId,
                request.UserId,
                "51F12345", // Giữ nguyên biển số cũ
                "51F-123.45",
                request.VehicleType,
                request.FuelType,
                request.Brand,
                request.Model,
                request.Color,
                request.HeightCm ?? 150,
                false,
                request.LengthCm,
                request.WidthCm
            ));
        }

        public Task<bool> ExistsByIdAndUserAsync(int vehicleId, int userId, CancellationToken cancellationToken)
            => Task.FromResult(VehicleExists);

        public Task<bool> SoftDeleteAsync(int vehicleId, int userId, CancellationToken cancellationToken)
        {
            SoftDeleteCalled = true;
            return Task.FromResult(true);
        }
    }
}
