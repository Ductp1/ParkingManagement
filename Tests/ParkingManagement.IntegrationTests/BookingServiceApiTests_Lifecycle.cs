using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BookingService.API.Controllers;
using BookingService.Application.Features.Bookings;
using BookingService.Infrastructure.Persistence;
using BookingService.Infrastructure.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.API.Controllers;
using ParkingService.Infrastructure.Persistence;
using ParkingService.Domain.Entities;
using ParkingService.Infrastructure.Persistence;

namespace ParkingManagement.IntegrationTests;

/// <summary>
/// Suite tích hợp "vòng đời booking" end-to-end: chạy host thật (WebApplicationFactory) của BookingService VÀ ParkingService
/// đi qua đủ API → Application → Domain → Infrastructure → PostgreSQL, kèm kiểm chứng trực tiếp trong database.
/// Mỗi lần chạy suite tự tạo 2 database PostgreSQL trống (pm_it_parking_*, pm_it_booking_*), migrate + seed demo như production
/// rồi xóa sạch khi xong — không đụng pm_parking/pm_booking dùng chung của nhóm, chạy song song/chạy lại bao nhiêu lần cũng ổn.
/// Yêu cầu: PostgreSQL chạy ở localhost:5432 (postgres/postgres) như README, hoặc đặt ConnectionStrings__ServiceDb trỏ tới server bất kỳ.
/// </summary>
[CollectionDefinition("Booking lifecycle (isolated databases)")]
public sealed class BookingLifecycleCollection : ICollectionFixture<BookingLifecycleFixture>;

/// <summary>
/// Fixture dùng chung cả suite: dựng 2 service thật + 2 database PostgreSQL riêng biệt.
/// - Nguồn connection (theo thứ tự ưu tiên, cùng quy ước run-all.ps1): biến môi trường ConnectionStrings__ServiceDb (CI dùng) →
///   DB_HOST/DB_PORT/DB_USER/DB_PASSWORD hoặc file .env ở gốc repo (local) → mặc định appsettings.json (postgres/postgres).
/// - CREATE DATABASE pm_it_* trước khi dựng host, DROP DATABASE ... WITH (FORCE) khi teardown (PostgreSQL 13+),
///   nên CI chỉ cần server PostgreSQL với user có quyền CREATEDB (postgres superuser trong workflow hiện tại là đủ).
/// - QRTOKEN_SECRET chỉ được set nếu máy chưa có (CI đã cung cấp secret thật, không ghi đè).
/// </summary>
public sealed class BookingLifecycleFixture : IAsyncLifetime
{
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];
    private readonly string _baseConnectionString;

    public string ParkingDbName { get; }
    public string BookingDbName { get; }
    public WebApplicationFactory<ParkingLotsController> ParkingFactory { get; private set; } = null!;
    public WebApplicationFactory<BookingsController> BookingFactory { get; private set; } = null!;
    public HttpClient ParkingClient { get; private set; } = null!;
    public HttpClient BookingClient { get; private set; } = null!;

    /// <summary>Root DI của ParkingService (resolve DbContext để arrange dữ liệu test).</summary>
    public IServiceProvider ParkingServices => ParkingFactory.Services;

    /// <summary>Root DI của BookingService (resolve IQrTokenService, IHostedService, DbContext...).</summary>
    public IServiceProvider BookingServices => BookingFactory.Services;

    public BookingLifecycleFixture()
    {
        ParkingDbName = $"pm_it_parking_{_suffix}";
        BookingDbName = $"pm_it_booking_{_suffix}";
        _baseConnectionString = TestDatabaseProvisioner.ResolveBaseConnectionString();
    }

    private static string GetEnv(string name, string fallback)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    /// <summary>Nạp .env ở gốc repo (cùng cách run-all.ps1 làm) nhưng KHÔNG ghi đè biến môi trường đã có sẵn.</summary>
    private static void LoadDotEnvAtRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ParkingManagement.slnx")))
            dir = dir.Parent;
        if (dir?.FullName is null) return;
        var envPath = Path.Combine(dir.FullName, ".env");
        if (!File.Exists(envPath)) return;

        foreach (var line in File.ReadAllLines(envPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    public async Task InitializeAsync()
    {
        await TestDatabaseProvisioner.CreateDatabaseAsync(_baseConnectionString, ParkingDbName);
        await TestDatabaseProvisioner.CreateDatabaseAsync(_baseConnectionString, BookingDbName);

        ParkingFactory = BuildFactory<ParkingLotsController, ParkingDbContext>(
            TestDatabaseProvisioner.ReplaceDatabase(_baseConnectionString, ParkingDbName));
        BookingFactory = BuildFactory<BookingsController, BookingDbContext>(
            TestDatabaseProvisioner.ReplaceDatabase(_baseConnectionString, BookingDbName));

        // Dựng host sớm ngay tại đây: migration + seed demo chạy xong trước test đầu tiên,
        // lỗi hạ tầng (PostgreSQL không chạy, thiếu secret...) báo ngay tại Initialize thay vì test đầu tiên.
        ParkingClient = ParkingFactory.CreateClient();
        BookingClient = BookingFactory.CreateClient();

        using var parkingHealth = await ParkingClient.GetAsync("/health");
        Assert.True(parkingHealth.IsSuccessStatusCode, $"ParkingService không start được trên database {ParkingDbName}.");
        using var bookingHealth = await BookingClient.GetAsync("/health");
        Assert.True(bookingHealth.IsSuccessStatusCode, $"BookingService không start được trên database {BookingDbName}.");
    }

    /// <summary>
    /// Host thật + database cô lập: thay đăng ký DbContext bằng connection string của test
    /// (ConfigureServices chạy sau Program.cs nên luôn thắng; không đụng biến môi trường toàn cục).
    /// </summary>
    private static WebApplicationFactory<TEntryPoint> BuildFactory<TEntryPoint, TContext>(string connectionString)
        where TEntryPoint : class
        where TContext : DbContext
        => new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.UseIsolatedDatabase<TContext>(connectionString)));

    /// <summary>Làm tròn về đầu phút – ủy quyền cho TestTime.</summary>
    public static DateTime RoundToMinute(DateTime utc) => TestTime.RoundToMinute(utc);

    // ===================== Helper dùng chung cho các test =====================

    /// <summary>
    /// Tạo booking qua API thật (US-022). Mặc định: driver 1 – xe 51A88888 – bãi Vincom slot B1-A02,
    /// bắt đầu sau 2 giờ, dài 2 giờ, giá 40.000đ, không khuyến mãi, Dynamic allocation.
    /// </summary>
    public async Task<BookingDetailJson> CreateBookingAsync(
        int userId = DemoIds.Driver1User,
        int vehicleId = DemoIds.Driver1Car,
        string plateNumber = "51A88888",
        string vehicleType = "Sedan",
        int parkingLotId = DemoIds.LotVincom,
        int ownerProfileId = DemoIds.OwnerVincom,
        string parkingLotName = "Bãi xe Vincom Đồng Khởi",
        int? zoneId = DemoIds.ZoneVincomB,
        int? slotId = 2,
        string? slotCode = "B1-A02",
        DateTime? startAtUtc = null,
        TimeSpan? duration = null,
        decimal totalAmount = 40000,
        string? promotionCode = null,
        bool requiresOwnerApproval = false)
    {
        var start = startAtUtc ?? RoundToMinute(DateTime.UtcNow.AddHours(2));
        var command = new
        {
            userId,
            vehicleId,
            plateNumber,
            vehicleType,
            parkingLotId,
            ownerProfileId,
            parkingLotName,
            zoneId,
            slotId,
            slotCode,
            allocationMode = "Dynamic",
            startAtUtc = start,
            endAtUtc = start + (duration ?? TimeSpan.FromHours(2)),
            totalAmount,
            promotionCode,
            requiresOwnerApproval,
        };

        // Mã BK-yyyyMMdd-NNNN sinh ngẫu nhiên: trùng unique index là hiếm (1/9000) nhưng retry 3 lần cho chắc.
        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            response?.Dispose();
            response = await BookingClient.PostAsJsonAsync("/api/v1/bookings", command);
            if (response.StatusCode != HttpStatusCode.InternalServerError) break;
        }

        Assert.Equal(HttpStatusCode.Created, response!.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingDetailJson>())!;
    }

    /// <summary>Tạo booking rồi để chủ bãi duyệt (PendingPayment → Confirmed) — API không có bước "callback thanh toán" nội bộ.</summary>
    public async Task<BookingDetailJson> CreateApprovedBookingAsync(int ownerProfileId = DemoIds.OwnerVincom,
        int userId = DemoIds.Driver1User, DateTime? startAtUtc = null, TimeSpan? duration = null)
    {
        var booking = await CreateBookingAsync(userId: userId, startAtUtc: startAtUtc, duration: duration);
        return await ApproveAsync(booking.Code, ownerProfileId);
    }

    /// <summary>POST /{code}/approve – chủ bãi duyệt booking (US-031).</summary>
    public async Task<BookingDetailJson> ApproveAsync(string code, int ownerProfileId)
    {
        var response = await BookingClient.PostAsJsonAsync($"/api/v1/bookings/{code}/approve", new { ownerProfileId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingDetailJson>())!;
    }

    public async Task DisposeAsync()
    {
        ParkingFactory?.Dispose();
        BookingFactory?.Dispose();
        try
        {
            await TestDatabaseProvisioner.DropDatabaseAsync(_baseConnectionString, ParkingDbName);
            await TestDatabaseProvisioner.DropDatabaseAsync(_baseConnectionString, BookingDbName);
        }
        catch
        {
            // Dọn dẹp là best-effort: PostgreSQL tắt sớm cũng không được làm đỏ kết quả test.
        }
    }
}

