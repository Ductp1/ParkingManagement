using System.Net;
using System.Net.Http.Json;
using AdminService.API.Controllers;
using AdminService.Application.Features.Users;
using AdminService.Infrastructure.Integrations;
using AdminService.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ParkingManagement.IntegrationTests;
using ParkingManagement.SharedKernel.Contracts;
using UserService.API.Controllers;
using UserService.Infrastructure.Persistence;

namespace ParkingManagement.IntegrationTests.PerService;

/// <summary>
/// Fixture AdminService: dựng 2 host — UserService (nguồn dữ liệu người dùng thật) và AdminService với
/// Services:GatewayBaseUrl trỏ thẳng vào host UserService (bỏ qua Gateway vì test gọi thẳng service).
/// JWT admin được mint bằng cặp khóa RSA test; phiên đăng nhập được "arrange" bằng 1 dòng RefreshTokens
/// trong pm_user để UserPrincipalValidator (TV1) xác thực sid như production.
/// </summary>
public sealed class AdminApiFixture : IAsyncLifetime
{
    private readonly RsaTestKeys _keys = RsaTestKeys.Create();

    public string UserDbName { get; } = $"pm_it_user_{Guid.NewGuid().ToString("N")[..8]}";
    public string AdminDbName { get; } = $"pm_it_admin_{Guid.NewGuid().ToString("N")[..8]}";

    public WebApplicationFactory<UsersController> UserFactory { get; private set; } = null!;
    public WebApplicationFactory<AdminUsersController> AdminFactory { get; private set; } = null!;
    public HttpClient AdminClient { get; private set; } = null!;
    public string AdminToken { get; private set; } = string.Empty;

    /// <summary>sid của phiên admin — trùng TokenHash của RefreshTokens đã arrange trong pm_user.</summary>
    public string Sid { get; } = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    public IServiceProvider AdminServices => AdminFactory.Services;

    public async Task InitializeAsync()
    {
        var baseCs = TestDatabaseProvisioner.ResolveBaseConnectionString();
        var userCs = TestDatabaseProvisioner.ReplaceDatabase(baseCs, UserDbName);
        var adminCs = TestDatabaseProvisioner.ReplaceDatabase(baseCs, AdminDbName);
        await TestDatabaseProvisioner.CreateDatabaseAsync(baseCs, UserDbName);
        await TestDatabaseProvisioner.CreateDatabaseAsync(baseCs, AdminDbName);

        // Khóa JWT của fixture này (chỉ fixture Admin dùng 2 key này → an toàn khi chạy song song).
        Environment.SetEnvironmentVariable("Jwt__PublicKeyPath", _keys.PublicKeyPath);
        Environment.SetEnvironmentVariable("Jwt__PrivateKeyPath", _keys.PrivateKeyPath);

        // ----- 1. UserService host (nguồn dữ liệu + xác thực phiên thật) -----
        UserFactory = new WebApplicationFactory<UsersController>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:PublicKeyPath"] = _keys.PublicKeyPath,
                    ["Jwt:PrivateKeyPath"] = _keys.PrivateKeyPath,
                }));
            builder.ConfigureServices(services =>
            {
                services.UseIsolatedDatabase<UserDbContext>(userCs);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
        using var userHealth = await UserFactory.CreateClient().GetAsync("/health");
        Assert.True(userHealth.IsSuccessStatusCode, $"UserService không start được trên {UserDbName}.");

        // ----- 2. Arrange phiên đăng nhập cho user 1 (Admin) để TV1 xác thực sid -----
        await using (var scope = UserFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            db.RefreshTokens.Add(new UserService.Domain.Entities.RefreshToken
            {
                UserId = DemoIds.AdminUser,
                TokenHash = Sid,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                CreatedByIp = "integration-test",
            });
            await db.SaveChangesAsync();
        }

        AdminToken = _keys.MintToken(DemoIds.AdminUser, "admin@smartparking.vn", Sid, "Admin");

        // ----- 3. AdminService host (Gateway trỏ thẳng sang host UserService đã chạy) -----
        // Client IUserServiceClient được thay bằng closure scoped-factory trỏ thẳng sang host UserService test
        // (đăng ký kiểu này không bị Program.cs ghi đè).
        AdminFactory = new WebApplicationFactory<AdminUsersController>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:PublicKeyPath"] = _keys.PublicKeyPath,
                }));
            builder.ConfigureServices(services =>
            {
                services.UseIsolatedDatabase<AdminDbContext>(adminCs);

                // Thay client gọi UserService bằng HttpClient trỏ thẳng sang host test:
                // closure scoped-factory được resolve LAZY nên Program không thể ghi đè
                // (AddHttpClient/AddTransient của Program đăng ký type mà ta không resolve).
                // BearerTokenForwardingHandler giữ nguyên hành vi production: chuyển tiếp token admin sang TV1.
                var userUrl = UserFactory.ClientOptions.BaseAddress.AbsoluteUri;
                services.AddScoped<IUserServiceClient>(sp =>
                {
                    var http = new HttpClient(new BearerTokenForwardingHandler(
                        sp.GetRequiredService<IHttpContextAccessor>())
                    {
                        InnerHandler = new HttpClientHandler()
                    })
                    { BaseAddress = new Uri(userUrl) };
                    return new UserServiceClient(http, sp.GetRequiredService<ILogger<UserServiceClient>>());
                });
            });
        });
        AdminClient = AdminFactory.CreateClient();
        AdminClient.DefaultRequestHeaders.Authorization = new("Bearer", AdminToken);

        using var adminHealth = await AdminClient.GetAsync("/health");
        Assert.True(adminHealth.IsSuccessStatusCode, $"AdminService không start được trên {AdminDbName}.");
    }

    public async Task DisposeAsync()
    {
        UserFactory?.Dispose();
        AdminFactory?.Dispose();
        try
        {
            var baseCs = TestDatabaseProvisioner.ResolveBaseConnectionString();
            await TestDatabaseProvisioner.DropDatabaseAsync(baseCs, UserDbName);
            await TestDatabaseProvisioner.DropDatabaseAsync(baseCs, AdminDbName);
        }
        catch { // best-effort
        }
        _keys.Dispose();
    }
}

