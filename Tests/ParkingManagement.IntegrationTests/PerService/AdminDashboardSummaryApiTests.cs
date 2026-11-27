using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using GateService.API.Controllers;
using GateService.Application.Features.AdminDashboard;
using GateService.Application.Features.GateConsole;
using GateService.Domain.Entities;
using GateService.Domain.Enums;
using GateService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.API.Controllers;
using ParkingService.Application.Features.AdminDashboard;
using ParkingService.Domain.Entities;
using ParkingService.Infrastructure.Persistence;
using SupportService.API.Controllers;
using SupportService.Application.Features.AdminDashboard;
using SupportService.Domain.Entities;
using SupportService.Infrastructure.Persistence;
using SupportService.Infrastructure.Workers;
using GateLotCapacityConfig = GateService.Domain.Entities.LotCapacityConfig;
using ParkingLotCapacityConfig = ParkingService.Domain.Entities.LotCapacityConfig;

namespace ParkingManagement.IntegrationTests.PerService;

/// <summary>
/// US-081: endpoint tổng hợp chỉ-đọc cho Admin Dashboard ở GateService, SupportService, ParkingService.
/// Mỗi service: Admin → 200 với số đúng trên dữ liệu nhiều bãi / nhiều chủ bãi; role khác → 403; không token → 401;
/// endpoint cũ vẫn chạy như trước (không token) và controller cũ không bị gắn thêm [Authorize].
/// </summary>
internal static class AdminDashboardTestSupport
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Program.cs nạp Keys/public.key thật; host test kiểm tra token tự mint bằng public key của cặp khóa test.</summary>
    public static void UseTestSigningKey(IServiceCollection services, RsaTestKeys keys)
        => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(keys.PublicKeyPath));
            options.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false));
        });

    public static HttpRequestMessage Get(string path, RsaTestKeys keys, params string[] roles)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (roles.Length > 0)
            request.Headers.Authorization = new("Bearer", keys.MintToken(DemoIds.AdminUser, "dashboard@it.test", null, roles));
        return request;
    }

    /// <summary>Controller mới: [Authorize(Policy = "RequireAdmin")] ở cấp class và chỉ có action GET.</summary>
    public static void AssertAdminReadOnlyController(Type controller)
    {
        var authorize = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();
        Assert.Single(authorize);
        Assert.Equal("RequireAdmin", authorize[0].Policy);
        var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.NotEmpty(actions);
        Assert.All(actions, a => Assert.All(a.GetCustomAttributes<HttpMethodAttribute>(), h => Assert.Equal(new[] { "GET" }, h.HttpMethods)));
    }

    /// <summary>Controller cũ cùng prefix: không có [Authorize] ở cấp class (hành vi ẩn danh giữ nguyên).</summary>
    public static void AssertNoClassLevelAuthorize(params Type[] controllers)
        => Assert.All(controllers, c => Assert.Empty(c.GetCustomAttributes<AuthorizeAttribute>(inherit: true)));
}

// ======================= GateService =======================

/// <summary>Host GateService riêng (database pm_it_gate_* riêng) – dữ liệu demo của Gate bị xóa và thay bằng dữ liệu của test.</summary>
public sealed class GateAdminDashboardFixture : ServiceHostFixture<GateController, GateDbContext>, IAsyncLifetime
{
    public RsaTestKeys Keys { get; } = RsaTestKeys.Create();