// ===== JSON contract của API (chỉ khai báo field test cần, camelCase khớp mặc định của ASP.NET Core) =====

public sealed record BookingDetailJson(int Id, string Code, string Status, int UserId, int VehicleId, string PlateNumber, string VehicleType,
    int ParkingLotId, string ParkingLotName, int? ZoneId, int? SlotId, string? SlotCode, DateTime StartAtUtc, DateTime EndAtUtc,
    DateTime? HoldExpiresAtUtc, decimal TotalAmount, decimal DiscountAmount, decimal PaidAmount, string? PromotionCode,
    PriceSnapshotJson? PriceSnapshot, IReadOnlyList<StatusLogJson> History, string? QrToken);

public sealed record PriceSnapshotJson(int RateCardId, decimal BaseAmount, decimal SurchargeAmount, decimal DiscountAmount,
    decimal FinalAmount, int BillingGracePeriodMinutes, string RateCardJson);

public sealed record StatusLogJson(string? FromStatus, string ToStatus, int? ChangedByUserId, string? Reason, DateTime AtUtc);

public sealed record BookingSummaryJson(int Id, string Code, string Status, string ParkingLotName, string? SlotCode,
    string PlateNumber, DateTime StartAtUtc, DateTime EndAtUtc, decimal TotalAmount);

public sealed record CancellationPreviewJson(string Code, bool CanCancel, string ResultStatus, int RefundPercent, decimal RefundAmount, string Reason);

public sealed record CancellationResultJson(string Code, string Status, int RefundPercent, decimal RefundAmount, string Reason);

/// <summary>ProblemDetails (RFC 7807) do ExceptionHandlingMiddleware dùng chung 9 service trả ra.</summary>
public sealed record ProblemJson(string? Title, string? Detail, int? Status);

public sealed record LotDetailJson(int Id, string Name, string Status, int TotalSlots, int AvailableSlots, int MaxHeightCm, string OpeningHours);

public sealed record LotHierarchyJson(int Id, string Name, string Status, int TotalSlots, IReadOnlyList<ZoneJson> Zones);

public sealed record ZoneJson(int Id, string Code, string Name, IReadOnlyList<FloorJson> Floors);

public sealed record FloorJson(int Id, string Name, int Level, IReadOnlyList<SlotJson> Slots);

public sealed record SlotJson(int Id, string Code, string State, bool IsActive);

public sealed record LotSearchItemJson(int Id, string Name, double DistanceKm, int AvailableSlots, int TotalSlots, int MaxHeightCm, bool IsOpenNow);

public sealed record KybJson(int Id, int ParkingLotId, string Status, bool FieldSurveyRequired, DateTime? SubmittedAtUtc);

public sealed record CreatedLotJson(int Id);

