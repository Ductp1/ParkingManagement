using ParkingManagement.ServiceDefaults;
using GateService.Domain.Entities;
using GateService.Application.Features.Shifts;
using GateService.Application.Features.LotCapacity;
using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GateService.API.Controllers;
using GateService.Application.Features.GateConsole;
using GateService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace ParkingManagement.IntegrationTests.PerService;

/// <summary>Collection: GateService chạy host thật trên database pm_it_gate_* riêng của từng lần chạy.</summary>
[CollectionDefinition("Gate API")]
public sealed class GateApiCollection : ICollectionFixture<GateServiceHostFixture>;

/// <summary>
/// Host GateService cho per-service test: BookingService (TV3) và PaymentService (TV4) KHÔNG chạy cùng
/// host này, nên các port liên dịch vụ của UC-21/UC-22 được thay bằng stub trong bộ nhớ
/// (booking stub luôn Confirmed, bắt đầu sau 1 tiếng → không kích hoạt nhánh grace/late note).
/// </summary>
public sealed class GateServiceHostFixture : ServiceHostFixture<GateController, GateDbContext>, IAsyncLifetime
{
    public RsaTestKeys Keys { get; } = RsaTestKeys.Create();

    /// <summary>Client mang JWT đúng hợp đồng UserService (kiểm phiên từ xa được gỡ trong test).</summary>
    public HttpClient As(int userId, string role, int? ownerProfileId = null, int? parkingLotId = null)
    {
        var claims = new List<Claim>();
        if (ownerProfileId is { } owner) claims.Add(new Claim("OwnerProfileId", owner.ToString()));
        if (parkingLotId is { } lot) claims.Add(new Claim("ParkingLotId", lot.ToString()));
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Keys.MintToken(userId, $"u{userId}@it.test", null, claims, role));
        return client;
    }

    /// <summary>Nhân viên Vincom đang có ca mở – mọi thao tác ghi ở cổng cần ca này.</summary>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GateDbContext>();
        var now = DateTime.UtcNow;
        db.Shifts.Add(new Shift
        {
            ParkingLotId = DemoIds.LotVincom, OwnerProfileId = DemoIds.OwnerVincom, StaffUserId = DemoIds.StaffVincomUser,
            ScheduledStartUtc = now.AddHours(-1), ScheduledEndUtc = now.AddHours(8), StartedAtUtc = now.AddHours(-1),
            BusinessDate = Shift.BusinessDateOf(now), Status = ShiftStatus.Open,
        });
        await db.SaveChangesAsync();
        var staff = As(DemoIds.StaffVincomUser, "Staff", DemoIds.OwnerVincom, DemoIds.LotVincom);
        Client.DefaultRequestHeaders.Authorization = staff.DefaultRequestHeaders.Authorization;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        Keys.Dispose();
    }

    public GateServiceHostFixture()
    {
        ExtraServices = services =>
        {
            services.RemoveAll<IGateBookingClient>();
            services.AddSingleton<StubGateBookingClient>();
            services.AddSingleton<IGateBookingClient>(sp => sp.GetRequiredService<StubGateBookingClient>());

            services.RemoveAll<IExitFeeCalculator>();
            services.RemoveAll<IGateSettlementRecorder>();
            services.AddSingleton<StubPaymentService>();
            services.AddSingleton<IExitFeeCalculator>(sp => sp.GetRequiredService<StubPaymentService>());
            services.AddSingleton<IGateSettlementRecorder>(sp => sp.GetRequiredService<StubPaymentService>());

            // US-076: không có UserService / ParkingService trong host này.
            AdminDashboardTestSupport.UseTestSigningKey(services, Keys);
            services.RemoveAll<ITokenPrincipalValidator>();
            services.RemoveAll<ILotDirectory>();
            services.AddSingleton<ILotDirectory, StubLotDirectory>();
            services.RemoveAll<IStaffDirectory>();
            services.AddSingleton<IStaffDirectory, StubStaffDirectory>();
            services.RemoveAll<ILotOperationalStatusProvider>();
            services.AddSingleton<ILotOperationalStatusProvider, StubLotStatus>();
            services.RemoveAll<IShiftCashSource>();
            services.AddSingleton<IShiftCashSource>(sp => sp.GetRequiredService<StubPaymentService>());
        };
    }
}

