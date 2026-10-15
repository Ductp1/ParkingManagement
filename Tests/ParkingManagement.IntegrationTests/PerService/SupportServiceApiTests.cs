using System.Net;
using System.Net.Http.Json;
using BookingService.API.Controllers;
using BookingService.Domain.Entities;
using BookingService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.IntegrationTests;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using SupportService.API.Controllers;
using SupportService.Application.Interfaces;
using SupportService.Infrastructure.Http;
using SupportService.Infrastructure.Persistence;

namespace ParkingManagement.IntegrationTests.PerService;

/// <summary>
/// Fixture SupportService: dựng 2 host — BookingService (nguồn dữ liệu booking thật, được khiếu nại tham chiếu
/// qua HTTP client "Services:BookingService") và SupportService. Arrange 1 booking đã hoàn thành + đã trả tiền
/// trong pm_booking để chạy được luồng tạo khiếu nại (booking phải đã diễn ra và đã thanh toán).
/// </summary>
public sealed class SupportApiFixture : IAsyncLifetime
{
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];

    public string BookingDbName { get; } = $"pm_it_booking_{Guid.NewGuid().ToString("N")[..8]}";
    public string SupportDbName { get; } = $"pm_it_support_{Guid.NewGuid().ToString("N")[..8]}";

    public WebApplicationFactory<BookingsController> BookingFactory { get; private set; } = null!;
    public WebApplicationFactory<ComplaintsController> SupportFactory { get; private set; } = null!;
    public HttpClient SupportClient { get; private set; } = null!;

    public IServiceProvider BookingServices => BookingFactory.Services;
    public IServiceProvider SupportServices => SupportFactory.Services;

    public async Task InitializeAsync()
    {
        var baseCs = TestDatabaseProvisioner.ResolveBaseConnectionString();
        await TestDatabaseProvisioner.CreateDatabaseAsync(baseCs, BookingDbName);
        await TestDatabaseProvisioner.CreateDatabaseAsync(baseCs, SupportDbName);

        // ----- 1. BookingService host (dependency thật của SupportService) -----
        BookingFactory = new WebApplicationFactory<BookingsController>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.UseIsolatedDatabase<BookingDbContext>(
                TestDatabaseProvisioner.ReplaceDatabase(baseCs, BookingDbName))));
        using var bookingHealth = await BookingFactory.CreateClient().GetAsync("/health");
        Assert.True(bookingHealth.IsSuccessStatusCode, $"BookingService không start được trên {BookingDbName}.");

        // ----- 2. Arrange: booking đã hoàn thành, đã trả 50.000đ, đã diễn ra từ hôm qua (đủ điều kiện gửi khiếu nại) -----
        await using (var scope = BookingFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            db.Bookings.Add(new Booking
            {
                Code = "BK-IT-DONE",
                UserId = DemoIds.Driver1User,
                VehicleId = DemoIds.Driver1Car,
                PlateNumber = "51F12345",
                VehicleType = VehicleType.Sedan,
                ParkingLotId = DemoIds.LotVincom,
                OwnerProfileId = DemoIds.OwnerVincom,
                ParkingLotName = "Bãi xe Vincom Đồng Khởi",
                StartAtUtc = yesterday.AddHours(1),
                EndAtUtc = yesterday.AddHours(3),
                Status = BookingStatus.Completed,
                TotalAmount = 50000,
                PaidAmount = 50000,
            });
            await db.SaveChangesAsync();
        }

        // ----- 3. SupportService host (BookingService URL trỏ sang host đã chạy; key này chỉ fixture Support dùng) -----
        // BaseAddress của IBookingLookup bị GHI ĐÈ bằng AddHttpClient (config action đăng ký sau luôn thắng).
        SupportFactory = new WebApplicationFactory<ComplaintsController>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Services:BookingService"] = BookingFactory.ClientOptions.BaseAddress.AbsoluteUri,
                }));
            builder.ConfigureServices(services =>
            {
                services.UseIsolatedDatabase<SupportDbContext>(
                    TestDatabaseProvisioner.ReplaceDatabase(baseCs, SupportDbName));
                services.AddHttpClient<IBookingLookup, BookingServiceClient>(client =>
                    client.BaseAddress = new Uri(BookingFactory.ClientOptions.BaseAddress.AbsoluteUri));
            });
        });
        SupportClient = SupportFactory.CreateClient();

        using var supportHealth = await SupportClient.GetAsync("/health");
        Assert.True(supportHealth.IsSuccessStatusCode, $"SupportService không start được trên {SupportDbName}.");
    }

    public async Task DisposeAsync()
    {
        BookingFactory?.Dispose();
        SupportFactory?.Dispose();
        try
        {
            var baseCs = TestDatabaseProvisioner.ResolveBaseConnectionString();
            await TestDatabaseProvisioner.DropDatabaseAsync(baseCs, BookingDbName);
            await TestDatabaseProvisioner.DropDatabaseAsync(baseCs, SupportDbName);
        }
        catch { // best-effort
        }
    }
}