/// <summary>
/// Suite "vòng đời booking" chạy end-to-end qua HTTP thật trên database riêng của từng lần chạy:
/// tạo giữ chỗ 15 phút (US-022) → duyệt/sửa/gia hạn (US-029/030/031) → hủy theo CancellationWindow (US-038)
/// → chủ bãi hủy sự cố (US-042) → cổng check-in/out/no-show (hợp đồng TV7) → worker hết hạn giữ chỗ (US-023),
/// đồng thời kiểm chứng dữ liệu chéo service pm_parking ↔ pm_booking theo hợp đồng DemoIds.
/// Tên lớp chứa "BookingServiceApiTests" để job CI BookingService (--filter ~BookingServiceApiTests) tự chạy suite này.
/// Lưu ý: chỉ "Cancellation_window_decide_the_refund" được thay đổi BK-0002; các test khác chỉ đọc dữ liệu seed.
/// </summary>
[Collection("Booking lifecycle (isolated databases)")]
public class BookingServiceApiTests_Lifecycle(BookingLifecycleFixture fixture)
{
    [Fact] // Hợp đồng dữ liệu chéo service: seed của Booking phải khớp catalog + cây cấu trúc + tìm kiếm của ParkingService.
    public async Task Booking_seed_snapshots_match_parking_catalog_hierarchy_and_search()
    {
        // ----- ParkingService: chi tiết bãi + cây Lot → Zone → Floor → Slot -----
        var lot = await fixture.ParkingClient.GetFromJsonAsync<LotDetailJson>("/api/v1/parking-lots/1");
        Assert.Equal("Bãi xe Vincom Đồng Khởi", lot!.Name);
        Assert.Equal("Active", lot.Status);
        Assert.Equal(80, lot.TotalSlots);
        Assert.Equal(210, lot.MaxHeightCm);
        Assert.Equal("06:00 - 23:00", lot.OpeningHours);

        var hierarchy = await fixture.ParkingClient.GetFromJsonAsync<LotHierarchyJson>("/api/v1/parking-lots/1/hierarchy");
        var zoneB = Assert.Single(hierarchy!.Zones, z => z.Code == "B");
        var floorB1 = Assert.Single(zoneB.Floors, f => f.Name == "B1" && f.Level == -1);
        Assert.Contains(floorB1.Slots, s => s is { Id: DemoIds.SlotVincomB1A01, Code: "B1-A01" });
        var slotA02 = Assert.Single(floorB1.Slots, s => s.Code == "B1-A02");
        Assert.Equal(DemoIds.SlotVincomB1A02, slotA02.Id);
        Assert.Contains(zoneB.Floors, f => f.Name == "B2" && f.Level == -2);

        // ----- Tìm kiếm: bán kính + lọc chiều cao xe + chỉ trả bãi Active -----
        var nearby = await fixture.ParkingClient.GetFromJsonAsync<List<LotSearchItemJson>>(
            "/api/v1/parking-lots/search?lat=10.777&lng=106.701&radiusKm=5");
        var vincom = Assert.Single(nearby!); // TSN cách ~5,9km; Landmark chờ duyệt; Bến Thành bị treo
        Assert.Equal(DemoIds.LotVincom, vincom.Id);
        Assert.True(vincom.DistanceKm < 0.5, $"Vincom phải cách Quận 1 dưới 0,5km, thực tế {vincom.DistanceKm}km");

        var tallVehicle = await fixture.ParkingClient.GetFromJsonAsync<List<LotSearchItemJson>>(
            "/api/v1/parking-lots/search?lat=10.777&lng=106.701&radiusKm=30&vehicleHeightCm=240");
        var tsn = Assert.Single(tallVehicle!); // cần trần ≥ 250cm trong bán kính 30km → chỉ TSN qua được
        Assert.Equal(DemoIds.LotTsn, tsn.Id);

        // ----- BookingService: snapshot của BK-0002 phải trỏ đúng bãi/slot phía trên -----
        var booking = await fixture.BookingClient.GetFromJsonAsync<BookingDetailJson>("/api/v1/bookings/BK-0002");
        Assert.Equal(lot.Id, booking!.ParkingLotId);
        Assert.Equal(lot.Name, booking.ParkingLotName);
        Assert.Equal(slotA02.Id, booking.SlotId);
        Assert.Equal(slotA02.Code, booking.SlotCode);
        Assert.Equal("WELCOME10", booking.PromotionCode);
        Assert.Equal(70000m, booking.TotalAmount);
        Assert.Equal(7000m, booking.DiscountAmount);
        Assert.Equal(63000m, booking.PaidAmount);
        Assert.Equal("demo-qr-BK-0002", booking.QrToken);
        Assert.Equal(DemoIds.RateCardVincom, booking.PriceSnapshot!.RateCardId);
        Assert.Equal(7000m, booking.PriceSnapshot.DiscountAmount);
        Assert.Equal(63000m, booking.PriceSnapshot.FinalAmount);
        Assert.Contains(booking.History, l => l.ToStatus == "Confirmed" && l.Reason == "VNPAY callback thành công");

        // BK-0001 hoàn thành đủ mốc thời gian (kiểm chứng trực tiếp trong pm_booking).
        await using var scope = fixture.BookingServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var completed = await db.Bookings.AsNoTracking().SingleAsync(b => b.Code == "BK-0001");
        Assert.Equal(BookingStatus.Completed, completed.Status);
        Assert.NotNull(completed.CheckedInAtUtc);
        Assert.NotNull(completed.CheckedOutAtUtc);
        Assert.NotNull(completed.CompletedAtUtc);
    }