/// <summary>
/// Integration test GateService (TV7): cổng vào/ra T-702 và hợp đồng QR với BookingService T-704.
/// Token QR mint đúng định dạng từng byte của QrTokenService (payload "code|plate|lot|expiryTicks",
/// HMAC-SHA256 Base64) bằng chính QRTOKEN_SECRET của host → xác thực thật qua BookingQrTokenValidator.
/// </summary>
[Collection("Gate API")]
public class GateServiceApiTests(GateServiceHostFixture fixture)
{
    /// <summary>Mint token QR theo đúng hợp đồng T-704 với secret của host đang chạy.</summary>
    private static string MintQrToken(string bookingCode, string plate, int parkingLotId, DateTime expiresAtUtc)
    {
        var payload = $"{bookingCode}|{plate}|{parkingLotId}|{expiresAtUtc.Ticks}";
        var secret = Environment.GetEnvironmentVariable("QRTOKEN_SECRET")
            ?? throw new InvalidOperationException("Thiếu QRTOKEN_SECRET cho test.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        return $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))}.{hash}";
    }

    private static string Tamper(string token)
    {
        var parts = token.Split('.');
        var bytes = Convert.FromBase64String(parts[1]);
        bytes[0] ^= 0xFF;
        return $"{parts[0]}.{Convert.ToBase64String(bytes)}";
    }

    [Fact] // T-704: check-in bằng QR hợp lệ → lượt gửi Active gắn booking; GateEvents ghi audit "Check-in theo booking".
    public async Task Qr_checkin_creates_a_booking_session_with_audit_event()
    {
        var expires = DateTime.UtcNow.AddHours(4);
        var token = MintQrToken("BK-IT-GATE-1", "51A88888", DemoIds.LotVincom, expires);
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-in", new
        {
            parkingLotId = DemoIds.LotVincom,
            method = "Qr",
            plate = "51A88888",
            qrToken = token,
            staffUserId = DemoIds.StaffVincomUser,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionJson>();
        Assert.False(session!.IsWalkIn);
        Assert.Equal("BK-IT-GATE-1", session.BookingCode);
        Assert.Equal("Active", session.Status);
        Assert.Equal("Qr", session.CheckInMethod);
        Assert.StartsWith("PS-", session.Code);
        Assert.Equal("51A-888.88", session.PlateDisplay);

        // Tra tại cổng ra → thấy xe đang đỗ
        var lookup = await fixture.Client.GetFromJsonAsync<SessionJson>(
            $"/api/v1/parking-sessions/lookup?parkingLotId={DemoIds.LotVincom}&plate=51A-888.88");
        Assert.Equal(session.Code, lookup!.Code);

        // Database: đúng 1 GateEvent Check-in với note chuẩn T-704
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GateDbContext>();
        var events = await db.GateEvents
            .Where(e => e.ParkingLotId == DemoIds.LotVincom && e.EventType == GateEventType.CheckIn)
            .ToListAsync();
        Assert.Contains(events, e => e.Note != null && e.Note.Contains("Check-in theo booking BK-IT-GATE-1"));
        Assert.Contains(events, e => e.PlateNumber == "51A88888");
    }

    [Fact] // 1 booking chỉ check-in đúng 1 lần (T-704): cùng booking + biển khác vẫn bị chặn vì trùng mã booking.
    public async Task Second_checkin_for_the_same_booking_conflicts()
    {
        var expires = DateTime.UtcNow.AddHours(4);
        var firstToken = MintQrToken("BK-IT-GATE-2", "51B23456", DemoIds.LotVincom, expires);
        var first = await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-in", new
        {
            parkingLotId = DemoIds.LotVincom,
            method = "Qr",
            plate = "51B23456",
            qrToken = firstToken,
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Cùng booking, đổi biển số (token của cùng booking): chặn trước việc tạo lượt gửi thứ hai
        var replayToken = MintQrToken("BK-IT-GATE-2", "51B99999", DemoIds.LotVincom, expires);
        await Asserts.ConflictAsync(await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-in", new
        {
            parkingLotId = DemoIds.LotVincom,
            method = "Qr",
            plate = "51B99999",
            qrToken = replayToken,
        }), "đã được check-in trước đó");
    }

    [Fact] // Token thiếu / sai chữ ký / thuộc bãi khác / hết hạn đều bị từ chối với 400 (không rò rỉ lý do bảo mật).
    public async Task Invalid_qr_tokens_are_rejected()
    {
        var expires = DateTime.UtcNow.AddHours(4);

        // Thiếu token
        await Asserts.BadRequestAsync(await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-in",
            new { parkingLotId = DemoIds.LotVincom, method = "Qr", plate = "51C11111" }),
            "Check-in bằng QR phải kèm mã token QR");

        // Sai chữ ký
        var validToken = MintQrToken("BK-IT-GATE-3", "51C11111", DemoIds.LotVincom, expires);
        await Asserts.BadRequestAsync(await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-in",
            new { parkingLotId = DemoIds.LotVincom, method = "Qr", plate = "51C11111", qrToken = Tamper(validToken) }),
            "Mã QR không hợp lệ");

        // Token của bãi khác (TSN) dùng cho bãi Vincom
        var wrongLot = MintQrToken("BK-IT-GATE-4", "51C22222", DemoIds.LotTsn, expires);
        await Asserts.BadRequestAsync(await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-in",
            new { parkingLotId = DemoIds.LotVincom, method = "Qr", plate = "51C22222", qrToken = wrongLot }),
            "Mã QR không hợp lệ");