/// <summary>Collection: AdminService (+ UserService thật làm phụ thuộc) trên database pm_it_* riêng.</summary>
[CollectionDefinition("Admin API")]
public sealed class AdminApiCollection : ICollectionFixture<AdminApiFixture>;

/// <summary>
/// Integration test AdminService (TV8): tra cứu người dùng US-096 qua UserService thật (chuyển tiếp Bearer token),
/// khóa/mở khóa chủ bãi kèm Sanction + AuditLog trong pm_admin, tham số hệ thống UC-44 (đổi → lịch sử → hủy).
/// </summary>
[Collection("Admin API")]
public class AdminServiceApiTests(AdminApiFixture fixture)
{
    [Fact] // US-096: danh sách người dùng đọc từ UserService thật qua HTTP; lọc theo role; chi tiết có OwnerProfile.
    public async Task Users_list_and_detail_are_proxied_from_user_service()
    {
        var all = await fixture.AdminClient.GetFromJsonAsync<PageJson<UserJson>>("/api/v1/admin/users?page=1&pageSize=20");
        Assert.Equal(6, all!.TotalCount);

        var drivers = await fixture.AdminClient.GetFromJsonAsync<PageJson<UserJson>>("/api/v1/admin/users?role=Driver&page=1&pageSize=20");
        Assert.Equal(3, drivers!.TotalCount); // driver1, driver2 và owner.vincom (được gắn thêm role Driver)
        Assert.Contains(drivers.Items, u => u.Email == "driver1@smartparking.vn");

        var detail = await fixture.AdminClient.GetFromJsonAsync<AdminUserJson>("/api/v1/admin/users/2");
        Assert.Equal("Công ty Vincom Parking", detail!.FullName);
        Assert.Equal("owner.vincom@smartparking.vn", detail.Email);
        Assert.Equal(1, detail.OwnerProfile!.Id);
        Assert.Equal("Công ty TNHH Vincom Parking", detail.OwnerProfile.BusinessName);

        await Asserts.NotFoundAsync(await fixture.AdminClient.GetAsync("/api/v1/admin/users/999"));
    }

    [Fact] // Không mang token → 401 từ middleware xác thực JWT.
    public async Task Admin_endpoints_require_a_bearer_token()
    {
        using var anonymous = fixture.AdminFactory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/users")).StatusCode);
    }

    [Fact] // UC-40: khóa chủ bãi ghi Sanction + AuditLog; unlock thu hồi; unlock lần 2 → 409 "không bị khóa".
    public async Task Lock_then_unlock_owner_records_sanction_and_audit_logs()
    {
        // Owner 1 (Vincom) chưa có chế tài nào trong seed
        var before = await fixture.AdminClient.GetFromJsonAsync<PageJson<SanctionJson>>("/api/v1/admin/owners/1/sanctions");
        Assert.Empty(before!.Items);

        var lockResponse = await fixture.AdminClient.PostAsJsonAsync("/api/v1/admin/owners/1/lock",
            new { ownerUserId = DemoIds.OwnerVincomUser, reason = "IT – nghi ngờ gian lận" });
        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);
        var sanction = await lockResponse.Content.ReadFromJsonAsync<SanctionJson>();
        Assert.Equal(1, sanction!.OwnerProfileId);
        Assert.Equal("IT – nghi ngờ gian lận", sanction.Reason);

        // Danh sách chế tài phản ánh lệnh khóa
        var afterLock = await fixture.AdminClient.GetFromJsonAsync<PageJson<SanctionJson>>("/api/v1/admin/owners/1/sanctions");
        Assert.Single(afterLock!.Items);

        // Thiếu lý do → 400
        await Asserts.BadRequestAsync(await fixture.AdminClient.PostAsJsonAsync("/api/v1/admin/owners/1/unlock",
            new { reason = "" }), "Lý do mở khóa không được để trống");