    [Fact] // TC-BOOK-01 (US-022): tạo booking mở giữ chỗ miễn phí 15 phút, sinh QR có chữ ký và khóa giá (PriceSnapshot).
    public async Task Creating_a_booking_starts_a_15_minute_hold_with_signed_qr_and_price_snapshot()
    {
        var before = DateTime.UtcNow;
        var booking = await fixture.CreateBookingAsync();
        var after = DateTime.UtcNow;

        // ----- Hợp đồng HTTP -----
        Assert.Equal("PendingPayment", booking.Status);
        Assert.Equal("51A88888", booking.PlateNumber);
        Assert.Equal("Sedan", booking.VehicleType);
        Assert.Equal("Bãi xe Vincom Đồng Khởi", booking.ParkingLotName);
        Assert.Equal("B1-A02", booking.SlotCode);
        Assert.Equal(40000m, booking.TotalAmount);
        Assert.Equal(0m, booking.DiscountAmount);
        Assert.Equal(0m, booking.PaidAmount);
        Assert.Null(booking.PromotionCode);
        Assert.NotNull(booking.HoldExpiresAtUtc);
        Assert.InRange(booking.HoldExpiresAtUtc!.Value,
            before.AddMinutes(15).AddSeconds(-2), after.AddMinutes(15).AddSeconds(2));

        // State machine ghi đúng 1 log chuyển Created → PendingPayment với lý do giữ chỗ.
        var holdLog = Assert.Single(booking.History, l => l.ToStatus == "PendingPayment");
        Assert.Equal("Bắt đầu giữ chỗ miễn phí 15 phút", holdLog.Reason);

        // QR token phải xác thực được bằng đúng secret host đang chạy (Booking ↔ Gate chia sẻ).
        var qrService = fixture.BookingServices.GetRequiredService<IQrTokenService>();
        Assert.True(qrService.ValidateToken(booking.QrToken!, out var qrCode, out var qrPlate, out var qrLotId),
            "QrToken sinh ra phải validate thành công bằng IQrTokenService trong DI.");
        Assert.Equal(booking.Code, qrCode);
        Assert.Equal("51A88888", qrPlate);
        Assert.Equal(DemoIds.LotVincom, qrLotId);

        // ----- Database: PriceSnapshot khóa giá + RowVersion đóng dấu optimistic concurrency -----
        await using var scope = fixture.BookingServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var entity = await db.Bookings.Include(b => b.PriceSnapshot).SingleAsync(b => b.Code == booking.Code);
        Assert.Equal(BookingStatus.PendingPayment, entity.Status);
        Assert.NotEmpty(entity.RowVersion);
        Assert.InRange(entity.CreatedAtUtc, before.AddSeconds(-2), after.AddSeconds(2));
        Assert.Single(entity.StatusLogs);
        Assert.NotNull(entity.PriceSnapshot);
        Assert.Equal(1, entity.PriceSnapshot!.RateCardId);
        Assert.Equal(40000m, entity.PriceSnapshot.BaseAmount);
        Assert.Equal(40000m, entity.PriceSnapshot.FinalAmount);
        Assert.Equal(0m, entity.PriceSnapshot.DiscountAmount);
        Assert.Equal(15, entity.PriceSnapshot.BillingGracePeriodMinutes);

        // Snapshot không được echo trong response tạo (use case trả null) nhưng đọc lại qua GET phải có.
        var stored = await fixture.BookingClient.GetFromJsonAsync<BookingDetailJson>($"/api/v1/bookings/{booking.Code}");
        Assert.Equal(40000m, stored!.PriceSnapshot!.FinalAmount);
    }

    [Theory] // Cửa sổ đặt chỗ [now + 15 phút ; now + 30 ngày]: mỗi ràng buộc một dòng InlineData (TC-BOOK-02/03).
    [InlineData(0, "ít nhất 15 phút")]            // đặt cho phút hiện tại
    [InlineData(10, "ít nhất 15 phút")]           // dưới mốc giữ chỗ 15'
    [InlineData(30 * 24 * 60 + 60, "30 ngày")]    // quá hạn đặt trước tối đa 30 ngày
    public async Task Create_booking_rejects_start_outside_the_allowed_window(int startOffsetMinutes, string expectedFragment)
    {
        var response = await PostCreateAsync(TimeSpan.FromMinutes(startOffsetMinutes), TimeSpan.FromHours(2));
        await AssertBadRequestAsync(response, expectedFragment);
    }

    [Fact] // Validate đầu vào đi qua đủ pipeline → ProblemDetails 400 đúng tiêu đề/thông điệp của middleware chung.
    public async Task Create_booking_rejects_invalid_fields_with_problem_details()
    {
        // EndAtUtc không sau StartAtUtc
        await AssertBadRequestAsync(
            await PostCreateAsync(TimeSpan.FromHours(2), TimeSpan.Zero),
            "Thời gian kết thúc phải sau thời gian bắt đầu");

        // Biển số rỗng
        await AssertBadRequestAsync(
            await PostCreateAsync(TimeSpan.FromHours(2), TimeSpan.FromHours(2), plate: "   "),
            "Biển số xe không được để trống");

        // UserId <= 0
        await AssertBadRequestAsync(
            await PostCreateAsync(TimeSpan.FromHours(2), TimeSpan.FromHours(2), userId: 0),
            "UserId không hợp lệ");
        await AssertBadRequestAsync(
            await PostCreateAsync(TimeSpan.FromHours(2), TimeSpan.FromHours(2), userId: -5),
            "UserId không hợp lệ");
    }

    [Fact] // US-029/US-030: sửa giờ + gia hạn đúng trạng thái thì được, và mỗi lần ghi 1 dòng BookingModification.
    public async Task Modify_and_extend_update_the_window_and_persist_audit_records()
    {
        var start = BookingLifecycleFixture.RoundToMinute(DateTime.UtcNow.AddHours(3));
        var booking = await fixture.CreateApprovedBookingAsync(startAtUtc: start, duration: TimeSpan.FromHours(2));
        Assert.Equal("Confirmed", booking.Status);

        // ----- Đổi giờ sang [+6h ; +8h] -----
        var newStart = BookingLifecycleFixture.RoundToMinute(DateTime.UtcNow.AddHours(6));
        var newEnd = newStart.AddHours(2);
        var modifyResponse = await fixture.BookingClient.PutAsJsonAsync($"/api/v1/bookings/{booking.Code}",
            new { userId = DemoIds.Driver1User, newStartAtUtc = newStart, newEndAtUtc = newEnd });
        Assert.Equal(HttpStatusCode.OK, modifyResponse.StatusCode);
        var modified = await modifyResponse.Content.ReadFromJsonAsync<BookingDetailJson>();
        Assert.Equal("Confirmed", modified!.Status);
        AssertCloseTo(newStart, modified.StartAtUtc);
        AssertCloseTo(newEnd, modified.EndAtUtc);

        // ----- Gia hạn thêm 1 giờ -----
        var extendedEnd = newEnd.AddHours(1);
        var extendResponse = await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{booking.Code}/extend",
            new { userId = DemoIds.Driver1User, newEndAtUtc = extendedEnd });
        Assert.Equal(HttpStatusCode.OK, extendResponse.StatusCode);
        var extended = await extendResponse.Content.ReadFromJsonAsync<BookingDetailJson>();
        AssertCloseTo(extendedEnd, extended!.EndAtUtc);

