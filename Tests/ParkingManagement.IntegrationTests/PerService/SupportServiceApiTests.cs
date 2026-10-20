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