    public GateAdminDashboardFixture()
    {
        ExtraServices = services =>
        {
            services.RemoveAll<IGateBookingClient>();
            services.AddSingleton<IGateBookingClient, StubGateBookingClient>();
            services.RemoveAll<IExitFeeCalculator>();
            services.RemoveAll<IGateSettlementRecorder>();
            services.AddSingleton<StubPaymentService>();
            services.AddSingleton<IExitFeeCalculator>(sp => sp.GetRequiredService<StubPaymentService>());
            services.AddSingleton<IGateSettlementRecorder>(sp => sp.GetRequiredService<StubPaymentService>());
            AdminDashboardTestSupport.UseTestSigningKey(services, Keys);
        };
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GateDbContext>();
        await db.GateEvents.ExecuteDeleteAsync();
        await db.ParkingSessions.ExecuteDeleteAsync();
        await db.GateDevices.ExecuteDeleteAsync();
        await db.LotCapacityConfigs.ExecuteDeleteAsync();

        var entry = DateTime.UtcNow.AddHours(-2);
        ParkingSession Session(string code, int lot, int owner, bool walkIn, ParkingSessionStatus status = ParkingSessionStatus.Active) => new()
        {
            Code = code, ParkingLotId = lot, OwnerProfileId = owner, PlateNumber = $"51{code}", IsWalkIn = walkIn, EntryAtUtc = entry,
            CheckInMethod = walkIn ? GateMethod.Manual : GateMethod.Qr, Status = status,
            AdmissionType = walkIn ? AdmissionType.Transient : AdmissionType.Subscriber,
            ExitAtUtc = status == ParkingSessionStatus.Completed ? entry.AddHours(1) : null,
        };
        // Bãi 1 (chủ bãi Vincom): 2 vãng lai + 3 có booking đang đỗ, 1 lượt đã ra. Bãi 2 (chủ bãi TSN): 1 có booking + 1 bất thường.
        db.ParkingSessions.AddRange(
            Session("IT001", DemoIds.LotVincom, DemoIds.OwnerVincom, walkIn: true),
            Session("IT002", DemoIds.LotVincom, DemoIds.OwnerVincom, walkIn: true),
            Session("IT003", DemoIds.LotVincom, DemoIds.OwnerVincom, walkIn: false),
            Session("IT004", DemoIds.LotVincom, DemoIds.OwnerVincom, walkIn: false),
            Session("IT005", DemoIds.LotVincom, DemoIds.OwnerVincom, walkIn: false),
            Session("IT006", DemoIds.LotVincom, DemoIds.OwnerVincom, walkIn: false, ParkingSessionStatus.Completed),
            Session("IT007", DemoIds.LotTsn, DemoIds.OwnerTsn, walkIn: false),
            Session("IT008", DemoIds.LotTsn, DemoIds.OwnerTsn, walkIn: true, ParkingSessionStatus.Abnormal));
        // Sức chứa US-066: bãi 1 và bãi 3 (bãi 3 chưa có xe), bãi 2 chưa cấu hình.
        db.LotCapacityConfigs.AddRange(
            new GateLotCapacityConfig { ParkingLotId = DemoIds.LotVincom, Capacity = 100, TransientBuffer = 10, SubscriberQuota = 20 },
            new GateLotCapacityConfig { ParkingLotId = DemoIds.LotLandmark, Capacity = 40, TransientBuffer = 5, SubscriberQuota = 5 });
        GateDevice Device(string code, int lot, GateDeviceType type, GateDeviceStatus status, bool active = true)
            => new() { Code = code, Name = code, ParkingLotId = lot, DeviceType = type, Status = status, IsActive = active };
        db.GateDevices.AddRange(
            Device("IT-QR-1", DemoIds.LotVincom, GateDeviceType.QrScanner, GateDeviceStatus.Online),
            Device("IT-QR-2", DemoIds.LotVincom, GateDeviceType.QrScanner, GateDeviceStatus.Online),
            Device("IT-QR-3", DemoIds.LotVincom, GateDeviceType.QrScanner, GateDeviceStatus.Offline),
            Device("IT-CAM-1", DemoIds.LotVincom, GateDeviceType.AnprCamera, GateDeviceStatus.Maintenance),
            Device("IT-BAR-1", DemoIds.LotTsn, GateDeviceType.Barrier, GateDeviceStatus.Online),
            Device("IT-BAR-OLD", DemoIds.LotTsn, GateDeviceType.Barrier, GateDeviceStatus.Offline, active: false));
        await db.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        Keys.Dispose();
    }
}

[CollectionDefinition("Gate admin dashboard API")]
public sealed class GateAdminDashboardCollection : ICollectionFixture<GateAdminDashboardFixture>;

[Collection("Gate admin dashboard API")]
public class GateAdminDashboardApiTests(GateAdminDashboardFixture fixture)
{
    private Task<HttpResponseMessage> SendAsync(string path, params string[] roles)
        => fixture.Client.SendAsync(AdminDashboardTestSupport.Get(path, fixture.Keys, roles));

