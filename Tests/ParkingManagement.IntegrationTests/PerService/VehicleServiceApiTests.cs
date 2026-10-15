using System.Net;
using System.Net.Http.Json;
using VehicleService.API.Controllers;

namespace ParkingManagement.IntegrationTests.PerService;

/// <summary>Collection: VehicleService chạy host thật trên database pm_it_vehicle_* riêng của từng lần chạy.</summary>
[CollectionDefinition("Vehicle API")]
public sealed class VehicleApiCollection : ICollectionFixture<ServiceHostFixture<VehiclesController>>;

/// <summary>
/// Integration test VehicleService (TV2): Garage xe (UC-07), thêm/sửa/xóa xe (US-009/010/011),
/// tra cứu theo biển số dùng cho cổng OCR, ràng buộc biển số Thông tư 01/2021/TT-BCA và soft delete.
/// </summary>
[Collection("Vehicle API")]
public class VehicleServiceApiTests(ServiceHostFixture<VehiclesController> fixture)
{
    [Fact] // Seed: driver 5 có 2 xe, Vios 51F-123.45 là xe mặc định.
    public async Task Garage_lists_seed_vehicles_with_default_flag()
    {
        var list = await fixture.Client.GetFromJsonAsync<List<VehicleJson>>("/api/v1/vehicles?userId=5");
        Assert.Equal(2, list!.Count);

        var vios = list.Single(v => v.Id == 1);
        Assert.Equal("51F-123.45", vios.PlateDisplay);
        Assert.Equal("51F12345", vios.PlateNumber);
        Assert.Equal("Sedan", vios.VehicleType);
        Assert.Equal("Toyota", vios.Brand);
        Assert.Equal(147, vios.HeightCm);
        Assert.True(vios.IsDefault);

        var cx5 = list.Single(v => v.Id == 2);
        Assert.Equal("30A-678.90", cx5.PlateDisplay);
        Assert.Equal("Suv", cx5.VehicleType);
        Assert.False(cx5.IsDefault);
    }

    [Fact] // Tra biển số cho cổng: chuẩn hóa được cả dạng không dấu chấm; sai định dạng → 400; không tồn tại → 404.
    public async Task By_plate_lookup_normalizes_and_formats()
    {
        var found = await fixture.Client.GetFromJsonAsync<VehicleJson>("/api/v1/vehicles/by-plate/51F-123.45");
        Assert.Equal(1, found!.Id);

        // Biển số không dấu chấm vẫn tra được nhờ PlateNormalizer
        var normalized = await fixture.Client.GetFromJsonAsync<VehicleJson>("/api/v1/vehicles/by-plate/51f12345");
        Assert.Equal(1, normalized!.Id);

        await Asserts.NotFoundAsync(await fixture.Client.GetAsync("/api/v1/vehicles/by-plate/59H-111.11"));
        await Asserts.BadRequestAsync(await fixture.Client.GetAsync("/api/v1/vehicles/by-plate/XX"),
            "Thông tư 01/2021/TT-BCA");
    }