        // ----- Database: 2 dòng ChangeTime với đúng giá trị cũ/mới + log duyệt của chủ bãi -----
        await using var scope = fixture.BookingServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var modifications = await db.BookingModifications.AsNoTracking()
            .Where(m => m.BookingId == booking.Id).OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(2, modifications.Count);
        Assert.All(modifications, m =>
        {
            Assert.Equal(BookingModificationType.ChangeTime, m.ModificationType);
            Assert.Equal(DemoIds.Driver1User, m.RequestedByUserId);
        });
        AssertCloseTo(newStart, modifications[0].NewStartAtUtc!.Value);
        AssertCloseTo(newEnd, modifications[0].NewEndAtUtc!.Value);
        AssertCloseTo(newEnd, modifications[1].OldEndAtUtc!.Value);
        AssertCloseTo(extendedEnd, modifications[1].NewEndAtUtc!.Value);

        var logs = await db.BookingStatusLogs.AsNoTracking().Where(l => l.BookingId == booking.Id).ToListAsync();
        var approvedLog = Assert.Single(logs, l => l.ToStatus == BookingStatus.Confirmed);
        Assert.Equal(BookingStatus.PendingPayment, approvedLog.FromStatus);
        Assert.Equal(DemoIds.OwnerVincom, approvedLog.ChangedByUserId);
        Assert.Equal("Chủ bãi duyệt thành công", approvedLog.Reason);

