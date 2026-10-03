using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ParkingManagement.IntegrationTests;

/// <summary>
/// Chạy service thật trong bộ nhớ (WebApplicationFactory) – đi qua đủ API → Application → Infrastructure → SQL Server.
/// Yêu cầu: SQL Server local đang chạy; lần chạy đầu service tự tạo database PM_*Db và nạp dữ liệu demo.
/// Dùng 1 controller của mỗi service làm "entry point" vì cả 9 project API đều có class Program.
/// </summary>
public class ParkingServiceApiTests(WebApplicationFactory<ParkingService.API.Controllers.ParkingLotsController> factory)
    : IClassFixture<WebApplicationFactory<ParkingService.API.Controllers.ParkingLotsController>>
{
    [Fact]
    public async Task Active_lot_returns_200_with_slot_counters()
    {
        var res = await factory.CreateClient().GetAsync("/api/parking-lots/1");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var lot = await res.Content.ReadFromJsonAsync<LotJson>();
        Assert.Equal("Bãi xe Vincom Đồng Khởi", lot!.Name);
        Assert.Equal(80, lot.TotalSlots);
    }

    [Fact] // bãi Landmark đang chờ duyệt KYB → tài xế không được xem
    public async Task Pending_lot_returns_404()
        => Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync("/api/parking-lots/3")).StatusCode);

    [Fact] // TC-SEARCH-01: tìm bãi trong bán kính 5 km quanh Quận 1
    public async Task Search_near_district_1_finds_vincom_first()
    {
        var items = await factory.CreateClient().GetFromJsonAsync<List<LotJson>>("/api/parking-lots/search?lat=10.777&lng=106.701&radiusKm=5");
        Assert.Equal(1, items![0].Id);
    }

    private sealed record LotJson(int Id, string Name, int TotalSlots);
}

public class BookingServiceApiTests(WebApplicationFactory<BookingService.API.Controllers.BookingsController> factory)
    : IClassFixture<WebApplicationFactory<BookingService.API.Controllers.BookingsController>>
{
    [Fact]
    public async Task Booking_detail_contains_locked_price_and_history()
    {
        var json = await factory.CreateClient().GetStringAsync("/api/bookings/BK-0002");
        Assert.Contains("\"promotionCode\":\"WELCOME10\"", json);
        Assert.Contains("\"priceSnapshot\"", json);
        Assert.Contains("\"history\"", json);
    }

    [Fact]
    public async Task Unknown_booking_returns_problem_details_404()
    {
        var res = await factory.CreateClient().GetAsync("/api/bookings/BK-9999");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Contains("Không tìm thấy", await res.Content.ReadAsStringAsync());
    }
}