    [Fact] // US-009/010: thêm xe thành công; trùng biển trong garage của chính mình → 400; sai định dạng → 400.
    public async Task Create_vehicle_persists_and_rejects_duplicates()
    {
        var create = await fixture.Client.PostAsJsonAsync("/api/v1/vehicles", new
        {
            userId = 5,
            plateNumber = "59A11111",
            vehicleType = "Suv",
            fuelType = "Diesel",
            brand = "Kia",
            model = "Sorento",
            color = "Trắng",
            heightCm = 170,
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<VehicleJson>();
        Assert.True(created!.Id > 3);
        Assert.Equal("59A-111.11", created.PlateDisplay);
        Assert.Equal("59A11111", created.PlateNumber);

        // Trùng biển trong garage của chính tài xế
        await Asserts.BadRequestAsync(
            await fixture.Client.PostAsJsonAsync("/api/v1/vehicles", new { userId = 5, plateNumber = "59A-111.11", vehicleType = "Sedan" }),
            "đã tồn tại trong Garage của bạn");

        // Biển số sai định dạng Thông tư 01/2021
        await Asserts.BadRequestAsync(
            await fixture.Client.PostAsJsonAsync("/api/v1/vehicles", new { userId = 5, plateNumber = "XX", vehicleType = "Sedan" }),
            "Thông tư 01/2021/TT-BCA");

        // Kích thước không hợp lệ
        await Asserts.BadRequestAsync(
            await fixture.Client.PostAsJsonAsync("/api/v1/vehicles", new { userId = 5, plateNumber = "60C33333", vehicleType = "Sedan", heightCm = 0 }),
            "Chiều cao xe phải lớn hơn 0");

        // Garage sau khi thêm: 3 xe cho driver 5
        var list = await fixture.Client.GetFromJsonAsync<List<VehicleJson>>("/api/v1/vehicles?userId=5");
        Assert.Equal(3, list!.Count);
    }

    [Fact] // US-010: đặt xe mặc định → cờ dời sang xe mới, garage chỉ còn đúng 1 xe mặc định.
    public async Task Set_default_moves_the_flag_to_the_new_vehicle()
    {
        var response = await fixture.Client.PatchAsync("/api/v1/vehicles/2/default?userId=5", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<VehicleJson>();
        Assert.True(updated!.IsDefault);

        var list = await fixture.Client.GetFromJsonAsync<List<VehicleJson>>("/api/v1/vehicles?userId=5");
        Assert.Single(list!, v => v.IsDefault);
        Assert.Equal(2, list!.Single(v => v.IsDefault).Id);
        Assert.False(list.Single(v => v.Id == 1).IsDefault);
    }

    [Fact] // US-011: cập nhật màu/size; biển số là bất biến nên response giữ nguyên biển cũ.
    public async Task Update_changes_details_but_keeps_the_plate_immutable()
    {
        var response = await fixture.Client.PutAsJsonAsync("/api/v1/vehicles/2", new
        {
            userId = 5,
            vehicleType = "Suv",
            fuelType = "Diesel",
            color = "Xanh đen",
            heightCm = 170,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<VehicleJson>();
        Assert.Equal("30A-678.90", updated!.PlateDisplay);
        Assert.Equal("Xanh đen", updated.Color);
        Assert.Equal("Diesel", updated.FuelType);

        await Asserts.NotFoundAsync(await fixture.Client.PutAsJsonAsync("/api/v1/vehicles/999", new
        {
            userId = 5,
            vehicleType = "Sedan",
            fuelType = "Gasoline",
        }));
    }

    [Fact] // US-011: xóa mềm → biến mất khỏi garage + tra biển 404; xóa lần 2 → 404.
    public async Task Delete_soft_removes_the_vehicle_from_garage_and_plate_lookup()
    {
        var create = await fixture.Client.PostAsJsonAsync("/api/v1/vehicles", new
        {
            userId = 5,
            plateNumber = "60B22222",
            vehicleType = "Sedan",
        });
        var created = await create.Content.ReadFromJsonAsync<VehicleJson>();

        Assert.Equal(HttpStatusCode.NoContent,
            (await fixture.Client.DeleteAsync($"/api/v1/vehicles/{created!.Id}?userId=5")).StatusCode);
        await Asserts.NotFoundAsync(await fixture.Client.GetAsync("/api/v1/vehicles/by-plate/60B-222.22"));
        await Asserts.NotFoundAsync(await fixture.Client.DeleteAsync($"/api/v1/vehicles/{created.Id}?userId=5"));

        var list = await fixture.Client.GetFromJsonAsync<List<VehicleJson>>("/api/v1/vehicles?userId=5");
        Assert.DoesNotContain(list!, v => v.Id == created.Id);
    }

    [Fact] // Validate đầu vào qua middleware chung → 400 ProblemDetails.
    public async Task Invalid_user_id_is_rejected_with_problem_details()
    {
        await Asserts.BadRequestAsync(await fixture.Client.GetAsync("/api/v1/vehicles?userId=0"),
            "userId phải là số nguyên dương");
    }

    public sealed record VehicleJson(int Id, int UserId, string PlateNumber, string PlateDisplay, string VehicleType,
        string FuelType, string? Brand, string? Model, string? Color, int HeightCm, bool IsDefault);
}