        // Token hết hạn
        var expired = MintQrToken("BK-IT-GATE-5", "51C33333", DemoIds.LotVincom, DateTime.UtcNow.AddMinutes(-5));
        await Asserts.BadRequestAsync(await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-in",
            new { parkingLotId = DemoIds.LotVincom, method = "Qr", plate = "51C33333", qrToken = expired }),
            "Mã QR không hợp lệ");

        // Không lượt nào được tạo cho các biển số trên
        var list = await fixture.Client.GetFromJsonAsync<List<SessionJson>>(
            $"/api/v1/parking-sessions?parkingLotId={DemoIds.LotVincom}&status=Active");
        Assert.DoesNotContain(list!, s => s.PlateNumber is "51C11111" or "51C22222" or "51C33333");
    }

    [Fact] // Khách vãng lai nhập tay: vào → đang đỗ → ra; xe chưa vào bãi tra cứu → 204; check-out 2 lần → 404.
    public async Task Walkin_manual_flow_from_checkin_to_checkout()
    {
        // Vào bãi
        var checkIn = await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-in",
            new { parkingLotId = DemoIds.LotVincom, method = "Manual", plate = "51B77777", staffUserId = DemoIds.StaffVincomUser });
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        var session = await checkIn.Content.ReadFromJsonAsync<SessionJson>();
        Assert.True(session!.IsWalkIn);
        Assert.Null(session.BookingCode);
        Assert.Equal("Manual", session.CheckInMethod);

        // Đang trong bãi → check-in lần 2 cùng biển/bãi bị chặn
        await Asserts.ConflictAsync(await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-in",
            new { parkingLotId = DemoIds.LotVincom, method = "Manual", plate = "51B77777" }),
            "đang có lượt gửi chưa kết thúc");

        // Danh sách xe đang đỗ của bãi chứa lượt này
        var active = await fixture.Client.GetFromJsonAsync<List<SessionJson>>(
            $"/api/v1/parking-sessions?parkingLotId={DemoIds.LotVincom}&status=Active");
        Assert.Contains(active!, s => s.Code == session.Code);

        // Ra bãi (biển nhập có dấu chấm vẫn chuẩn hóa được) — response mới: CheckOutResultDto { session, fee, warning }.
        // Walk-in không confirmFee → fee = null (hành vi cũ giữ nguyên).
        var checkOut = await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-out",
            new { parkingLotId = DemoIds.LotVincom, plate = "51B-777.77", method = "Manual", staffUserId = DemoIds.StaffVincomUser });
        Assert.Equal(HttpStatusCode.OK, checkOut.StatusCode);
        var result = await checkOut.Content.ReadFromJsonAsync<CheckOutJson>();
        Assert.Equal("Completed", result!.Session.Status);
        Assert.NotNull(result.Session.ExitAtUtc);
        Assert.Null(result.Fee);
        Assert.Null(result.Warning);

        // Sau khi ra: tra cứu → 204 (NoContent), check-out lần nữa → 404
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.GetAsync(
            $"/api/v1/parking-sessions/lookup?parkingLotId={DemoIds.LotVincom}&plate=51B-777.77")).StatusCode);
        await Asserts.NotFoundAsync(await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-out",
            new { parkingLotId = DemoIds.LotVincom, plate = "51B77777", method = "Manual" }));
    }

    [Fact] // Seed đọc được từ API: PS-0002 vãng lai đang đỗ tại TSN; Vincom có 3 thiết bị; heartbeat cập nhật LastSeenAtUtc.
    public async Task Seeded_sessions_and_gate_devices_are_served()
    {
        var admin = fixture.As(DemoIds.AdminUser, "Admin");
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync(
            $"/api/v1/parking-sessions?parkingLotId={DemoIds.LotTsn}")).StatusCode); // nhân viên bãi khác
        var tsnActive = await admin.GetFromJsonAsync<List<SessionJson>>(
            $"/api/v1/parking-sessions?parkingLotId={DemoIds.LotTsn}&status=Active");
        var walkIn = Assert.Single(tsnActive!);
        Assert.Equal("PS-0002", walkIn.Code);
        Assert.True(walkIn.IsWalkIn);
        Assert.Equal("51A-999.99", walkIn.PlateDisplay);

        var devices = await admin.GetFromJsonAsync<List<DeviceJson>>($"/api/v1/gate-devices?parkingLotId={DemoIds.LotVincom}");
        Assert.Equal(3, devices!.Count);
        var scanner = devices.Single(d => d.Code == "VC-QR-IN-01");
        Assert.Equal("QrScanner", scanner.DeviceType);

        var heartbeat = await fixture.Client.PostAsJsonAsync($"/api/v1/gate-devices/{scanner.Id}/heartbeat", new { });
        Assert.True(heartbeat.IsSuccessStatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GateDbContext>();
        var device = await db.GateDevices.FindAsync([scanner.Id]);
        Assert.NotNull(device);
        Assert.True(device!.LastSeenAtUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact] // US-076: xếp ca → Start → thu tiền → End → ca sau đếm lại và nhận → chủ bãi nhận cuối; truy vấn chạy trên PostgreSQL.
    public async Task Shift_flow_from_planning_to_cash_handover()
    {
        const int an = 901, binh = 902;
        var lot = DemoIds.LotTsn;
        var owner = fixture.As(DemoIds.AdminUser + 2, "LotOwner", DemoIds.OwnerTsn);
        var first = fixture.As(an, "Staff", DemoIds.OwnerTsn, lot);
        var second = fixture.As(binh, "Staff", DemoIds.OwnerTsn, lot);
        var now = DateTime.UtcNow;

        var created = await Json<ScheduleJson>(await owner.PostAsJsonAsync($"/api/v1/gate/lots/{lot}/shifts", new
        {
            shifts = new[]
            {
                new { staffUserId = an, startUtc = now.AddMinutes(-5), endUtc = now.AddMinutes(10), isCashHolder = true },
                new { staffUserId = binh, startUtc = now.AddMinutes(10), endUtc = now.AddMinutes(60), isCashHolder = true },
            },
        }), HttpStatusCode.Created);
        var (shiftA, shiftB) = (created.Shifts[0].Id, created.Shifts[1].Id);

        var walkIn = new { parkingLotId = lot, method = "Manual", plate = "51K11111" };
        await Asserts.ConflictAsync(await first.PostAsJsonAsync("/api/v1/gate/check-in", walkIn), "Chưa có ca");
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Factory.CreateClient().PostAsJsonAsync("/api/v1/gate/check-in", walkIn)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.As(DemoIds.AdminUser, "Admin").PostAsJsonAsync("/api/v1/gate/check-in", walkIn)).StatusCode);

        await Json<ShiftJson>(await first.PostAsync($"/api/v1/gate/shifts/{shiftA}/start", null));
        Assert.Equal(HttpStatusCode.OK, (await first.PostAsJsonAsync("/api/v1/gate/check-in", walkIn)).StatusCode);
        var paid = await Json<CheckOutJson>(await first.PostAsJsonAsync("/api/v1/gate/check-out",
            new { parkingLotId = lot, plate = "51K11111", method = "Manual", confirmFee = true }));
        Assert.Equal(80000m, paid.Fee!.AdditionalDue);

        var summary = await first.GetFromJsonAsync<SummaryJson>($"/api/v1/gate/shifts/{shiftA}/summary");
        Assert.Equal((80000m, 1, 1), (summary!.ExpectedCash, summary.Entries, summary.Exits));
        var ended = await Json<ShiftJson>(await first.PostAsJsonAsync($"/api/v1/gate/shifts/{shiftA}/end", new { cashCounted = 80000, note = "ok" }));
        Assert.Equal(("HandoverPending", shiftB), (ended.Status, ended.HandoverToShiftId!.Value));

        await Json<ShiftJson>(await second.PostAsync($"/api/v1/gate/shifts/{shiftB}/start", null));
        var mine = await second.GetFromJsonAsync<MineJson>("/api/v1/gate/me/shifts");
        Assert.Equal(shiftA, Assert.Single(mine!.IncomingHandovers).Id);
        var accepted = await Json<ShiftJson>(await second.PostAsJsonAsync($"/api/v1/gate/shifts/{shiftA}/handover/accept", new { cashReceived = 80000 }));
        Assert.Equal("Completed", accepted.Status);
        Assert.Equal(80000m, (await second.GetFromJsonAsync<SummaryJson>($"/api/v1/gate/shifts/{shiftB}/summary"))!.OpeningCash);

        var last = await Json<ShiftJson>(await second.PostAsJsonAsync($"/api/v1/gate/shifts/{shiftB}/end", new { cashCounted = 70000 }));
        Assert.Equal(("HandoverPending", "Open"), (last.Status, last.DiscrepancyStatus));
        await Json<ShiftJson>(await owner.PostAsJsonAsync($"/api/v1/gate/shifts/{shiftB}/handover/accept", new { cashReceived = 70000 }));
        await Json<ShiftJson>(await owner.PostAsJsonAsync($"/api/v1/gate/shifts/{shiftB}/discrepancy/resolve", new { note = "Đã đối chiếu" }));
        Assert.Equal(2, (await owner.GetFromJsonAsync<List<ShiftJson>>($"/api/v1/gate/lots/{lot}/handovers"))!.Count);
        var week = await owner.GetAsync($"/api/v1/gate/lots/{lot}/shifts?weekStart={DateOnly.FromDateTime(now.AddHours(7)).AddDays(-3):yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, week.StatusCode);
        var page = await fixture.As(DemoIds.AdminUser, "Admin").GetFromJsonAsync<PageJson>($"/api/v1/gate/admin/shifts?parkingLotId={lot}&status=Completed");
        Assert.Equal(2, page!.Total);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GateDbContext>();
        var session = await db.ParkingSessions.SingleAsync(s => s.PlateNumber == "51K11111");
        Assert.Equal(DemoIds.OwnerTsn, session.OwnerProfileId);
        Assert.All(await db.GateEvents.Where(e => e.ParkingSessionId == session.Id).ToListAsync(), e => Assert.Equal(shiftA, e.ShiftId));
        Assert.Contains(await db.OutboxMessages.Select(m => m.EventType).ToListAsync(), t => t == "CashDiscrepancyRaised");
    }

    private static async Task<T> Json<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.True(response.StatusCode == expected, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    public sealed record ShiftJson(int Id, string Status, int? HandoverToShiftId, string? DiscrepancyStatus);
    public sealed record ScheduleJson(List<ShiftJson> Shifts);
    public sealed record SummaryJson(decimal OpeningCash, decimal? ExpectedCash, int Entries, int Exits);
    public sealed record MineJson(List<ShiftJson> IncomingHandovers);
    public sealed record PageJson(int Total);

    public sealed record SessionJson(int Id, string Code, int ParkingLotId, string PlateNumber, string PlateDisplay,
        bool IsWalkIn, string? BookingCode, string? SlotCode, string Status, DateTime EntryAtUtc, DateTime? ExitAtUtc,
        int MinutesParked, string CheckInMethod, double? EntryOcrConfidence, decimal? Fee);

    public sealed record CheckOutJson(SessionJson Session, FeeJson? Fee, string? Warning);

    public sealed record FeeJson(decimal SessionFee, decimal PrepaidAmount, decimal AdditionalDue,
        bool IsWithinBillingGrace, int OverstayMinutes, decimal OverstayAmount);

    public sealed record DeviceJson(int Id, string Code, string Name, string DeviceType, string? Position, string Status,
        DateTime? LastSeenAtUtc);
}