        // ----- Gia hạn khi còn PendingPayment bị chặn (chỉ CheckedIn/Parking/Confirmed) -----
        var pending = await fixture.CreateBookingAsync();
        await AssertBadRequestAsync(
            await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{pending.Code}/extend",
                new { userId = DemoIds.Driver1User, newEndAtUtc = pending.EndAtUtc.AddHours(1) }),
            "Chỉ có thể gia hạn khi đang đỗ hoặc đã xác nhận");
    }

    [Fact] // Quy tắc chống lách luật (US-029): đặt lúc +59 phút thì tạo hợp lệ nhưng không được sửa vì còn dưới 60 phút.
    public async Task Modifying_within_60_minutes_of_start_is_blocked_by_anti_circumvention_rule()
    {
        var booking = await fixture.CreateBookingAsync(startAtUtc: DateTime.UtcNow.AddMinutes(59));
        var response = await fixture.BookingClient.PutAsJsonAsync($"/api/v1/bookings/{booking.Code}",
            new { userId = DemoIds.Driver1User, newStartAtUtc = DateTime.UtcNow.AddHours(3) });
        await AssertBadRequestAsync(response, "60 phút");
    }

    [Fact] // US-038/UC-16: preview đúng % hoàn theo CancellationWindow 60', hủy xong chặn hủy lần 2, chưa trả tiền thì 0%.
    public async Task Cancellation_window_preview_decide_the_refund()
    {
        // ----- BK-0002: Confirmed, đã trả 63.000đ, giờ bắt đầu còn xa → hủy được, hoàn 100% -----
        var preview = await fixture.BookingClient.GetFromJsonAsync<CancellationPreviewJson>(
            "/api/v1/bookings/BK-0002/cancellation-preview");
        Assert.True(preview!.CanCancel);
        Assert.Equal("Cancelled", preview.ResultStatus);
        Assert.Equal(100, preview.RefundPercent);
        Assert.Equal(63000m, preview.RefundAmount);
        Assert.Contains("hoàn 100%", preview.Reason);

        var cancelResponse = await fixture.BookingClient.PostAsJsonAsync("/api/v1/bookings/BK-0002/cancel",
            new { userId = DemoIds.Driver1User, reason = "Trùng lịch họp" });
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<CancellationResultJson>();
        Assert.Equal("Cancelled", cancelled!.Status);
        Assert.Equal(100, cancelled.RefundPercent);
        Assert.Equal(63000m, cancelled.RefundAmount);

        // Hủy lần 2 trên booking đã Cancelled → 400
        await AssertBadRequestAsync(
            await fixture.BookingClient.PostAsJsonAsync("/api/v1/bookings/BK-0002/cancel", new { userId = DemoIds.Driver1User }),
            "không thể hủy");

        // ----- Booking mới chưa trả tiền → vẫn hủy được nhưng 0% -----
        var pending = await fixture.CreateBookingAsync();
        var pendingPreview = await fixture.BookingClient.GetFromJsonAsync<CancellationPreviewJson>(
            $"/api/v1/bookings/{pending.Code}/cancellation-preview");
        Assert.True(pendingPreview!.CanCancel);
        Assert.Equal(0, pendingPreview.RefundPercent);
        Assert.Equal(0m, pendingPreview.RefundAmount);
        Assert.Contains("Chưa thanh toán", pendingPreview.Reason);

        var pendingCancel = await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{pending.Code}/cancel",
            new { userId = DemoIds.Driver1User, reason = "Không còn nhu cầu" });
        Assert.Equal(HttpStatusCode.OK, pendingCancel.StatusCode);
        var pendingCancelled = await pendingCancel.Content.ReadFromJsonAsync<CancellationResultJson>();
        Assert.Equal("Cancelled", pendingCancelled!.Status);
        Assert.Equal(0, pendingCancelled.RefundPercent);
        Assert.Equal(0m, pendingCancelled.RefundAmount);

        // ----- Database: dấu vết hủy đầy đủ cho BK-0002 -----
        await using var scope = fixture.BookingServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var entity = await db.Bookings.Include(b => b.StatusLogs).AsNoTracking().SingleAsync(b => b.Code == "BK-0002");
        Assert.Equal(BookingStatus.Cancelled, entity.Status);
        Assert.NotNull(entity.CancelledAtUtc);
        Assert.Equal(DemoIds.Driver1User, entity.CancelledByUserId);
        Assert.Equal("Trùng lịch họp", entity.CancelReason);
        Assert.Contains(entity.StatusLogs, l => l.FromStatus == BookingStatus.Confirmed && l.ToStatus == BookingStatus.Cancelled);
    }

    [Fact] // US-031/US-042: duyệt và hủy sự cố gắn tenant OwnerProfileId – chủ bãi khác không đụng được, duyệt 2 lần bị 409.
    public async Task Owner_review_and_lot_cancel_enforce_tenant_ownership_and_state_machine()
    {
        var booking = await fixture.CreateBookingAsync();

        // ----- Tenant khác (TSN) không được duyệt dù cũng là chủ bãi hợp lệ -----
        await AssertBadRequestAsync(
            await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{booking.Code}/approve",
                new { ownerProfileId = DemoIds.OwnerTsn }),
            "Bạn không có quyền duyệt booking của bãi này");

        // Chủ bãi đúng tenant → Confirmed
        var approved = await fixture.ApproveAsync(booking.Code, DemoIds.OwnerVincom);
        Assert.Equal("Confirmed", approved.Status);
        Assert.Contains(approved.History, l => l.ToStatus == "Confirmed" && l.Reason == "Chủ bãi duyệt thành công");

        // Duyệt lần 2 → xung đột trạng thái 409
        var again = await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{booking.Code}/approve",
            new { ownerProfileId = DemoIds.OwnerVincom });
        await AssertConflictAsync(again, "Chỉ duyệt booking ở trạng thái chờ duyệt");

        // ----- Lot-cancel: sai tenant → chặn; đúng tenant → Cancelled với lý do được chuẩn hóa -----
        await AssertBadRequestAsync(
            await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{booking.Code}/lot-cancel",
                new { ownerProfileId = DemoIds.OwnerTsn, reason = "Bãi cháy" }),
            "Bạn không có quyền hủy booking của bãi này");

        var lotCancel = await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{booking.Code}/lot-cancel",
            new { ownerProfileId = DemoIds.OwnerVincom, reason = "Sự cố mất điện toàn bãi" });
        Assert.Equal(HttpStatusCode.OK, lotCancel.StatusCode);
        var lotCancelled = await lotCancel.Content.ReadFromJsonAsync<BookingDetailJson>();
        Assert.Equal("Cancelled", lotCancelled!.Status);
        var cancelLog = Assert.Single(lotCancelled.History, l => l.ToStatus == "Cancelled");
        Assert.StartsWith("Chủ bãi hủy sự cố:", cancelLog.Reason);
        Assert.Contains("Sự cố mất điện toàn bãi", cancelLog.Reason);

        await using var scope = fixture.BookingServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var entity = await db.Bookings.AsNoTracking().SingleAsync(b => b.Code == booking.Code);
        Assert.Equal(BookingStatus.Cancelled, entity.Status);
        Assert.Equal(DemoIds.OwnerVincom, entity.CancelledByUserId);
    }

    [Fact] // Hợp đồng TV7: check-in → check-out đưa booking về Completed; mọi nhảy trạng thái trái phép → 409/400.
    public async Task Gate_actions_drive_checkin_checkout_and_reject_illegal_transitions()
    {
        var booking = await fixture.CreateApprovedBookingAsync();

        // ----- Check-in (Cổng 7, nhân viên Vincom 4) -----
        var checkIn = await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{booking.Code}/check-in",
            new { gateDeviceId = 7, staffUserId = DemoIds.StaffVincomUser });
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        var checkedIn = await checkIn.Content.ReadFromJsonAsync<BookingDetailJson>();
        Assert.Equal("CheckedIn", checkedIn!.Status);
        var checkInLog = Assert.Single(checkedIn.History, l => l.ToStatus == "CheckedIn");
        Assert.Contains("Cổng 7", checkInLog.Reason);
        Assert.Equal(DemoIds.StaffVincomUser, checkInLog.ChangedByUserId);

        // Check-in lần 2 → 409
        await AssertConflictAsync(
            await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{booking.Code}/check-in",
                new { gateDeviceId = 7, staffUserId = DemoIds.StaffVincomUser }),
            "không hợp lệ để vào bãi");

        // ----- Check-out → Completed -----
        var checkOut = await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{booking.Code}/check-out",
            new { gateDeviceId = 7, staffUserId = DemoIds.StaffVincomUser });
        Assert.Equal(HttpStatusCode.OK, checkOut.StatusCode);
        var completed = await checkOut.Content.ReadFromJsonAsync<BookingDetailJson>();
        Assert.Equal("Completed", completed!.Status);
        Assert.Single(completed.History, l => l.ToStatus == "Completed");

        // Check-out lần 2 → 409
        await AssertConflictAsync(
            await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{booking.Code}/check-out",
                new { gateDeviceId = 7, staffUserId = DemoIds.StaffVincomUser }),
            "chưa vào bãi hoặc đã rời bãi");

        // Database: đủ mốc CheckedIn/CheckedOut/Completed
        await using var scope = fixture.BookingServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var entity = await db.Bookings.AsNoTracking().SingleAsync(b => b.Code == booking.Code);
        Assert.Equal(BookingStatus.Completed, entity.Status);
        Assert.NotNull(entity.CheckedInAtUtc);
        Assert.NotNull(entity.CheckedOutAtUtc);
        Assert.NotNull(entity.CompletedAtUtc);

        // ----- No-show (US-039): chưa quá 30 phút giờ hẹn → 400; trạng thái không phải Confirmed → 409 -----
        var tooEarly = await fixture.CreateApprovedBookingAsync();
        await AssertBadRequestAsync(
            await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{tooEarly.Code}/no-show",
                new { gateDeviceId = 1, staffUserId = DemoIds.StaffVincomUser }),
            "Chưa quá 30 phút");

        var neverConfirmed = await fixture.CreateBookingAsync();
        await AssertConflictAsync(
            await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{neverConfirmed.Code}/no-show",
                new { gateDeviceId = 1, staffUserId = DemoIds.StaffVincomUser }),
            "không thể đánh dấu vắng mặt");
    }

    [Fact] // US-023: HoldExpiryWorker quét và tự Expired các giữ chỗ quá hạn 15 phút, kèm log chuẩn trong database.
    public async Task Hold_expiry_worker_releases_overdue_holds()
    {
        var booking = await fixture.CreateBookingAsync();
        Assert.Equal("PendingPayment", booking.Status);

        // Mô phỏng thời gian trôi qua: đẩy mốc giữ chỗ về quá khứ rồi bấm worker chạy ngay 1 vòng quét.
        await using (var arrange = fixture.BookingServices.CreateAsyncScope())
        {
            var db = arrange.ServiceProvider.GetRequiredService<BookingDbContext>();
            var entity = await db.Bookings.SingleAsync(b => b.Code == booking.Code);
            entity.HoldExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var worker = fixture.BookingServices.GetServices<IHostedService>().OfType<HoldExpiryWorker>().Single();
        var expiredCount = await worker.ExpireOverdueBookingsAsync();
        Assert.True(expiredCount >= 1, "Worker phải xử lý ít nhất booking vừa đẩy quá hạn.");

        await using var verify = fixture.BookingServices.CreateAsyncScope();
        var dbVerify = verify.ServiceProvider.GetRequiredService<BookingDbContext>();
        var expired = await dbVerify.Bookings.Include(b => b.StatusLogs).AsNoTracking().SingleAsync(b => b.Code == booking.Code);
        Assert.Equal(BookingStatus.Expired, expired.Status);
        var log = Assert.Single(expired.StatusLogs, l => l.ToStatus == BookingStatus.Expired);
        Assert.Equal("Hết thời gian giữ chỗ 15 phút, tự động nhả vị trí đỗ", log.Reason);
    }

    [Fact] // UC-13: "Booking của tôi" lọc đúng user/status + sắp xếp mới nhất trước; mã lạ → 404 ProblemDetails.
    public async Task Booking_queries_filter_by_user_status_and_unknown_code_returns_404()
    {
        // ----- Mã không tồn tại → 404 + ProblemDetails chuẩn từ middleware dùng chung -----
        var missing = await fixture.BookingClient.GetAsync("/api/v1/bookings/BK-9999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var problem = await missing.Content.ReadFromJsonAsync<ProblemJson>();
        Assert.Equal("Không tìm thấy", problem!.Title);
        Assert.Contains("BK-9999", problem.Detail);

        // ----- Dữ liệu seed: user 5 có BK-0001 và BK-0002 -----
        var mine = await fixture.BookingClient.GetFromJsonAsync<List<BookingSummaryJson>>("/api/v1/bookings?userId=5");
        Assert.Contains(mine!, b => b.Code == "BK-0001");
        Assert.Contains(mine!, b => b.Code == "BK-0002");

        // ----- 2 booking mới cho user ảo 9 → lọc status + thứ tự StartAtUtc giảm dần -----
        var newer = await fixture.CreateBookingAsync(userId: 9, startAtUtc: DateTime.UtcNow.AddHours(5), plateNumber: "60X99999");
        var older = await fixture.CreateBookingAsync(userId: 9, startAtUtc: DateTime.UtcNow.AddHours(3), plateNumber: "60X88888");

        var listUser9 = await fixture.BookingClient.GetFromJsonAsync<List<BookingSummaryJson>>("/api/v1/bookings?userId=9");
        Assert.Equal(2, listUser9!.Count);
        Assert.Equal(newer.Code, listUser9[0].Code); // StartAtUtc desc
        Assert.Equal(older.Code, listUser9[1].Code);

        var cancelOlder = await fixture.BookingClient.PostAsJsonAsync($"/api/v1/bookings/{older.Code}/cancel", new { userId = 9 });
        Assert.Equal(HttpStatusCode.OK, cancelOlder.StatusCode);

        var onlyPending = await fixture.BookingClient.GetFromJsonAsync<List<BookingSummaryJson>>(
            "/api/v1/bookings?userId=9&status=PendingPayment");
        Assert.Equal(newer.Code, Assert.Single(onlyPending!).Code);

        var onlyCancelled = await fixture.BookingClient.GetFromJsonAsync<List<BookingSummaryJson>>(
            "/api/v1/bookings?userId=9&status=Cancelled");
        Assert.Equal(older.Code, Assert.Single(onlyCancelled!).Code);

        // ----- UserId không hợp lệ → 400 -----
        await AssertBadRequestAsync(
            await fixture.BookingClient.GetAsync("/api/v1/bookings?userId=-1"),
            "UserId phải là số nguyên dương");
    }

    [Fact] // US-053/US-054: đăng ký bãi mới → ẩn với tài xế khi chưa duyệt → nộp KYB → duyệt → hiện trong tìm kiếm → đặt booking đầu tiên.
    public async Task New_lot_from_registration_through_kyb_to_first_booking()
    {
        // ----- 1. Chủ bãi đăng ký bãi mới (PendingApproval) -----
        var lotName = $"Bãi IT {Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var createLot = await fixture.ParkingClient.PostAsJsonAsync("/api/v1/parking-lots", new
        {
            ownerProfileId = DemoIds.OwnerVincom,
            name = lotName,
            address = "9 Đường Tích hợp, Q.1, TP.HCM",
            city = "TP.HCM",
            district = "Q.1",
            latitude = 10.7620,
            longitude = 106.7030,
            totalSlots = 30, // ≤ 50 chỗ nên bước 4 (khảo sát thực địa) không bắt buộc
            maxHeightCm = 200,
            openTime = "06:00:00",
            closeTime = "22:00:00",
            integrationTier = "Manual",
            description = "Bãi đỗ tạo bởi integration test",
        });
        Assert.Equal(HttpStatusCode.Created, createLot.StatusCode);
        var lotId = (await createLot.Content.ReadFromJsonAsync<CreatedLotJson>())!.Id;

        // ----- 2. Chưa Active → tài xế không nhìn thấy (khớp "Pending_lot_returns_404") -----
        Assert.Equal(HttpStatusCode.NotFound,
            (await fixture.ParkingClient.GetAsync($"/api/v1/parking-lots/{lotId}")).StatusCode);

        // Hợp đồng Số 4: cây phân cấp đọc được cho tích hợp dù chưa duyệt, và bãi mới chưa có zone nào
        var emptyHierarchy = await fixture.ParkingClient.GetFromJsonAsync<LotHierarchyJson>($"/api/v1/parking-lots/{lotId}/hierarchy");
        Assert.Empty(emptyHierarchy!.Zones);

        // ----- 3. Nộp hồ sơ KYB 4 bước (GPS của ảnh trùng tọa độ bãi nên không vượt bán kính 500m) -----
        var submitKyb = await fixture.ParkingClient.PostAsJsonAsync($"/api/v1/parking-lots/{lotId}/kyb/submit", new
        {
            ownerProfileId = DemoIds.OwnerVincom,
            businessLicenseUrl = "https://docs.it.test/gpkd.pdf",
            sitePhotoUrls = new[] { "https://photos.it.test/mat-tien.jpg" },
            photoLatitude = 10.7620,
            photoLongitude = 106.7030,
            fireSafetyCertificateUrl = "https://docs.it.test/pccc.pdf",
        });
        Assert.Equal(HttpStatusCode.OK, submitKyb.StatusCode);
        var kyb = await submitKyb.Content.ReadFromJsonAsync<KybJson>();
        Assert.Equal("Submitted", kyb!.Status);
        Assert.False(kyb.FieldSurveyRequired);
        Assert.NotNull(kyb.SubmittedAtUtc);

        // Nộp lại khi đang thẩm định → 409
        var resubmit = await fixture.ParkingClient.PostAsJsonAsync($"/api/v1/parking-lots/{lotId}/kyb/submit", new
        {
            ownerProfileId = DemoIds.OwnerVincom,
            businessLicenseUrl = "https://docs.it.test/gpkd.pdf",
            sitePhotoUrls = new[] { "https://photos.it.test/mat-tien.jpg" },
            photoLatitude = 10.7620,
            photoLongitude = 106.7030,
            fireSafetyCertificateUrl = "https://docs.it.test/pccc.pdf",
        });
        await AssertConflictAsync(resubmit, "không thể nộp lại");

        // ----- 4. Admin duyệt KYB (domain method Activate) → bãi công khai -----
        await using (var arrange = fixture.ParkingServices.CreateAsyncScope())
        {
            var parkingDb = arrange.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var lot = await parkingDb.ParkingLots.SingleAsync(l => l.Id == lotId);
            lot.Activate();
            await parkingDb.SaveChangesAsync();
        }

        var visible = await fixture.ParkingClient.GetFromJsonAsync<LotDetailJson>($"/api/v1/parking-lots/{lotId}");
        Assert.Equal("Active", visible!.Status);
        Assert.Equal(lotName, visible.Name);
        Assert.Equal(30, visible.TotalSlots);

        // Xuất hiện trong tìm kiếm quanh tọa độ của nó (Vincom cách ~1,8km, các bãi khác không Active)
        var search = await fixture.ParkingClient.GetFromJsonAsync<List<LotSearchItemJson>>(
            "/api/v1/parking-lots/search?lat=10.7620&lng=106.7030&radiusKm=1");
        Assert.Equal(lotId, Assert.Single(search!).Id);

        // ----- 5. Booking đầu tiên trên bãi mới qua BookingService (snapshot tên bãi khớp chéo service) -----
        var booking = await fixture.CreateBookingAsync(
            plateNumber: " 51a88888 ",
            parkingLotId: lotId,
            parkingLotName: lotName,
            zoneId: null,
            slotId: null,
            slotCode: null,
            startAtUtc: BookingLifecycleFixture.RoundToMinute(DateTime.UtcNow.AddHours(26)));
        Assert.Equal(lotId, booking.ParkingLotId);
        Assert.Equal(lotName, booking.ParkingLotName);
        Assert.Equal("51A88888", booking.PlateNumber); // plate được trim + upper tại biên API

        var stored = await fixture.BookingClient.GetFromJsonAsync<BookingDetailJson>($"/api/v1/bookings/{booking.Code}");
        Assert.Equal(lotId, stored!.ParkingLotId);
        Assert.Equal(lotName, stored.ParkingLotName);
        Assert.Equal("PendingPayment", stored.Status);
    }

    // ===================== Helper cục bộ của test class =====================

    /// <summary>POST /api/v1/bookings thô (dùng cho các case 400) — các trường hợp lệ đặt theo mặc định fixture.</summary>
    private Task<HttpResponseMessage> PostCreateAsync(TimeSpan startOffset, TimeSpan duration,
        string plate = "51A88888", int userId = DemoIds.Driver1User)
        => fixture.BookingClient.PostAsJsonAsync("/api/v1/bookings", new
        {
            userId,
            vehicleId = DemoIds.Driver1Car,
            plateNumber = plate,
            vehicleType = "Sedan",
            parkingLotId = DemoIds.LotVincom,
            ownerProfileId = DemoIds.OwnerVincom,
            parkingLotName = "Bãi xe Vincom Đồng Khởi",
            zoneId = DemoIds.ZoneVincomB,
            slotId = (int?)2,
            slotCode = (string?)"B1-A02",
            allocationMode = "Dynamic",
            startAtUtc = DateTime.UtcNow + startOffset,
            endAtUtc = DateTime.UtcNow + startOffset + duration,
            totalAmount = 40000m,
        });

    /// <summary>Assert 400 + ProblemDetails với tiêu đề "Dữ liệu không hợp lệ" và đúng mảnh thông điệp nghiệp vụ.</summary>
    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string detailFragment)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>();
        Assert.Equal("Dữ liệu không hợp lệ", problem!.Title);
        Assert.Contains(detailFragment, problem.Detail);
    }

    /// <summary>Assert 409 + ProblemDetails với tiêu đề "Xung đột nghiệp vụ" (ConflictException từ Domain).</summary>
    private static async Task AssertConflictAsync(HttpResponseMessage response, string detailFragment)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>();
        Assert.Equal("Xung đột nghiệp vụ", problem!.Title);
        Assert.Contains(detailFragment, problem.Detail);
    }

    /// <summary>So thời gian với dung sai (JSON round-trip ms ↔ timestamptz microseconds).</summary>
    private static void AssertCloseTo(DateTime expected, DateTime actual, TimeSpan? tolerance = null)
    {
        var allowed = tolerance ?? TimeSpan.FromSeconds(1);
        Assert.True((actual - expected).Duration() <= allowed,
            $"Expected ~{expected:O} (±{allowed.TotalSeconds}s) but was {actual:O}");
    }
}