/// <summary>Collection: SupportService (+ BookingService thật làm phụ thuộc) trên database pm_it_* riêng.</summary>
[CollectionDefinition("Support API")]
public sealed class SupportApiCollection : ICollectionFixture<SupportApiFixture>;

/// <summary>
/// Integration test SupportService (TV9): khiếu nại US-088 (tạo → tra cứu → my, validate theo dữ liệu booking thật),
/// đánh giá bãi UC-18, trung tâm trợ giúp FAQ US-092.
/// </summary>
[Collection("Support API")]
public class SupportServiceApiTests(SupportApiFixture fixture)
{
    [Fact] // US-088 AC1: tạo khiếu nại cho booking đã hoàn thành → 201, mã CP-*, trạng thái AwaitingOwner, có tin nhắn hệ thống.
    public async Task Create_complaint_for_a_completed_booking()
    {
        var create = await fixture.SupportClient.PostAsJsonAsync("/api/v1/complaints", new
        {
            userId = DemoIds.Driver1User,
            bookingCode = "BK-IT-DONE",
            category = "Overcharge",
            description = "Bị tính thừa 10 phút cuối, xin đối soát giờ ra bãi (integration test).",
            requestedAmount = 10000m,
            evidenceUrls = new[] { "https://photos.it.test/complaints/man-hinh.jpg" },
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var detail = await create.Content.ReadFromJsonAsync<ComplaintDetailJson>();
        Assert.StartsWith("CP-", detail!.Code);
        Assert.Equal("AwaitingOwner", detail.Status);
        Assert.Equal("Overcharge", detail.Category);
        Assert.Equal("BK-IT-DONE", detail.BookingCode);
        Assert.Equal(DemoIds.LotVincom, detail.ParkingLotId);
        Assert.Equal(10000m, detail.RequestedAmount);
        Assert.NotEmpty(detail.Messages);

        // Xuất hiện trong "khiếu nại của tôi"
        var mine = await fixture.SupportClient.GetFromJsonAsync<List<ComplaintSummaryJson>>("/api/v1/complaints/my?userId=5");
        Assert.Contains(mine!, c => c.Code == detail.Code);

        // Tra theo mã đúng user
        var byCode = await fixture.SupportClient.GetFromJsonAsync<ComplaintDetailJson>(
            $"/api/v1/complaints/{detail.Code}?userId=5");
        Assert.Equal(detail.Code, byCode!.Code);

        // User khác không xem được khiếu nại không thuộc về mình → 404
        await Asserts.NotFoundAsync(await fixture.SupportClient.GetAsync($"/api/v1/complaints/{detail.Code}?userId=6"));
    }

    [Fact] // Mỗi booking chỉ có 1 khiếu nại đang xử lý: tạo lần 2 → 409; BK-0001 đã có CP-0001 → 409.
    public async Task Duplicate_open_complaint_for_a_booking_conflicts()
    {
        await Asserts.ConflictAsync(await fixture.SupportClient.PostAsJsonAsync("/api/v1/complaints", new
        {
            userId = DemoIds.Driver1User,
            bookingCode = "BK-IT-DONE",
            category = "Overcharge",
            description = "Khiếu nại thứ hai cho cùng một booking phải bị chặn.",
        }), "đã có khiếu nại đang được xử lý");

        await Asserts.ConflictAsync(await fixture.SupportClient.PostAsJsonAsync("/api/v1/complaints", new
        {
            userId = DemoIds.Driver1User,
            bookingCode = "BK-0001",
            category = "Overcharge",
            description = "BK-0001 đã có CP-0001 đang chờ chủ bãi phản hồi.",
        }), "đã có khiếu nại đang được xử lý");
    }

    [Fact] // Validate theo dữ liệu booking thật: chưa diễn ra, vượt số tiền đã trả, booking lạ, mô tả quá ngắn.
    public async Task Complaint_rules_reject_invalid_requests()
    {
        // BK-0002 chưa diễn ra (bắt đầu ngày mai) → 400
        await Asserts.BadRequestAsync(await fixture.SupportClient.PostAsJsonAsync("/api/v1/complaints", new
        {
            userId = DemoIds.Driver1User,
            bookingCode = "BK-0002",
            category = "Overcharge",
            description = "Booking chưa diễn ra nên chưa thể có sự cố để khiếu nại.",
        }), "chưa diễn ra");

        // Booking lạ → 404
        await Asserts.NotFoundAsync(await fixture.SupportClient.PostAsJsonAsync("/api/v1/complaints", new
        {
            userId = DemoIds.Driver1User,
            bookingCode = "BK-9999",
            category = "Overcharge",
            description = "Booking này không tồn tại trong hệ thống.",
        }));

        // Số tiền yêu cầu vượt số tiền đã trả (50.000đ) → 400
        await Asserts.BadRequestAsync(await fixture.SupportClient.PostAsJsonAsync("/api/v1/complaints", new
        {
            userId = DemoIds.Driver1User,
            bookingCode = "BK-0001",
            category = "Overcharge",
            description = "Muốn đòi nhiều hơn số tiền đã thanh toán để kiểm tra validate.",
            requestedAmount = 999999m,
        }), "vượt quá số tiền đã thanh toán");

        // Mô tả quá ngắn → 400
        await Asserts.BadRequestAsync(await fixture.SupportClient.PostAsJsonAsync("/api/v1/complaints", new
        {
            userId = DemoIds.Driver1User,
            bookingCode = "BK-0001",
            category = "Other",
            description = "Ngắn",
        }), "Mô tả phải dài từ");
    }

    [Fact] // UC-18 + US-092: điểm đánh giá bãi từ seed (5★) và trung tâm trợ giúp FAQ có danh mục + liên hệ.
    public async Task Reviews_and_help_center_are_served_from_seed()
    {
        var reviews = await fixture.SupportClient.GetFromJsonAsync<LotReviewsJson>("/api/v1/reviews?parkingLotId=1");
        Assert.Equal(DemoIds.LotVincom, reviews!.ParkingLotId);
        Assert.Equal(5, reviews.AverageRating);
        Assert.Equal(1, reviews.ReviewCount);

        var help = await fixture.SupportClient.GetFromJsonAsync<HelpCenterJson>("/api/v1/faqs");
        Assert.NotEmpty(help!.Categories);
        Assert.All(help.Categories, c => Assert.NotEmpty(c.Items));

        // Khiếu nại của chủ bãi Vincom từ seed: CP-0001 đang chờ phản hồi
        var ownerBox = await fixture.SupportClient.GetFromJsonAsync<List<ComplaintSummaryJson>>(
            "/api/v1/complaints?ownerProfileId=1&status=AwaitingOwner");
        Assert.Contains(ownerBox!, c => c.Code == "CP-0001");

        // Chi tiết CP-0001: hội thoại ẩn ghi chú nội bộ của Admin
        var thread = await fixture.SupportClient.GetFromJsonAsync<ComplaintDetailJson>("/api/v1/complaints/CP-0001?userId=5");
        Assert.Equal("CP-0001", thread!.Code);
        Assert.DoesNotContain(thread.Messages, m => m.Message.Contains("Log camera")); // tin nhắn IsInternal=true bị ẩn
    }

    public sealed record LotReviewsJson(int ParkingLotId, double AverageRating, int ReviewCount,
        IReadOnlyDictionary<int, int> StarBreakdown);

    public sealed record HelpCenterJson(string Audience, IReadOnlyList<CategoryJson> Categories,
        IReadOnlyList<ContactJson> Contacts);

    public sealed record CategoryJson(string Category, IReadOnlyList<FaqItemJson> Items);

    public sealed record FaqItemJson(int Id, string Category, string Audience, string Question, string Answer,
        int SortOrder, int ViewCount);

    public sealed record ContactJson(string Channel, string Value, string? Note);
}

public sealed record ComplaintDetailJson(int Id, string Code, string Status, string Category, string Priority,
    string AssignedTeam, string? BookingCode, int ParkingLotId, string Description, IReadOnlyList<string> EvidenceUrls,
    decimal? RequestedAmount, DateTime? OwnerResponseDueAtUtc, string? OwnerResponse, string? Resolution,
    decimal? ResolvedRefundAmount, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, IReadOnlyList<MessageJson> Messages);

public sealed record MessageJson(int Id, string SenderParty, string Message, IReadOnlyList<string> AttachmentUrls, DateTime CreatedAtUtc);

public sealed record ComplaintSummaryJson(int Id, string Code, string Status, string Category, string Priority,
    string? BookingCode, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);
