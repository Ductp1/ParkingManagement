using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using BookingService.API.Controllers;
using BookingService.Domain.Entities;
using BookingService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ParkingManagement.IntegrationTests;
using Microsoft.AspNetCore.TestHost;
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

    /// <summary>Cặp khóa RSA test: SupportService kiểm tra JWT bằng public key này, test mint token bằng private key.</summary>
    public RsaTestKeys Keys { get; } = RsaTestKeys.Create();

    /// <summary>Client gắn sẵn JWT của <paramref name="userId"/> với các role cho trước (API có [Authorize]).</summary>
    public HttpClient CreateAuthenticatedClient(int userId, params string[] roles)
    {
        var client = SupportFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Keys.MintToken(userId, $"user{userId}@it.test", null, roles));
        return client;
    }

    /// <summary>Client của chủ bãi: token có role LotOwner + claim OwnerProfileId (giống token UserService phát hành).</summary>
    public HttpClient CreateOwnerClient(int ownerUserId, int ownerProfileId)
    {
        var client = SupportFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Keys.MintToken(ownerUserId, $"owner{ownerUserId}@it.test", null,
            [new System.Security.Claims.Claim("OwnerProfileId", ownerProfileId.ToString())], "LotOwner"));
        return client;
    }

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
            // US-093: booking đã hoàn thành ở bãi TSN để test đánh giá – khác bãi Vincom nên không đổi điểm trung bình seed của bãi 1.
            db.Bookings.Add(new Booking
            {
                Code = "BK-IT-REVIEW",
                UserId = DemoIds.Driver1User,
                VehicleId = DemoIds.Driver1Car,
                PlateNumber = "51F12345",
                VehicleType = VehicleType.Sedan,
                ParkingLotId = DemoIds.LotTsn,
                OwnerProfileId = DemoIds.OwnerTsn,
                ParkingLotName = "Bãi xe sân bay Tân Sơn Nhất",
                StartAtUtc = yesterday.AddHours(5),
                EndAtUtc = yesterday.AddHours(7),
                Status = BookingStatus.Completed,
                TotalAmount = 40000,
                PaidAmount = 40000,
            });
            // US-091: 2 booking Confirmed ở bãi Vincom bắt đầu sau 10 phút (tài xế đã đến bãi) – 1 nhận chuyển bãi, 1 từ chối.
            foreach (var code in new[] { "BK-IT-FULL-MOVE", "BK-IT-FULL-REFUND" })
            {
                db.Bookings.Add(new Booking
                {
                    Code = code,
                    UserId = DemoIds.Driver1User,
                    VehicleId = DemoIds.Driver1Car,
                    PlateNumber = "51F12345",
                    VehicleType = VehicleType.Sedan,
                    ParkingLotId = DemoIds.LotVincom,
                    OwnerProfileId = DemoIds.OwnerVincom,
                    ParkingLotName = "Bãi xe Vincom Đồng Khởi",
                    StartAtUtc = DateTime.UtcNow.AddMinutes(10),
                    EndAtUtc = DateTime.UtcNow.AddHours(3),
                    Status = BookingStatus.Confirmed,
                    TotalAmount = 60000,
                    PaidAmount = 60000,
                });
            }
            await db.SaveChangesAsync();
        }

        // ----- 3. SupportService host -----
        // Client IBookingLookup gọi THẲNG vào pipeline in-memory của host BookingService:
        // WebApplicationFactory KHÔNG mở cổng mạng thật → bắt buộc dùng TestServer.CreateHandler().
        var bookingServerHandler = BookingFactory.Server.CreateHandler();
        SupportFactory = new WebApplicationFactory<ComplaintsController>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.UseIsolatedDatabase<SupportDbContext>(
                    TestDatabaseProvisioner.ReplaceDatabase(baseCs, SupportDbName));
                services.AddScoped<IBookingLookup>(sp =>
                {
                    var http = new HttpClient(bookingServerHandler)
                    {
                        BaseAddress = BookingFactory.ClientOptions.BaseAddress,
                    };
                    return new BookingServiceClient(http, sp.GetRequiredService<ILogger<BookingServiceClient>>());
                });
                // US-094: fixture không dựng ParkingService → tra bãi của chủ bãi bằng dữ liệu cố định.
                services.AddScoped<IOwnerLotLookup, StubOwnerLots>();
                // US-091: fixture không dựng ParkingService → bãi thay thế quanh Vincom là dữ liệu cố định.
                services.AddScoped<IParkingLotSearch, StubParkingLotSearch>();
                // US-090: lịch sử booking lấy từ BookingService thật; fixture không dựng GateService → log check-in/out cố định.
                services.AddScoped<IBookingHistoryLookup>(sp => new BookingHistoryClient(
                    new HttpClient(bookingServerHandler) { BaseAddress = BookingFactory.ClientOptions.BaseAddress },
                    sp.GetRequiredService<ILogger<BookingHistoryClient>>()));
                services.AddScoped<IGateSessionLookup, StubGateSessions>();
            }).ConfigureTestServices(services =>
                // Program.cs nạp Keys/public.key thật; test thay bằng public key của cặp khóa test để kiểm tra token tự mint.
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    using var rsa = RSA.Create();
                    rsa.ImportFromPem(File.ReadAllText(Keys.PublicKeyPath));
                    options.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false));
                })));
        SupportClient = SupportFactory.CreateClient();

        using var supportHealth = await SupportClient.GetAsync("/health");
        Assert.True(supportHealth.IsSuccessStatusCode, $"SupportService không start được trên {SupportDbName}.");

        // ----- 4. Arrange US-089: 2 tranh chấp của chủ bãi TSN đang giữ tiền – 1 còn hạn 48h, 1 đã quá hạn -----
        // (BookingService chưa trả OwnerProfileId nên chưa tạo được ticket AwaitingOwner qua API → ghi thẳng vào pm_it_support.)
        await using (var scope = SupportFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            db.Complaints.AddRange(
                OwnerDispute("CP-IT-OWNER", DateTime.UtcNow.AddHours(47), 20000m),
                OwnerDispute("CP-IT-LATE", DateTime.UtcNow.AddHours(-1), 15000m));
            await db.SaveChangesAsync();
        }
    }

    private static SupportService.Domain.Entities.Complaint OwnerDispute(string code, DateTime dueAtUtc, decimal amount) => new()
    {
        Code = code,
        UserId = DemoIds.Driver1User,
        BookingCode = "BK-IT-REVIEW",
        ParkingLotId = DemoIds.LotTsn,
        OwnerProfileId = DemoIds.OwnerTsn,
        Category = ComplaintCategory.Overcharge,
        Description = "Bị tính thừa block cuối (integration test US-089).",
        RequestedAmount = amount,
        Status = ComplaintStatus.AwaitingOwner,
        OwnerResponseDueAtUtc = dueAtUtc,
        PayoutHeld = true,
    };

    public async Task DisposeAsync()
    {
        BookingFactory?.Dispose();
        SupportFactory?.Dispose();
        Keys.Dispose();
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

/// <summary>US-094: bãi của từng chủ bãi (thay cho ParkingService trong test) – TSN quản lý bãi TSN và Bến Thành.</summary>
public sealed class StubOwnerLots : IOwnerLotLookup
{
    public Task<IReadOnlyCollection<int>> GetLotIdsAsync(int ownerProfileId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<int>>(ownerProfileId switch
        {
            DemoIds.OwnerVincom => [DemoIds.LotVincom],
            DemoIds.OwnerTsn => [DemoIds.LotTsn, DemoIds.LotBenThanh],
            _ => [],
        });
}

/// <summary>US-091: ParkingService giả – quanh Vincom có Bến Thành (0,6 km, còn chỗ) và 1 bãi ngoài bán kính 1 km.</summary>
public sealed class StubParkingLotSearch : IParkingLotSearch
{
    public Task<LotLocation?> GetLotAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => Task.FromResult<LotLocation?>(new LotLocation(parkingLotId, "Bãi xe Vincom Đồng Khởi", 10.7781, 106.7019, 0));

    public Task<IReadOnlyList<SupportService.Application.DTOs.AlternativeLotDto>> SearchAvailableAsync(double latitude, double longitude,
        double radiusKm, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SupportService.Application.DTOs.AlternativeLotDto>>(
        [
            new(DemoIds.LotBenThanh, "Bãi xe chợ Bến Thành", "Lê Lợi, Q.1", 0.6, 15, true),
            new(DemoIds.LotLandmark, "Bãi xe Landmark 81", "Bình Thạnh", 4.2, 30, true),
        ]);
}

/// <summary>US-090: GateService giả – booking BK-IT-DONE có 1 lượt vào/ra nhận diện bằng camera LPR.</summary>
public sealed class StubGateSessions : IGateSessionLookup
{
    public Task<IReadOnlyList<SupportService.Application.DTOs.GateSessionEvidenceDto>> ListForBookingAsync(int parkingLotId,
        string bookingCode, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SupportService.Application.DTOs.GateSessionEvidenceDto>>(bookingCode == "BK-IT-DONE"
            ? [new("PS-IT-DONE", "Completed", "51F-123.45", DateTime.UtcNow.Date.AddDays(-1).AddHours(1),
                DateTime.UtcNow.Date.AddDays(-1).AddHours(3), 120, "Lpr", 0.97, 50000m)]
            : []);
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
    [Fact] // US-088 AC1: tạo khiếu nại cho booking đã hoàn thành → 201, mã CP-*, trạng thái Open, có tin nhắn hệ thống.
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
        Assert.Equal("Open", detail.Status); // ticket mới mở, sau khi chủ bãi phản hồi mới chuyển AwaitingOwner/Resolved
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

    [Fact] // US-093: đánh giá bãi với userId lấy từ JWT – không token 401, sai role 403, đúng chủ → 201, lần 2 → 409.
    public async Task Review_after_completed_booking_uses_user_from_token()
    {
        const string eligibilityUrl = "/api/v1/reviews/bookings/BK-IT-REVIEW/eligibility";
        var review = new { bookingCode = "BK-IT-REVIEW", rating = 4, comment = "Bãi sân bay rộng, dễ tìm chỗ (integration test).", reviewerName = "Tài xế IT" };

        // Không đăng nhập → 401 (client cũ không có header Authorization)
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.SupportClient.GetAsync(eligibilityUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.SupportClient.PostAsJsonAsync("/api/v1/reviews", review)).StatusCode);

        // Đăng nhập nhưng không phải tài xế → 403
        var owner = fixture.CreateAuthenticatedClient(DemoIds.OwnerTsnUser, "LotOwner");
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/v1/reviews", review)).StatusCode);

        // Tài xế khác (Driver2) dò booking của Driver1 → 404, không lộ trạng thái booking (IDOR)
        var otherDriver = fixture.CreateAuthenticatedClient(DemoIds.Driver2User, "Driver");
        await Asserts.NotFoundAsync(await otherDriver.GetAsync(eligibilityUrl));
        await Asserts.NotFoundAsync(await otherDriver.PostAsJsonAsync("/api/v1/reviews", review));

        // Chủ booking: được đánh giá → gửi → không được đánh giá lần nữa
        var driver = fixture.CreateAuthenticatedClient(DemoIds.Driver1User, "Driver");
        var before = await driver.GetFromJsonAsync<EligibilityJson>(eligibilityUrl);
        Assert.True(before!.CanReview);

        var create = await driver.PostAsJsonAsync("/api/v1/reviews", review);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ReviewJson>();
        Assert.Equal(DemoIds.LotTsn, created!.ParkingLotId);
        Assert.Equal(4, created.Rating);

        var after = await driver.GetFromJsonAsync<EligibilityJson>(eligibilityUrl);
        Assert.False(after!.CanReview);
        Assert.Equal(created.Id, after.ReviewId);

        await Asserts.ConflictAsync(await driver.PostAsJsonAsync("/api/v1/reviews", review), "đã được đánh giá");

        // AC3: điểm của bãi TSN có đánh giá mới (API công khai, không cần đăng nhập)
        var lot = await fixture.SupportClient.GetFromJsonAsync<LotReviewsJson>($"/api/v1/reviews?parkingLotId={DemoIds.LotTsn}");
        Assert.Equal(1, lot!.ReviewCount);
        Assert.Equal(4, lot.AverageRating);
    }

    [Fact] // US-089: chủ bãi phản hồi tranh chấp bằng JWT; quá 48h → Escalated; tiền tranh chấp bị phong tỏa.
    public async Task Owner_responds_to_dispute_within_48_hours_and_overdue_is_escalated()
    {
        const string url = "/api/v1/complaints/CP-IT-OWNER";
        var reply = new
        {
            response = "Log cổng cho thấy xe ra lúc 11:52, đúng giờ tính phí; đính kèm log và ảnh camera.",
            evidenceUrls = new[] { "https://evidence.it.test/gate-log.pdf", "https://evidence.it.test/camera-1152.jpg" },
        };

        // Không đăng nhập → 401; tài xế → 403; chủ bãi khác (Vincom) → 404 (không lộ tranh chấp của bãi khác)
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.SupportClient.GetAsync($"{url}/owner-view")).StatusCode);
        var driver = fixture.CreateAuthenticatedClient(DemoIds.Driver1User, "Driver");
        Assert.Equal(HttpStatusCode.Forbidden, (await driver.PostAsJsonAsync($"{url}/owner-response", reply)).StatusCode);
        var otherOwner = fixture.CreateOwnerClient(DemoIds.OwnerVincomUser, DemoIds.OwnerVincom);
        await Asserts.NotFoundAsync(await otherOwner.GetAsync($"{url}/owner-view"));
        await Asserts.NotFoundAsync(await otherOwner.PostAsJsonAsync($"{url}/owner-response", reply));

        var owner = fixture.CreateOwnerClient(DemoIds.OwnerTsnUser, DemoIds.OwnerTsn);

        // AC1: xem tranh chấp còn hạn, đang giữ tiền
        var view = await owner.GetFromJsonAsync<OwnerComplaintJson>($"{url}/owner-view");
        Assert.Equal("AwaitingOwner", view!.Status);
        Assert.True(view.PayoutHeld);

        // AC3: khoản tiền bị phong tỏa của chủ bãi TSN (lấy từ token, không truyền ownerProfileId)
        var holds = await owner.GetFromJsonAsync<List<PayoutHoldJson>>("/api/v1/complaints/payout-holds");
        Assert.Contains(holds!, h => h.ComplaintCode == "CP-IT-OWNER" && h.Amount == 20000m);
        Assert.All(holds!, h => Assert.Equal(DemoIds.OwnerTsn, h.OwnerProfileId));

        // AC1: phản hồi quá ngắn → 400; phản hồi hợp lệ → OwnerResponded + tin nhắn của chủ bãi; lần 2 → 409
        await Asserts.BadRequestAsync(await owner.PostAsJsonAsync($"{url}/owner-response", new { response = "Sai" }), "Nội dung phản hồi");
        var responded = await owner.PostAsJsonAsync($"{url}/owner-response", reply);
        Assert.Equal(HttpStatusCode.OK, responded.StatusCode);
        var afterReply = await responded.Content.ReadFromJsonAsync<OwnerComplaintJson>();
        Assert.Equal("OwnerResponded", afterReply!.Status);
        Assert.NotNull(afterReply.OwnerRespondedAtUtc);
        Assert.True(afterReply.PayoutHeld);   // vẫn giữ tiền đến khi Admin phán quyết
        Assert.Contains(afterReply.Messages, m => m.SenderParty == "LotOwner" && m.AttachmentUrls.Count == 2);
        await Asserts.ConflictAsync(await owner.PostAsJsonAsync($"{url}/owner-response", reply), "đã phản hồi");

        // Tài xế thấy phản hồi của chủ bãi trong ticket của mình
        var driverView = await fixture.SupportClient.GetFromJsonAsync<ComplaintDetailJson>($"{url}?userId={DemoIds.Driver1User}");
        Assert.Equal("OwnerResponded", driverView!.Status);

        // AC2: quá hạn 48h → phản hồi bị từ chối; lượt escalate chuyển Escalated, giao Supervisor
        await Asserts.ConflictAsync(await owner.PostAsJsonAsync("/api/v1/complaints/CP-IT-LATE/owner-response", reply), "quá hạn");
        await using (var scope = fixture.SupportFactory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IComplaintEscalationService>().EscalateOverdueAsync();
        var late = await owner.GetFromJsonAsync<OwnerComplaintJson>("/api/v1/complaints/CP-IT-LATE/owner-view");
        Assert.Equal("Escalated", late!.Status);
        Assert.Equal("Supervisor", late.AssignedTeam);
        Assert.NotNull(late.EscalatedAtUtc);
        Assert.Contains(late.Messages, m => m.SenderParty == "System");

        // Sự kiện đã vào Outbox để báo tài xế / Admin
        await using (var scope = fixture.SupportFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var types = await db.OutboxMessages.Select(o => o.EventType).ToListAsync();
            Assert.Contains("ComplaintOwnerResponded", types);
            Assert.Contains("ComplaintEscalated", types);
        }
    }

    /// <summary>Tạo 1 tranh chấp riêng cho test (hạn còn 47 giờ để worker/test khác không đụng tới), trả Id.</summary>
    private async Task<int> ArrangeDisputeAsync(string code)
    {
        await using var scope = fixture.SupportFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        var complaint = new SupportService.Domain.Entities.Complaint
        {
            Code = code, UserId = DemoIds.Driver1User, ParkingLotId = DemoIds.LotTsn, OwnerProfileId = DemoIds.OwnerTsn,
            Category = ComplaintCategory.Overcharge, Description = "Tranh chấp cho test concurrency (US-089 review).",
            RequestedAmount = 5000m, Status = ComplaintStatus.AwaitingOwner,
            OwnerResponseDueAtUtc = DateTime.UtcNow.AddHours(47), PayoutHeld = true,
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();
        return complaint.Id;
    }

    private async Task<List<string>> OutboxEventsForAsync(string code)
    {
        await using var scope = fixture.SupportFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        var payloads = await db.OutboxMessages.Select(o => new { o.EventType, o.PayloadJson }).ToListAsync();
        // jsonb chuẩn hóa lại định dạng ("Code": "...") nên đọc bằng JSON parser thay vì so chuỗi.
        return payloads
            .Where(p => System.Text.Json.JsonDocument.Parse(p.PayloadJson).RootElement.TryGetProperty("Code", out var c) && c.GetString() == code)
            .Select(p => p.EventType).ToList();
    }

    [Fact] // Review: worker đã nạp ticket nhưng chủ bãi phản hồi trước → lần lưu Escalated của worker bị RowVersion chặn.
    public async Task Stale_escalation_cannot_overwrite_owner_response()
    {
        var id = await ArrangeDisputeAsync("CP-IT-RACE");
        await using var workerScope = fixture.SupportFactory.Services.CreateAsyncScope();
        await using var ownerScope = fixture.SupportFactory.Services.CreateAsyncScope();
        var workerRepo = workerScope.ServiceProvider.GetRequiredService<IComplaintRepository>();
        var ownerRepo = ownerScope.ServiceProvider.GetRequiredService<IComplaintRepository>();

        var staleForWorker = await workerRepo.FindByIdAsync(id);            // worker nạp trước
        var forOwner = await ownerRepo.FindByIdAsync(id);

        forOwner!.Status = ComplaintStatus.OwnerResponded;                   // chủ bãi lưu trước → thắng
        forOwner.OwnerResponse = "Phản hồi của chủ bãi trước khi worker kịp lưu.";
        await ownerRepo.UpdateAsync(forOwner, new ComplaintOwnerResponded(id, "CP-IT-RACE", DemoIds.Driver1User, DemoIds.LotTsn));

        staleForWorker!.Status = ComplaintStatus.Escalated;                  // worker lưu bằng bản cũ → bị từ chối
        await Assert.ThrowsAsync<ParkingManagement.SharedKernel.Exceptions.ConflictException>(() => workerRepo.UpdateAsync(staleForWorker,
            new ComplaintEscalated(id, "CP-IT-RACE", DemoIds.Driver1User, DemoIds.LotTsn, DemoIds.OwnerTsn, "test")));

        var owner = fixture.CreateOwnerClient(DemoIds.OwnerTsnUser, DemoIds.OwnerTsn);
        var current = await owner.GetFromJsonAsync<OwnerComplaintJson>("/api/v1/complaints/CP-IT-RACE/owner-view");
        Assert.Equal("OwnerResponded", current!.Status);                     // không bị ghi đè
        Assert.Equal(["ComplaintOwnerResponded"], await OutboxEventsForAsync("CP-IT-RACE"));   // không có sự kiện Escalated
    }

    [Fact] // Review: 2 instance cùng escalate 1 ticket → chỉ 1 lần thành công, chỉ 1 sự kiện.
    public async Task Two_instances_escalating_the_same_ticket_emit_one_event()
    {
        var id = await ArrangeDisputeAsync("CP-IT-DUP");
        await using var scopeA = fixture.SupportFactory.Services.CreateAsyncScope();
        await using var scopeB = fixture.SupportFactory.Services.CreateAsyncScope();
        var repoA = scopeA.ServiceProvider.GetRequiredService<IComplaintRepository>();
        var repoB = scopeB.ServiceProvider.GetRequiredService<IComplaintRepository>();
        var a = await repoA.FindByIdAsync(id);
        var b = await repoB.FindByIdAsync(id);

        a!.Status = ComplaintStatus.Escalated;
        await repoA.UpdateAsync(a, new ComplaintEscalated(id, "CP-IT-DUP", DemoIds.Driver1User, DemoIds.LotTsn, DemoIds.OwnerTsn, "A"));
        b!.Status = ComplaintStatus.Escalated;
        await Assert.ThrowsAsync<ParkingManagement.SharedKernel.Exceptions.ConflictException>(() =>
            repoB.UpdateAsync(b, new ComplaintEscalated(id, "CP-IT-DUP", DemoIds.Driver1User, DemoIds.LotTsn, DemoIds.OwnerTsn, "B")));

        Assert.Equal(["ComplaintEscalated"], await OutboxEventsForAsync("CP-IT-DUP"));
    }

    [Fact] // Review: worker chạy trên nhiều instance – instance đang giữ advisory lock thì instance khác bỏ qua lượt.
    public async Task Escalation_worker_skips_run_while_another_instance_holds_the_lock()
    {
        var worker = fixture.SupportFactory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<SupportService.Infrastructure.Workers.ComplaintEscalationWorker>().Single();

        await using var otherInstance = fixture.SupportFactory.Services.CreateAsyncScope();
        var db = otherInstance.ServiceProvider.GetRequiredService<SupportDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_lock({SupportService.Infrastructure.Workers.ComplaintEscalationWorker.AdvisoryLockKey})");
        try
        {
            Assert.False(await worker.RunOnceAsync());                       // instance khác đang chạy → bỏ qua
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({SupportService.Infrastructure.Workers.ComplaintEscalationWorker.AdvisoryLockKey})");
            await db.Database.CloseConnectionAsync();
        }
        Assert.True(await worker.RunOnceAsync());                            // khóa đã nhả → chạy được
    }

    [Fact] // Review: DB chặn giữ tiền khi chưa biết chủ bãi, và ticket đã đóng mà vẫn giữ tiền.
    public async Task Database_rejects_invalid_payout_holds()
    {
        static SupportService.Domain.Entities.Complaint Invalid(string code, int? owner, ComplaintStatus status) => new()
        {
            Code = code, UserId = DemoIds.Driver1User, ParkingLotId = DemoIds.LotTsn, OwnerProfileId = owner,
            Category = ComplaintCategory.Overcharge, Description = "Dữ liệu sai cố ý để kiểm tra CHECK constraint.",
            RequestedAmount = 5000m, Status = status, PayoutHeld = true,
        };

        foreach (var invalid in new[] { Invalid("CP-IT-NOOWNER", null, ComplaintStatus.Open), Invalid("CP-IT-CLOSED", DemoIds.OwnerTsn, ComplaintStatus.Resolved) })
        {
            await using var scope = fixture.SupportFactory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            db.Complaints.Add(invalid);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains("CK_Complaints_PayoutHold", ex.InnerException!.Message);
        }

        // Đóng tranh chấp qua Complaint.Close → giải phóng khoản giữ → DB chấp nhận, không còn trong payout-holds.
        var id = await ArrangeDisputeAsync("CP-IT-CLOSE");
        await using (var scope = fixture.SupportFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var complaint = await db.Complaints.SingleAsync(c => c.Id == id);
            complaint.Close(ComplaintStatus.Resolved, "Hoàn 5.000đ cho tài xế.", 5000m, DemoIds.AdminUser, DateTime.UtcNow);
            await db.SaveChangesAsync();
        }
        var holds = await fixture.CreateOwnerClient(DemoIds.OwnerTsnUser, DemoIds.OwnerTsn)
            .GetFromJsonAsync<List<PayoutHoldJson>>("/api/v1/complaints/payout-holds");
        Assert.DoesNotContain(holds!, h => h.ComplaintCode == "CP-IT-CLOSE");
    }

    [Fact] // US-094: chủ bãi phản hồi công khai 1 lần, khiếu nại đánh giá → Admin gỡ / bác; chỉ Admin được gỡ.
    public async Task Owner_replies_and_reports_review_and_only_admin_can_hide_it()
    {
        // Arrange: 2 đánh giá ở bãi Bến Thành (chủ bãi TSN theo StubOwnerLots) – 1 đã lưu chủ bãi, 1 chưa (đối chiếu theo bãi).
        int withOwner, withoutOwner;
        await using (var scope = fixture.SupportFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var a = new SupportService.Domain.Entities.Review { BookingId = 9_094_001, UserId = DemoIds.Driver1User, ReviewerName = "Tài xế IT",
                ParkingLotId = DemoIds.LotBenThanh, OwnerProfileId = DemoIds.OwnerTsn, Rating = 3, Comment = "Lối vào hơi hẹp (IT US-094)." };
            var b = new SupportService.Domain.Entities.Review { BookingId = 9_094_002, UserId = DemoIds.Driver2User, ReviewerName = "Tài xế IT 2",
                ParkingLotId = DemoIds.LotBenThanh, OwnerProfileId = null, Rating = 1, Comment = "Nội dung sai sự thật cần gỡ (IT US-094)." };
            db.Reviews.AddRange(a, b);
            await db.SaveChangesAsync();
            (withOwner, withoutOwner) = (a.Id, b.Id);
        }
        var reply = new { reply = "Cảm ơn góp ý, bãi đã mở rộng lối vào từ tuần này." };
        var reason = new { reason = "Tài xế không đỗ tại bãi vào ngày được đánh giá." };

        // Quyền: không token 401, tài xế 403, chủ bãi khác 404
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.SupportClient.PostAsJsonAsync($"/api/v1/reviews/{withOwner}/owner-reply", reply)).StatusCode);
        var driver = fixture.CreateAuthenticatedClient(DemoIds.Driver1User, "Driver");
        Assert.Equal(HttpStatusCode.Forbidden, (await driver.PostAsJsonAsync($"/api/v1/reviews/{withOwner}/owner-reply", reply)).StatusCode);
        var otherOwner = fixture.CreateOwnerClient(DemoIds.OwnerVincomUser, DemoIds.OwnerVincom);
        await Asserts.NotFoundAsync(await otherOwner.PostAsJsonAsync($"/api/v1/reviews/{withOwner}/owner-reply", reply));

        // AC1: 1 phản hồi công khai; lần 2 → 409; hiện trên trang bãi (API công khai)
        var owner = fixture.CreateOwnerClient(DemoIds.OwnerTsnUser, DemoIds.OwnerTsn);
        var replied = await owner.PostAsJsonAsync($"/api/v1/reviews/{withOwner}/owner-reply", reply);
        Assert.Equal(HttpStatusCode.OK, replied.StatusCode);
        await Asserts.ConflictAsync(await owner.PostAsJsonAsync($"/api/v1/reviews/{withOwner}/owner-reply", reply), "đã phản hồi");
        var lotPage = await fixture.SupportClient.GetStringAsync($"/api/v1/reviews?parkingLotId={DemoIds.LotBenThanh}");
        Assert.Contains("mở rộng lối vào", lotPage);

        // AC2: chủ bãi khiếu nại (đánh giá chưa lưu chủ bãi → đối chiếu theo bãi); chủ bãi KHÔNG tự gỡ được
        var report = await owner.PostAsJsonAsync($"/api/v1/reviews/{withoutOwner}/report", reason);
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        await Asserts.ConflictAsync(await owner.PostAsJsonAsync($"/api/v1/reviews/{withoutOwner}/report", reason), "chờ Admin");
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"/api/v1/reviews/{withoutOwner}/hide", reason)).StatusCode);

        // Admin: hàng chờ có đánh giá bị khiếu nại → gỡ → biến khỏi trang bãi; bác khiếu nại khi không có khiếu nại → 409
        var admin = fixture.CreateAuthenticatedClient(DemoIds.AdminUser, "Admin");
        var queue = await admin.GetFromJsonAsync<List<ModeratedReviewJson>>("/api/v1/reviews/reported");
        Assert.Contains(queue!, r => r.Id == withoutOwner && r.IsReported);
        var hidden = await admin.PostAsJsonAsync($"/api/v1/reviews/{withoutOwner}/hide", new { reason = "Nội dung sai sự thật, đã xác minh với log cổng." });
        Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
        Assert.DoesNotContain("Nội dung sai sự thật cần gỡ", await fixture.SupportClient.GetStringAsync($"/api/v1/reviews?parkingLotId={DemoIds.LotBenThanh}"));
        await Asserts.ConflictAsync(await admin.PostAsJsonAsync($"/api/v1/reviews/{withOwner}/dismiss-report", reason), "không có khiếu nại");

        // DB: chủ bãi được bổ sung theo bãi; dữ liệu gỡ có người/thời điểm; Outbox có thông báo, sự kiện xếp hạng và audit
        await using (var scope = fixture.SupportFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var removed = await db.Reviews.SingleAsync(r => r.Id == withoutOwner);
            Assert.Equal(DemoIds.OwnerTsn, removed.OwnerProfileId);
            Assert.True(removed.IsHidden);
            Assert.False(removed.IsFlagged);
            Assert.Equal(DemoIds.AdminUser, removed.HiddenByUserId);
            var types = await db.OutboxMessages.Select(o => o.EventType).ToListAsync();
            Assert.Contains("ReviewOwnerReplied", types);
            Assert.Contains("ReviewHidden", types);
            Assert.Contains("AuditRecorded", types);
        }
    }

    [Fact] // US-091
    public async Task Driver_reports_lot_full_then_accepts_transfer_or_declines_for_refund_and_events_are_published()
    {
        var driver = fixture.CreateAuthenticatedClient(DemoIds.Driver1User, "Driver");
        var bookingApi = fixture.BookingFactory.CreateClient();

        // Quyền: không token 401; booking của tài xế khác 404
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.SupportClient.PostAsJsonAsync("/api/v1/lot-full-incidents", new { bookingCode = "BK-IT-FULL-MOVE" })).StatusCode);
        var otherDriver = fixture.CreateAuthenticatedClient(DemoIds.Driver2User, "Driver");
        await Asserts.NotFoundAsync(await otherDriver.PostAsJsonAsync("/api/v1/lot-full-incidents", new { bookingCode = "BK-IT-FULL-MOVE" }));

        // AC1 + AC2: sự cố ưu tiên cao; gợi ý Bến Thành (0,6 km), bỏ bãi ngoài 1 km; báo lần 2 → 409
        var reported = await driver.PostAsJsonAsync("/api/v1/lot-full-incidents",
            new { bookingCode = "BK-IT-FULL-MOVE", note = "Bảo vệ báo bãi đã hết chỗ." });
        Assert.Equal(HttpStatusCode.Created, reported.StatusCode);
        var move = (await reported.Content.ReadFromJsonAsync<LotFullIncidentJson>())!;
        Assert.Equal(("Reported", "High", 60000m), (move.Status, move.Priority, move.PaidAmount));
        Assert.Equal([DemoIds.LotBenThanh], move.Alternatives.Select(a => a.ParkingLotId));
        await Asserts.ConflictAsync(await driver.PostAsJsonAsync("/api/v1/lot-full-incidents", new { bookingCode = "BK-IT-FULL-MOVE" }), "đã được báo");
        await Asserts.NotFoundAsync(await otherDriver.GetAsync($"/api/v1/lot-full-incidents/{move.Code}"));

        // AC2: chọn bãi ngoài danh sách → 400; chọn Bến Thành → TransferAccepted; quyết định lần 2 → 409
        Assert.Equal(HttpStatusCode.BadRequest, (await driver.PostAsJsonAsync($"/api/v1/lot-full-incidents/{move.Code}/accept-transfer",
            new { parkingLotId = DemoIds.LotLandmark })).StatusCode);
        var accepted = await driver.PostAsJsonAsync($"/api/v1/lot-full-incidents/{move.Code}/accept-transfer",
            new { parkingLotId = DemoIds.LotBenThanh });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var moved = (await accepted.Content.ReadFromJsonAsync<LotFullIncidentJson>())!;
        Assert.Equal(("TransferAccepted", DemoIds.LotBenThanh, false), (moved.Status, moved.NewParkingLotId!.Value, moved.IsSlaViolation));
        await Asserts.ConflictAsync(await driver.PostAsync($"/api/v1/lot-full-incidents/{move.Code}/decline", null), "đã được xử lý");

        // AC3: từ chối → yêu cầu hoàn 100% + voucher, ghi vi phạm SLA
        var refundIncident = (await (await driver.PostAsJsonAsync("/api/v1/lot-full-incidents", new { bookingCode = "BK-IT-FULL-REFUND" }))
            .Content.ReadFromJsonAsync<LotFullIncidentJson>())!;
        var declined = await driver.PostAsync($"/api/v1/lot-full-incidents/{refundIncident.Code}/decline", null);
        Assert.Equal(HttpStatusCode.OK, declined.StatusCode);
        var refunded = (await declined.Content.ReadFromJsonAsync<LotFullIncidentJson>())!;
        Assert.Equal(("RefundRequested", 100, 20000m, true),
            (refunded.Status, refunded.RefundPercent!.Value, refunded.VoucherAmount!.Value, refunded.IsSlaViolation));

        // SupportService KHÔNG tự sửa booking – BookingService/PaymentService xử lý khi nhận sự kiện
        Assert.Equal("Confirmed", (await bookingApi.GetFromJsonAsync<BookingJson>("/api/v1/bookings/BK-IT-FULL-MOVE"))!.Status);

        // Admin theo dõi vi phạm SLA; tài xế không xem được danh sách
        var admin = fixture.CreateAuthenticatedClient(DemoIds.AdminUser, "Admin");
        var sla = await admin.GetFromJsonAsync<List<LotFullIncidentJson>>($"/api/v1/lot-full-incidents?parkingLotId={DemoIds.LotVincom}&slaViolationOnly=true");
        Assert.Equal([refundIncident.Code], sla!.Select(i => i.Code));
        Assert.Equal(HttpStatusCode.Forbidden, (await driver.GetAsync("/api/v1/lot-full-incidents")).StatusCode);

        // Outbox: thông báo sự cố, yêu cầu chuyển booking, yêu cầu hoàn tiền + voucher, vi phạm SLA (kèm đúng dữ liệu)
        await using var scope = fixture.SupportFactory.Services.CreateAsyncScope();
        var outbox = await scope.ServiceProvider.GetRequiredService<SupportDbContext>().OutboxMessages
            .Where(o => o.EventType.StartsWith("LotFull") || o.EventType == "LotSlaViolated")
            .Select(o => new { o.EventType, o.PayloadJson }).ToListAsync();
        Assert.Equal(2, outbox.Count(o => o.EventType == "LotFullIncidentReported"));
        var transfer = System.Text.Json.JsonDocument.Parse(outbox.Single(o => o.EventType == "LotFullTransferAccepted").PayloadJson).RootElement;
        Assert.Equal(("BK-IT-FULL-MOVE", DemoIds.LotBenThanh),
            (transfer.GetProperty("BookingCode").GetString(), transfer.GetProperty("NewParkingLotId").GetInt32()));
        var refundEvt = System.Text.Json.JsonDocument.Parse(outbox.Single(o => o.EventType == "LotFullRefundRequested").PayloadJson).RootElement;
        Assert.Equal(("BK-IT-FULL-REFUND", 100), (refundEvt.GetProperty("BookingCode").GetString(), refundEvt.GetProperty("RefundPercent").GetInt32()));
        Assert.Single(outbox, o => o.EventType == "LotSlaViolated");
    }

    [Fact] // US-090: chỗ đã đặt bị chiếm → chủ bãi (duy nhất) chuyển chỗ + phạt xe đậu sai chỗ; Admin không can thiệp; phán quyết không sửa được.
    public async Task Owner_rules_slot_occupied_dispute_and_fines_violator_and_nobody_else_can_intervene()
    {
        // Arrange: khiếu nại "chỗ bị chiếm" về booking BK-IT-DONE (đã trả 50.000đ ở bãi Vincom), đang chờ chủ bãi Vincom xử lý.
        const string code = "CP-IT-SLOT";
        const string url = $"/api/v1/complaints/{code}";
        await using (var scope = fixture.SupportFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            db.Complaints.Add(new SupportService.Domain.Entities.Complaint
            {
                Code = code, UserId = DemoIds.Driver1User, BookingCode = "BK-IT-DONE", ParkingLotId = DemoIds.LotVincom,
                OwnerProfileId = DemoIds.OwnerVincom, Category = ComplaintCategory.SlotOccupied, RequestedAmount = 50000m,
                Description = "Đến bãi thì chỗ đã đặt bị xe khác đậu (integration test US-090).",
                Status = ComplaintStatus.AwaitingOwner, OwnerResponseDueAtUtc = DateTime.UtcNow.AddHours(40), PayoutHeld = true,
            });
            await db.SaveChangesAsync();
        }
        var owner = fixture.CreateOwnerClient(DemoIds.OwnerVincomUser, DemoIds.OwnerVincom);
        var otherOwner = fixture.CreateOwnerClient(DemoIds.OwnerTsnUser, DemoIds.OwnerTsn);
        var admin = fixture.CreateAuthenticatedClient(DemoIds.AdminUser, "Admin");
        var driver = fixture.CreateAuthenticatedClient(DemoIds.Driver1User, "Driver");
        var move = new { decision = "MoveToSlot", reason = "Xe 51A-999.99 đậu sai vào chỗ đã đặt, chuyển tài xế sang chỗ trống B-12.",
            newSlotCode = "B-12", violatorPlate = "51A-999.99" };

        // Chỉ chủ bãi của bãi bị khiếu nại: không token 401; tài xế / Admin 403; chủ bãi khác 404
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.SupportClient.GetAsync($"{url}/case-file")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await driver.GetAsync($"{url}/case-file")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"{url}/case-file")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"{url}/rulings", move)).StatusCode);
        await Asserts.NotFoundAsync(await otherOwner.GetAsync($"{url}/case-file"));
        await Asserts.NotFoundAsync(await otherOwner.PostAsJsonAsync($"{url}/rulings", move));

        // AC3: tranh chấp nằm trong hàng chờ của chủ bãi, SLA phản hồi đầu 24 giờ
        var item = (await owner.GetFromJsonAsync<List<DisputeQueueJson>>("/api/v1/complaints/disputes"))!.Single(q => q.Code == code);
        Assert.False(item.Sla.FirstResponseBreached);
        Assert.Equal(item.Sla.StartedAtUtc.AddHours(24), item.Sla.FirstResponseDueAtUtc);

        // AC1: hồ sơ có lịch sử booking (BookingService thật) và log check-in/out (GateService)
        var file = await owner.GetFromJsonAsync<CaseFileJson>($"{url}/case-file");
        Assert.Equal(("BK-IT-DONE", 50000m, true), (file!.Booking!.Code, file.Booking.PaidAmount, file.CanRule));
        Assert.Equal("Lpr", Assert.Single(file.GateSessions).CheckInMethod);
        Assert.Empty(file.UnavailableSources);

        // Phạt đậu sai chỗ cần mức phạt chủ bãi đặt trước → 409; chủ bãi đặt cho bãi mình (bãi khác → 404)
        await Asserts.ConflictAsync(await owner.PostAsJsonAsync($"{url}/rulings", move), "mức phạt");
        await Asserts.NotFoundAsync(await owner.PutAsJsonAsync($"/api/v1/complaints/fine-rates/{DemoIds.LotTsn}", new { wrongSlotFine = 100000 }));
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PutAsJsonAsync($"/api/v1/complaints/fine-rates/{DemoIds.LotVincom}", new { wrongSlotFine = 200000 })).StatusCode);

        // AC3: chủ bãi nhắn cho tài xế → tính là phản hồi đầu tiên; tài xế thấy tin nhắn trong ticket của mình
        await owner.PostAsJsonAsync($"{url}/dispute-messages", new { message = "Nhân viên đang kiểm tra chỗ của anh." });
        Assert.NotNull((await owner.GetFromJsonAsync<CaseFileJson>($"{url}/case-file"))!.Sla.FirstRespondedAtUtc);
        var driverView = await fixture.SupportClient.GetFromJsonAsync<ComplaintDetailJson>($"{url}?userId={DemoIds.Driver1User}");
        Assert.Contains(driverView!.Messages, m => m.SenderParty == "LotOwner");

        // AC2: hoàn vượt số đã trả → 400; chuyển chỗ + lập biên bản phạt 200.000đ → Resolved, giải phóng khoản giữ; quyết lần 2 → 409
        await Asserts.BadRequestAsync(await owner.PostAsJsonAsync($"{url}/rulings",
            new { decision = "ForceRefund", reason = "Hoàn tiền vì không còn chỗ trống phù hợp.", refundAmount = 50001 }), "50.000đ");
        var ruled = await owner.PostAsJsonAsync($"{url}/rulings", move);
        Assert.Equal(HttpStatusCode.OK, ruled.StatusCode);
        file = await ruled.Content.ReadFromJsonAsync<CaseFileJson>();
        Assert.Equal(("Resolved", false, false), (file!.Status, file.PayoutHeld, file.CanRule));
        Assert.Equal(("MoveToSlot", "B-12", DemoIds.OwnerVincomUser), (file.Ruling!.Decision, file.Ruling.NewSlotCode, file.Ruling.DecidedByUserId));
        var violation = Assert.Single(file.Violations);
        Assert.Equal(200000m, violation.FineAmount);
        Assert.StartsWith("51A", violation.PlateNumber);
        Assert.Contains((await owner.GetFromJsonAsync<List<ViolationJson>>("/api/v1/complaints/violations"))!, v => v.ComplaintCode == code);
        await Asserts.ConflictAsync(await owner.PostAsJsonAsync($"{url}/rulings", move), "1 lần");

        // AC2: tiền lệ ở các bãi của chủ bãi; chủ bãi khác không thấy
        Assert.Contains((await owner.GetFromJsonAsync<List<PrecedentJson>>("/api/v1/complaints/disputes/precedents?category=SlotOccupied"))!,
            p => p.ComplaintCode == code);
        Assert.DoesNotContain((await otherOwner.GetFromJsonAsync<List<PrecedentJson>>("/api/v1/complaints/disputes/precedents"))!,
            p => p.ComplaintCode == code);

        // Outbox: phán quyết (Booking/Payment/Notification), biên bản phạt (Gate/Payment), audit log (AdminService)
        Assert.Equal(["DisputeRuled"], await OutboxEventsForAsync(code));
        await using (var scope = fixture.SupportFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var payloads = await db.OutboxMessages.Where(o => o.EventType == "AuditRecorded" || o.EventType == "ParkingViolationFined")
                .Select(o => new { o.EventType, o.PayloadJson }).ToListAsync();
            var fined = payloads.Where(p => p.EventType == "ParkingViolationFined")
                .Select(p => System.Text.Json.JsonDocument.Parse(p.PayloadJson).RootElement)
                .Single(e => e.GetProperty("ComplaintId").GetInt32() == file.Id);
            Assert.Equal(200000m, fined.GetProperty("FineAmount").GetDecimal());
            var actions = payloads.Where(p => p.EventType == "AuditRecorded").Select(p => System.Text.Json.JsonDocument.Parse(p.PayloadJson).RootElement)
                .Where(a => a.GetProperty("EntityName").GetString() == "Complaint" && a.GetProperty("EntityId").GetString() == file.Id.ToString())
                .Select(a => a.GetProperty("Action").GetString()).ToList();
            Assert.Equal(["DisputeRuled"], actions);

            // AC4: phán quyết không sửa / xóa được kể cả bằng SQL tay (trigger)
            var update = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
                db.Database.ExecuteSqlRawAsync("UPDATE \"DisputeRulings\" SET \"Reason\" = 'sửa' WHERE \"ComplaintId\" = {0}", file.Id));
            Assert.Contains("chỉ được thêm", update.MessageText);
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
                db.Database.ExecuteSqlRawAsync("DELETE FROM \"DisputeRulings\" WHERE \"ComplaintId\" = {0}", file.Id));
        }
    }

    public sealed record DisputeSlaJson(DateTime StartedAtUtc, DateTime FirstResponseDueAtUtc, DateTime? FirstRespondedAtUtc,
        bool FirstResponseBreached, DateTime ResolutionDueAtUtc, bool ResolutionBreached);

    public sealed record DisputeQueueJson(string Code, string Status, DisputeSlaJson Sla);

    public sealed record CaseFileJson(int Id, string Code, string Status, bool PayoutHeld, bool CanRule, DisputeSlaJson Sla,
        RulingJson? Ruling, IReadOnlyList<ViolationJson> Violations, BookingEvidenceJson? Booking,
        IReadOnlyList<GateSessionJson> GateSessions, IReadOnlyList<string> UnavailableSources);

    public sealed record RulingJson(string Decision, decimal? RefundAmount, string? NewSlotCode, int DecidedByUserId);

    public sealed record ViolationJson(string Code, string ComplaintCode, string PlateNumber, decimal FineAmount);

    public sealed record BookingEvidenceJson(string Code, decimal PaidAmount);

    public sealed record GateSessionJson(string Code, string CheckInMethod);

    public sealed record PrecedentJson(string ComplaintCode, string Decision);

    public sealed record LotFullIncidentJson(string Code, string Status, string Priority, string BookingCode, decimal PaidAmount,
        IReadOnlyList<AlternativeJson> Alternatives, int? NewParkingLotId, int? RefundPercent, decimal? VoucherAmount, bool IsSlaViolation);

    public sealed record AlternativeJson(int ParkingLotId, string Name, double DistanceKm);

    public sealed record BookingJson(string Code, string Status);

    public sealed record ModeratedReviewJson(int Id, int ParkingLotId, int Rating, string? OwnerReply, bool IsReported, bool IsHidden);

    public sealed record OwnerComplaintJson(string Code, string Status, string AssignedTeam, bool PayoutHeld,
        DateTime? OwnerRespondedAtUtc, DateTime? EscalatedAtUtc, IReadOnlyList<MessageJson> Messages);

    public sealed record PayoutHoldJson(string ComplaintCode, string Status, int ParkingLotId, int? OwnerProfileId, decimal Amount);

    public sealed record EligibilityJson(string BookingCode, bool CanReview, string? Reason, int? ReviewId);

    public sealed record ReviewJson(int Id, string BookingCode, int ParkingLotId, int Rating, string? Comment, string ReviewerName);

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