/// <summary>Stub BookingService (TV3): mọi mã booking đều Confirmed, bắt đầu sau 1 tiếng, bãi Vincom.</summary>
public sealed class StubGateBookingClient : IGateBookingClient
{
    public List<string> CheckInCalls { get; } = [];
    public List<string> CheckOutCalls { get; } = [];

    public Task<GateBookingSnapshot?> FindByCodeAsync(string bookingCode, CancellationToken cancellationToken = default)
        => Task.FromResult<GateBookingSnapshot?>(new GateBookingSnapshot(bookingCode, "Confirmed", DemoIds.LotVincom,
            "51A88888", "Sedan", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3), "B1-A01", 63000, 15, 1.5m));

    public Task<IReadOnlyList<GateBookingListItem>> ListForLotAsync(int parkingLotId, DateTime fromUtc, DateTime toUtc,
        string? search, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GateBookingListItem>>([]);

    public Task SyncCheckInAsync(string bookingCode, int? staffUserId, CancellationToken cancellationToken = default)
    {
        CheckInCalls.Add(bookingCode);
        return Task.CompletedTask;
    }

    public Task SyncCheckOutAsync(string bookingCode, int? staffUserId, CancellationToken cancellationToken = default)
    {
        CheckOutCalls.Add(bookingCode);
        return Task.CompletedTask;
    }
}

