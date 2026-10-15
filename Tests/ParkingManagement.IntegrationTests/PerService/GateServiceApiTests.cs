using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GateService.API.Controllers;
using GateService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace ParkingManagement.IntegrationTests.PerService;

/// <summary>Collection: GateService chạy host thật trên database pm_it_gate_* riêng của từng lần chạy.</summary>
[CollectionDefinition("Gate API")]
public sealed class GateApiCollection : ICollectionFixture<ServiceHostFixture<GateController, GateDbContext>>;

/// <summary>
/// Integration test GateService (TV7): cổng vào/ra T-702 và hợp đồng QR với BookingService T-704.
/// Token QR mint đúng định dạng từng byte của QrTokenService (payload "code|plate|lot|expiryTicks",
/// HMAC-SHA256 Base64) bằng chính QRTOKEN_SECRET của host → xác thực thật qua BookingQrTokenValidator.
/// </summary>
[Collection("Gate API")]
public class GateServiceApiTests(ServiceHostFixture<GateController, GateDbContext> fixture)
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

    [Fact] // 1 booking chỉ check-in đúng 1 lần (T-704) → lần 2 bị chặn 409.
    public async Task Second_checkin_for_the_same_booking_conflicts()
    {
        var token = MintQrToken("BK-IT-GATE-2", "51B23456", DemoIds.LotVincom, DateTime.UtcNow.AddHours(4));
        var body = new { parkingLotId = DemoIds.LotVincom, method = "Qr", plate = "51B23456", qrToken = token };
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-in", body)).StatusCode);

        await Asserts.ConflictAsync(await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-in", body),
            "đã được check-in trước đó");
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

        // Ra bãi (biển nhập có dấu chấm vẫn chuẩn hóa được)
        var checkOut = await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-out",
            new { parkingLotId = DemoIds.LotVincom, plate = "51B-777.77", method = "Manual", staffUserId = DemoIds.StaffVincomUser });
        Assert.Equal(HttpStatusCode.OK, checkOut.StatusCode);
        var completed = await checkOut.Content.ReadFromJsonAsync<SessionJson>();
        Assert.Equal("Completed", completed!.Status);
        Assert.NotNull(completed.ExitAtUtc);

        // Sau khi ra: tra cứu → 204 (NoContent), check-out lần nữa → 404
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.GetAsync(
            $"/api/v1/parking-sessions/lookup?parkingLotId={DemoIds.LotVincom}&plate=51B-777.77")).StatusCode);
        await Asserts.NotFoundAsync(await fixture.Client.PostAsJsonAsync("/api/v1/gate/check-out",
            new { parkingLotId = DemoIds.LotVincom, plate = "51B77777", method = "Manual" }));
    }

    [Fact] // Seed đọc được từ API: PS-0002 vãng lai đang đỗ tại TSN; Vincom có 3 thiết bị; heartbeat cập nhật LastSeenAtUtc.
    public async Task Seeded_sessions_and_gate_devices_are_served()
    {
        var tsnActive = await fixture.Client.GetFromJsonAsync<List<SessionJson>>(
            $"/api/v1/parking-sessions?parkingLotId={DemoIds.LotTsn}&status=Active");
        var walkIn = Assert.Single(tsnActive!);
        Assert.Equal("PS-0002", walkIn.Code);
        Assert.True(walkIn.IsWalkIn);
        Assert.Equal("51A-999.99", walkIn.PlateDisplay);

        var devices = await fixture.Client.GetFromJsonAsync<List<DeviceJson>>($"/api/v1/gate-devices?parkingLotId={DemoIds.LotVincom}");
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

    public sealed record SessionJson(int Id, string Code, int ParkingLotId, string PlateNumber, string PlateDisplay,
        bool IsWalkIn, string? BookingCode, string? SlotCode, string Status, DateTime EntryAtUtc, DateTime? ExitAtUtc,
        int MinutesParked, string CheckInMethod, double? EntryOcrConfidence, decimal? Fee);

    public sealed record DeviceJson(int Id, string Code, string Name, string DeviceType, string? Position, string Status,
        DateTime? LastSeenAtUtc);
}