    [Fact]
    public async Task Occupancy_Admin_ReturnsActiveSessionsPerLot()
    {
        using var response = await SendAsync("/api/v1/gate/admin/occupancy", "Admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AdminGateOccupancyDto>(AdminDashboardTestSupport.Json);
        Assert.NotNull(body);
        Assert.Equal((6, 2, 4, 1), (body.Total, body.WalkIn, body.Booked, body.Abnormal));
        Assert.Equal(
        [
            new AdminLotOccupancyRowDto(DemoIds.LotVincom, 5, 2, 3, 100),
            new AdminLotOccupancyRowDto(DemoIds.LotTsn, 1, 0, 1, null),
            new AdminLotOccupancyRowDto(DemoIds.LotLandmark, 0, 0, 0, 40),
        ], body.Lots);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("plate", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Devices_Admin_ReturnsTypeByStatusMatrix_OfActiveDevices()
    {
        using var response = await SendAsync("/api/v1/gate/admin/devices", "Admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AdminGateDevicesDto>(AdminDashboardTestSupport.Json);
        Assert.NotNull(body);
        Assert.Equal((3, 1, 1), (body.Online, body.Offline, body.Maintenance));
        Assert.Equal(
        [
            new AdminGateDeviceTypeRowDto(GateDeviceType.AnprCamera, 0, 0, 1),
            new AdminGateDeviceTypeRowDto(GateDeviceType.Barrier, 1, 0, 0),
            new AdminGateDeviceTypeRowDto(GateDeviceType.QrScanner, 2, 1, 0),
        ], body.ByType);
    }

    [Theory]
    [InlineData("/api/v1/gate/admin/occupancy", "LotOwner")]
    [InlineData("/api/v1/gate/admin/occupancy", "Driver")]
    [InlineData("/api/v1/gate/admin/devices", "Staff")]
    public async Task NonAdmin_Gets403(string path, string role)
    {
        using var response = await SendAsync(path, role);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/gate/admin/occupancy")]
    [InlineData("/api/v1/gate/admin/devices")]
    public async Task NoToken_Gets401(string path)
    {
        using var response = await SendAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/parking-sessions?parkingLotId=1&status=Active")]
    [InlineData("/api/v1/gate-devices?parkingLotId=1")]
    public async Task ExistingEndpoints_StillAnonymous(string path)
    {
        using var response = await SendAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Controllers_OnlyNewOneRequiresAdmin()
    {
        AdminDashboardTestSupport.AssertAdminReadOnlyController(typeof(AdminGateDashboardController));
        AdminDashboardTestSupport.AssertNoClassLevelAuthorize(typeof(GateController), typeof(ParkingSessionsController),
            typeof(GateDevicesController), typeof(GateService.API.Controllers.LotCapacityController));
    }
}

// ======================= SupportService =======================

/// <summary>Host SupportService riêng; tắt worker escalate để số "chờ chủ bãi quá hạn" không đổi trong lúc test.</summary>
public sealed class SupportAdminDashboardFixture : ServiceHostFixture<ComplaintsController, SupportDbContext>, IAsyncLifetime
{
    public RsaTestKeys Keys { get; } = RsaTestKeys.Create();

    public SupportAdminDashboardFixture()
    {
        ExtraServices = services =>
        {
            var worker = services.Where(d => d.ServiceType == typeof(IHostedService)
                                            && d.ImplementationType == typeof(ComplaintEscalationWorker)).ToList();
            foreach (var descriptor in worker) services.Remove(descriptor);
            AdminDashboardTestSupport.UseTestSigningKey(services, Keys);
        };
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        await db.ComplaintMessages.ExecuteDeleteAsync();
        await db.Complaints.ExecuteDeleteAsync();

        var now = DateTime.UtcNow;
        Complaint Ticket(string code, ComplaintStatus status, int? owner, int lot, DateTime? dueAtUtc = null,
            ComplaintCategory category = ComplaintCategory.Overcharge) => new()
        {
            Code = code, UserId = DemoIds.Driver1User, ParkingLotId = lot, OwnerProfileId = owner, Category = category,
            Description = "Dữ liệu integration test US-081.", Status = status, OwnerResponseDueAtUtc = dueAtUtc,
        };
        db.Complaints.AddRange(
            Ticket("CP-IT81-01", ComplaintStatus.Open, DemoIds.OwnerVincom, DemoIds.LotVincom),
            Ticket("CP-IT81-02", ComplaintStatus.Open, null, DemoIds.LotTsn, category: ComplaintCategory.AppError),
            Ticket("CP-IT81-03", ComplaintStatus.AwaitingOwner, DemoIds.OwnerVincom, DemoIds.LotVincom, now.AddHours(30)),
            Ticket("CP-IT81-04", ComplaintStatus.AwaitingOwner, DemoIds.OwnerTsn, DemoIds.LotTsn, now.AddHours(1)),
            Ticket("CP-IT81-05", ComplaintStatus.AwaitingOwner, DemoIds.OwnerTsn, DemoIds.LotTsn, now.AddHours(-3)),
            Ticket("CP-IT81-06", ComplaintStatus.OwnerResponded, DemoIds.OwnerTsn, DemoIds.LotBenThanh),
            Ticket("CP-IT81-07", ComplaintStatus.Escalated, DemoIds.OwnerVincom, DemoIds.LotVincom, now.AddHours(-60)),
            Ticket("CP-IT81-08", ComplaintStatus.Resolved, DemoIds.OwnerVincom, DemoIds.LotVincom),
            Ticket("CP-IT81-09", ComplaintStatus.Rejected, DemoIds.OwnerTsn, DemoIds.LotTsn));
        await db.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        Keys.Dispose();
    }
}

[CollectionDefinition("Support admin dashboard API")]
public sealed class SupportAdminDashboardCollection : ICollectionFixture<SupportAdminDashboardFixture>;

[Collection("Support admin dashboard API")]
public class SupportAdminDashboardApiTests(SupportAdminDashboardFixture fixture)
{
    private const string Path = "/api/v1/complaints/admin/summary";

    private Task<HttpResponseMessage> SendAsync(string path, params string[] roles)
        => fixture.Client.SendAsync(AdminDashboardTestSupport.Get(path, fixture.Keys, roles));

    [Fact]
    public async Task Summary_Admin_CountsUnresolvedAcrossOwners()
    {
        using var response = await SendAsync(Path, "Admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AdminComplaintSummaryDto>(AdminDashboardTestSupport.Json);
        Assert.NotNull(body);
        Assert.Equal((2, 3, 1, 1, 1), (body.Open, body.AwaitingOwner, body.AwaitingOwnerOverdue, body.OwnerResponded, body.Escalated));
        Assert.Equal(7, body.Unresolved);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CP-IT81", raw);
    }

    [Theory]
    [InlineData("LotOwner")]
    [InlineData("Driver")]
    public async Task NonAdmin_Gets403(string role)
    {
        using var response = await SendAsync(Path, role);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NoToken_Gets401()
    {
        using var response = await SendAsync(Path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/faqs")]
    [InlineData("/api/v1/complaints/my?userId=5")]
    [InlineData("/api/v1/complaints?ownerProfileId=1")]
    public async Task ExistingEndpoints_StillAnonymous(string path)
    {
        using var response = await SendAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Controllers_OnlyNewOneRequiresAdmin()
    {
        AdminDashboardTestSupport.AssertAdminReadOnlyController(typeof(AdminComplaintsDashboardController));
        AdminDashboardTestSupport.AssertNoClassLevelAuthorize(typeof(ComplaintsController), typeof(FaqsController));
    }
}

// ======================= ParkingService =======================

/// <summary>Host ParkingService riêng; thêm 3 bãi của test (1 bãi đã xóa mềm) bên cạnh dữ liệu demo.</summary>
public sealed class ParkingAdminDashboardFixture : ServiceHostFixture<ParkingLotsController, ParkingDbContext>, IAsyncLifetime
{
    public RsaTestKeys Keys { get; } = RsaTestKeys.Create();
    public int LotAId { get; private set; }
    public int LotBId { get; private set; }
    public int DeletedLotId { get; private set; }

    public ParkingAdminDashboardFixture() => ExtraServices = services => AdminDashboardTestSupport.UseTestSigningKey(services, Keys);

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();

        ParkingLot Lot(string name, int slots, ParkingLotStatus status, int owner, IntegrationTier tier)
            => new(0, name, "Địa chỉ test", 10.77, 106.70, slots, slots, 200, new TimeOnly(6, 0), new TimeOnly(22, 0), status, owner, tier,
                hotlinePhone: "0900000000");
        var a = Lot("IT81 Bãi A", 120, ParkingLotStatus.Active, DemoIds.OwnerVincom, IntegrationTier.PmsApi);
        a.SetLocation("TP.HCM", "Quận 3");
        var b = Lot("IT81 Bãi B", 40, ParkingLotStatus.Suspended, DemoIds.OwnerTsn, IntegrationTier.Manual);
        var deleted = Lot("IT81 Bãi C", 10, ParkingLotStatus.Active, DemoIds.OwnerTsn, IntegrationTier.Manual);
        deleted.IsDeleted = true;
        db.ParkingLots.AddRange(a, b, deleted);
        await db.SaveChangesAsync();

        db.LotCapacityConfigs.AddRange(
            new ParkingLotCapacityConfig { ParkingLotId = a.Id, OnlineQuota = 80, IsEmergencyStopped = true },
            new ParkingLotCapacityConfig { ParkingLotId = b.Id, OnlineQuota = 20, InstantBookingPaused = true });
        await db.SaveChangesAsync();
        (LotAId, LotBId, DeletedLotId) = (a.Id, b.Id, deleted.Id);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        Keys.Dispose();
    }
}

[CollectionDefinition("Parking admin dashboard API")]
public sealed class ParkingAdminDashboardCollection : ICollectionFixture<ParkingAdminDashboardFixture>;

[Collection("Parking admin dashboard API")]
public class ParkingAdminDashboardApiTests(ParkingAdminDashboardFixture fixture)
{
    private const string Path = "/api/v1/parking-lots/admin/lots-summary";

    private Task<HttpResponseMessage> SendAsync(string path, params string[] roles)
        => fixture.Client.SendAsync(AdminDashboardTestSupport.Get(path, fixture.Keys, roles));

    [Fact]
    public async Task LotsSummary_Admin_ReturnsNonDeletedLotsWithFlags_WithoutOwnerOrContact()
    {
        using var response = await SendAsync(Path, "Admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AdminLotsSummaryDto>(AdminDashboardTestSupport.Json);
        Assert.NotNull(body);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            Assert.Equal(await db.ParkingLots.CountAsync(p => !p.IsDeleted), body.Lots.Count);
        }
        Assert.DoesNotContain(body.Lots, l => l.ParkingLotId == fixture.DeletedLotId);

        var a = Assert.Single(body.Lots, l => l.ParkingLotId == fixture.LotAId);
        Assert.Equal(("IT81 Bãi A", "TP.HCM", "Quận 3", ParkingLotStatus.Active, IntegrationTier.PmsApi, 120, true, false),
            (a.Name, a.City, a.District, a.Status, a.IntegrationTier, a.TotalSlots, a.IsEmergencyStopped, a.InstantBookingPaused));
        var b = Assert.Single(body.Lots, l => l.ParkingLotId == fixture.LotBId);
        Assert.Equal((ParkingLotStatus.Suspended, 40, false, true), (b.Status, b.TotalSlots, b.IsEmergencyStopped, b.InstantBookingPaused));

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ownerProfileId", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0900000000", raw);
    }

    [Theory]
    [InlineData("LotOwner")]
    [InlineData("Driver")]
    public async Task NonAdmin_Gets403(string role)
    {
        using var response = await SendAsync(Path, role);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NoToken_Gets401()
    {
        using var response = await SendAsync(Path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExistingLotDetail_StillAnonymous()
    {
        using var response = await SendAsync($"/api/v1/parking-lots/{DemoIds.LotVincom}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Controllers_OnlyNewOneRequiresAdmin()
    {
        AdminDashboardTestSupport.AssertAdminReadOnlyController(typeof(AdminParkingLotsDashboardController));
        AdminDashboardTestSupport.AssertNoClassLevelAuthorize(typeof(ParkingLotsController), typeof(ParkingLotStructureController),
            typeof(FloorLayoutsController), typeof(ParkingService.API.Controllers.LotCapacityController));
    }
}