/// <summary>Stub PaymentService (TV4): phí cố định 80.000đ không quá giờ, ghi nhận lệnh thu tiền trong bộ nhớ.</summary>
public sealed class StubPaymentService : IExitFeeCalculator, IGateSettlementRecorder, IShiftCashSource
{
    public Task<decimal> CashTotalAsync(int shiftId, CancellationToken ct)
        => Task.FromResult(Records.Where(r => r.ShiftId == shiftId).Sum(r => r.Amount));

    public Task<GateSettlementResult?> FindAsync(string sessionCode, CancellationToken cancellationToken = default)
        => Task.FromResult(Records.Where(r => r.SessionCode == sessionCode)
            .Select(r => new GateSettlementResult("PM-IT-TEST", "Succeeded", r.Amount)).FirstOrDefault());

    public List<GateSettlementRequest> Records { get; } = [];

    public Task<SessionFeeResult> CalculateAsync(SessionFeeRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new SessionFeeResult(1, 60, false, 50000m, 1m, 0, 0m, 80000m, []));

    public Task<GateSettlementResult> RecordAsync(GateSettlementRequest request, CancellationToken cancellationToken = default)
    {
        Records.Add(request);
        return Task.FromResult(new GateSettlementResult("PM-IT-TEST", "Succeeded", request.Amount));
    }
}

/// <summary>Stub ParkingService: bãi TSN thuộc chủ bãi TSN, còn lại thuộc Vincom; không khai giờ mở cửa.</summary>
public sealed class StubLotDirectory : ILotDirectory
{
    public Task<LotInfo> GetAsync(int parkingLotId, CancellationToken ct) => Task.FromResult(new LotInfo(parkingLotId,
        parkingLotId == DemoIds.LotTsn ? DemoIds.OwnerTsn : DemoIds.OwnerVincom, "Lot", "Active"));
    public Task<IReadOnlyList<TimeRange>> OpeningHoursAsync(int parkingLotId, int ownerProfileId, DateOnly weekStart, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<TimeRange>>([]);
}

public sealed class StubStaffDirectory : IStaffDirectory
{
    public Task<bool> IsAssignedAsync(int parkingLotId, int staffUserId, CancellationToken ct) => Task.FromResult(true);
}

public sealed class StubLotStatus : ILotOperationalStatusProvider
{
    public Task<LotOperationalStatusSnapshot?> GetAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => Task.FromResult<LotOperationalStatusSnapshot?>(new(parkingLotId, "Lot", "Active", false, null));
}