        // Mở khóa → chế tài chuyển Revoked
        var unlock = await fixture.AdminClient.PostAsJsonAsync("/api/v1/admin/owners/1/unlock", new { reason = "IT – đã rõ hiềm nghi" });
        Assert.Equal(HttpStatusCode.OK, unlock.StatusCode);

        // Unlock lần 2 → 409 vì chủ bãi không còn bị khóa
        await Asserts.ConflictAsync(await fixture.AdminClient.PostAsJsonAsync("/api/v1/admin/owners/1/unlock",
            new { reason = "IT – mở lần nữa" }), "hiện không bị khóa");

        // Database pm_admin: đủ Sanction + AuditLog cho cả khóa và mở khóa
        await using var scope = fixture.AdminServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var audit = await db.AuditLogs.Where(a => a.OwnerProfileId == 1).OrderBy(a => a.Id).ToListAsync();
        Assert.Contains(audit, a => a.Action == "Owner.Locked");
        Assert.Contains(audit, a => a.Action == "Owner.Unlocked");
    }

    [Fact] // UC-44/US-098: đọc tham số nền tảng → đổi BOOKING_HOLD_MINUTES → lịch sử có dòng mới → hủy thay đổi hẹn trước.
    public async Task Settings_update_history_and_cancel_pending_change()
    {
        var settings = await fixture.AdminClient.GetFromJsonAsync<PlatformSettingsJson>("/api/v1/admin/settings");
        Assert.Contains(settings!.Configs, c => c.Key == "BOOKING_HOLD_MINUTES");

        // Đổi giá trị hiệu lực NGAY
        var update = await fixture.AdminClient.PutAsJsonAsync("/api/v1/admin/settings/BOOKING_HOLD_MINUTES",
            new { value = "20", reason = "IT – rút ngắn thời gian giữ chỗ" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var applied = await update.Content.ReadFromJsonAsync<SystemConfigChangeJson>();
        Assert.Equal("20", applied!.Value);

        var byKey = await fixture.AdminClient.GetFromJsonAsync<SystemConfigDetailJson>("/api/v1/admin/settings/BOOKING_HOLD_MINUTES");
        Assert.Equal("20", byKey!.Value);

        // Đặt thay đổi hẹn trước 30 ngày rồi hủy
        var future = await fixture.AdminClient.PutAsJsonAsync("/api/v1/admin/settings/BOOKING_HOLD_MINUTES",
            new { value = "45", reason = "IT – hẹn áp dụng tháng sau", effectiveFromUtc = DateTime.UtcNow.AddDays(30) });
        Assert.Equal(HttpStatusCode.OK, future.StatusCode);
        var pending = await future.Content.ReadFromJsonAsync<SystemConfigChangeJson>();

        var cancel = await fixture.AdminClient.PostAsJsonAsync(
            $"/api/v1/admin/settings/BOOKING_HOLD_MINUTES/changes/{pending!.Id}/cancel", new { reason = "IT – hủy lịch" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        // Lịch sử ghi đủ 3 thay đổi, thay đổi hẹn trước mang trạng thái Cancelled
        var history = await fixture.AdminClient.GetFromJsonAsync<PageJson<SystemConfigChangeJson>>(
            "/api/v1/admin/settings/BOOKING_HOLD_MINUTES/history?page=1&pageSize=20");
        Assert.True(history!.TotalCount >= 2);
        Assert.Contains(history.Items, c => c.Id == pending.Id && c.Status == "Cancelled");

        // Giá trị vượt khoảng cho phép (1–120) → 400
        await Asserts.BadRequestAsync(await fixture.AdminClient.PutAsJsonAsync("/api/v1/admin/settings/BOOKING_HOLD_MINUTES",
            new { value = "500", reason = "IT" }), "phải trong khoảng");
    }

}

public sealed record PageJson<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record UserJson(int Id, string FullName, string? Email, string Status, IReadOnlyList<string> Roles);

public sealed record OwnerProfileJson(int Id, string BusinessName);

public sealed record AdminUserJson(int Id, string FullName, string? Email, string? PhoneNumber, string Status,
    string KycStatus, IReadOnlyList<string> Roles, OwnerProfileJson? OwnerProfile);

public sealed record SanctionJson(int Id, int OwnerProfileId, string Level, string Reason, string? Status,
    string? UserServiceSyncStatus);

public sealed record SystemConfigJson(string Key, string Value, string DataType, string? Description, DateTime? UpdatedAtUtc);

public sealed record FeatureFlagJson(string Key, bool IsEnabled, string? Description);

public sealed record PlatformSettingsJson(IReadOnlyList<SystemConfigJson> Configs, IReadOnlyList<FeatureFlagJson> FeatureFlags);

public sealed record SystemConfigDetailJson(string Key, string Value, string DataType, string? Description, string DefaultValue);

public sealed record SystemConfigChangeJson(int Id, string Key, string Value, string PreviousValue,
    DateTime EffectiveFromUtc, string? Status);
