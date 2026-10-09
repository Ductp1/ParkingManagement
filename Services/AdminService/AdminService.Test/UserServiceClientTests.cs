using System.Net;
using System.Text;
using System.Text.Json;
using AdminService.Application.Features.Users;
using AdminService.Infrastructure;
using AdminService.Infrastructure.Integrations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Test;

// US-096: adapter HttpClient gọi UserService qua Gateway (unit test với fake HttpMessageHandler, không cần UserService chạy).
public class UserServiceClientTests
{
    private const string UsersPageJson = """
        {
          "items": [
            { "id": 2, "fullName": "Công ty Vincom Parking", "email": "owner.vincom@smartparking.vn", "status": "Active", "roles": ["LotOwner", "Driver"] },
            { "id": 3, "fullName": "Công ty Bãi xe Tân Sơn Nhất", "email": null, "status": "Locked", "roles": ["LotOwner"] }
          ],
          "page": 2, "pageSize": 10, "totalCount": 12, "totalPages": 2
        }
        """;

    private const string OwnerUserJson = """
        {
          "id": 2, "fullName": "Công ty Vincom Parking", "email": "owner.vincom@smartparking.vn", "phoneNumber": "0900000002",
          "status": "Active", "kycStatus": "NotSubmitted", "roles": ["LotOwner", "Driver"],
          "ownerProfile": { "id": 1, "businessName": "Công ty TNHH Vincom Parking", "taxCode": "0312345678", "bankName": "Vietcombank", "bankAccountNumber": "0011000000001" }
        }
        """;

    [Fact]
    public async Task List_users_calls_gateway_with_role_and_paging_and_maps_page()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, UsersPageJson));

        var result = await CreateClient(handler).ListUsersAsync(UserRoleType.LotOwner, 2, 10, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://gateway.test/api/v1/users?page=2&pageSize=10&role=LotOwner", request.RequestUri!.ToString());
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(12, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Công ty Vincom Parking", result.Items[0].FullName);
        Assert.Equal(["LotOwner", "Driver"], result.Items[0].Roles);
        Assert.Null(result.Items[0].BusinessName);       // UserService chưa trả trường này
        Assert.Null(result.Items[1].Email);
        Assert.Equal("Locked", result.Items[1].Status);
    }

    [Fact]
    public async Task List_users_without_role_omits_role_filter()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, UsersPageJson));

        await CreateClient(handler).ListUsersAsync(null, 1, 20, CancellationToken.None);

        Assert.Equal("http://gateway.test/api/v1/users?page=1&pageSize=20", handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task List_users_reads_business_name_once_user_service_returns_it()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, """
            { "items": [ { "id": 2, "fullName": "A", "email": null, "status": "Active", "roles": ["LotOwner"], "businessName": "Công ty TNHH Vincom Parking" } ],
              "page": 1, "pageSize": 20, "totalCount": 1 }
            """));

        var result = await CreateClient(handler).ListUsersAsync(null, 1, 20, CancellationToken.None);

        Assert.Equal("Công ty TNHH Vincom Parking", result.Items.Single().BusinessName);
    }

    [Fact]
    public async Task List_users_treats_not_found_as_user_service_unavailable()
    {
        // 404 ở đường dẫn danh sách nghĩa là Gateway/UserService chưa có route, không phải "không có dữ liệu".
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await Assert.ThrowsAsync<UserServiceUnavailableException>(
            () => CreateClient(handler).ListUsersAsync(null, 1, 20, CancellationToken.None));
    }

    [Fact]
    public async Task Find_user_by_id_maps_user_and_owner_profile()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, OwnerUserJson));

        var user = await CreateClient(handler).FindUserByIdAsync(2, CancellationToken.None);

        Assert.Equal("http://gateway.test/api/v1/users/2", handler.Requests.Single().RequestUri!.ToString());
        Assert.NotNull(user);
        Assert.Equal(2, user.Id);
        Assert.Equal("0900000002", user.PhoneNumber);
        Assert.Equal("Active", user.Status);
        Assert.Equal("NotSubmitted", user.KycStatus);
        Assert.Equal(["LotOwner", "Driver"], user.Roles);
        Assert.Equal(new AdminUserOwnerProfileDto(1, "Công ty TNHH Vincom Parking"), user.OwnerProfile);
    }

    [Fact]
    public async Task Find_user_by_id_returns_null_when_user_service_reports_not_found()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.NotFound, """{ "status": 404, "title": "Không tìm thấy" }"""));

        Assert.Null(await CreateClient(handler).FindUserByIdAsync(999, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Unexpected_status_code_is_reported_as_user_service_unavailable(HttpStatusCode statusCode)
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(statusCode));

        var ex = await Assert.ThrowsAsync<UserServiceUnavailableException>(
            () => CreateClient(handler).FindUserByIdAsync(2, CancellationToken.None));

        Assert.Contains(((int)statusCode).ToString(), ex.Message);
    }

    [Fact]
    public async Task Connection_failure_is_reported_as_user_service_unavailable()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("Connection refused"));

        var ex = await Assert.ThrowsAsync<UserServiceUnavailableException>(
            () => CreateClient(handler).FindUserByIdAsync(2, CancellationToken.None));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task Timeout_is_reported_as_user_service_unavailable()
    {
        // HttpClient báo hết thời gian chờ bằng TaskCanceledException trong khi request của người gọi chưa bị hủy.
        var handler = new FakeHandler(_ => throw new TaskCanceledException("Timeout", new TimeoutException()));

        await Assert.ThrowsAsync<UserServiceUnavailableException>(
            () => CreateClient(handler).FindUserByIdAsync(2, CancellationToken.None));
    }

    [Fact]
    public async Task Malformed_body_is_reported_as_user_service_unavailable()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, "<html>Bad Gateway</html>"));

        await Assert.ThrowsAsync<UserServiceUnavailableException>(
            () => CreateClient(handler).FindUserByIdAsync(2, CancellationToken.None));
    }

    [Fact]
    public async Task Caller_cancellation_is_not_swallowed()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, OwnerUserJson));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateClient(handler).FindUserByIdAsync(2, cts.Token));
    }

    [Fact]
    public async Task Bearer_token_of_current_request_is_forwarded()
    {
        var inner = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer token-abc";

        await CreateClient(Forwarding(inner, context)).FindUserByIdAsync(2, CancellationToken.None);

        var authorization = inner.Requests.Single().Headers.Authorization;
        Assert.NotNull(authorization);
        Assert.Equal("Bearer", authorization.Scheme);
        Assert.Equal("token-abc", authorization.Parameter);
    }

    [Theory]
    [InlineData(null)]              // request không kèm Authorization
    [InlineData("")]
    [InlineData("Bearer ")]         // thiếu token
    [InlineData("Basic dXNlcjpwYXNz")]
    public async Task Missing_or_non_bearer_authorization_is_not_forwarded(string? authorization)
    {
        var inner = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var context = new DefaultHttpContext();
        if (authorization is not null) context.Request.Headers.Authorization = authorization;

        await CreateClient(Forwarding(inner, context)).FindUserByIdAsync(2, CancellationToken.None);

        Assert.Null(inner.Requests.Single().Headers.Authorization);
    }

    [Fact]
    public async Task Call_outside_http_request_sends_no_authorization()
    {
        var inner = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await CreateClient(Forwarding(inner, context: null)).FindUserByIdAsync(2, CancellationToken.None);

        Assert.Null(inner.Requests.Single().Headers.Authorization);
    }

    [Fact]
    public async Task Find_owner_account_maps_owner_profile_and_lock_state()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, OwnerAccountJson(isLocked: false, ownerStatus: "Active")));

        var account = await CreateClient(handler).FindOwnerAccountAsync(2, CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://gateway.test/api/v1/users/owner-profiles/2", request.RequestUri!.ToString());
        Assert.Null(handler.Bodies.Single());
        Assert.Equal(new OwnerAccountDto(2, 3, "Công ty CP Bãi xe Tân Sơn Nhất", "Công ty Bãi xe Tân Sơn Nhất",
            "owner.tsn@smartparking.vn", "0900000003", false, "Active", "Active"), account);
    }

    [Fact]
    public async Task Lock_owner_posts_reason_lock_window_and_admin_then_maps_locked_account()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, OwnerAccountJson(isLocked: true, ownerStatus: "Suspended")));
        var lockedUntil = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

        var account = await CreateClient(handler).LockOwnerAsync(2, "Gian lận doanh thu", lockedUntil, 1, CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://gateway.test/api/v1/users/owner-profiles/2/lock", request.RequestUri!.ToString());
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
        using var body = JsonDocument.Parse(handler.Bodies.Single()!);
        Assert.Equal("Gian lận doanh thu", body.RootElement.GetProperty("reason").GetString());
        Assert.Equal(lockedUntil, body.RootElement.GetProperty("lockedUntilUtc").GetDateTime().ToUniversalTime());
        Assert.Equal(1, body.RootElement.GetProperty("performedByUserId").GetInt32());
        Assert.NotNull(account);
        Assert.True(account.IsLocked);
        Assert.Equal("Suspended", account.OwnerStatus);
        Assert.Equal(3, account.UserId);
    }

    [Fact]
    public async Task Lock_owner_without_end_date_sends_null_lock_window()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, OwnerAccountJson(isLocked: true, ownerStatus: "Suspended")));

        await CreateClient(handler).LockOwnerAsync(2, "Giấy phép giả", null, 1, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.Bodies.Single()!);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("lockedUntilUtc").ValueKind);   // null = khóa vô thời hạn
    }

    [Fact]
    public async Task Unlock_owner_posts_reason_and_admin_then_maps_unlocked_account()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, OwnerAccountJson(isLocked: false, ownerStatus: "Active")));

        var account = await CreateClient(handler).UnlockOwnerAsync(2, "Đã khắc phục vi phạm", 1, CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://gateway.test/api/v1/users/owner-profiles/2/unlock", request.RequestUri!.ToString());
        using var body = JsonDocument.Parse(handler.Bodies.Single()!);
        Assert.Equal("Đã khắc phục vi phạm", body.RootElement.GetProperty("reason").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("performedByUserId").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("lockedUntilUtc", out _));
        Assert.NotNull(account);
        Assert.False(account.IsLocked);
        Assert.Equal("Active", account.OwnerStatus);
    }

    [Theory]
    [InlineData("find")]
    [InlineData("lock")]
    [InlineData("unlock")]
    public async Task Owner_call_returns_null_when_user_service_reports_owner_not_found(string operation)
    {
        // 404 kèm ProblemDetails = UserService đã xử lý request và không tìm thấy chủ bãi.
        var handler = new FakeHandler(_ => Json(HttpStatusCode.NotFound, """{ "status": 404, "title": "Không tìm thấy" }"""));

        Assert.Null(await CallOwnerEndpoint(CreateClient(handler), operation));
    }

    [Theory]
    [InlineData("find")]
    [InlineData("lock")]
    [InlineData("unlock")]
    public async Task Owner_call_treats_bodyless_not_found_as_user_service_unavailable(string operation)
    {
        // 404 không có body = UserService chưa có endpoint này; không được hiểu là "chủ bãi không tồn tại".
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var ex = await Assert.ThrowsAsync<UserServiceUnavailableException>(
            () => CallOwnerEndpoint(CreateClient(handler), operation));

        Assert.Contains("404", ex.Message);
    }

    [Theory]
    [InlineData("lock", HttpStatusCode.BadRequest)]
    [InlineData("lock", HttpStatusCode.Forbidden)]
    [InlineData("lock", HttpStatusCode.InternalServerError)]
    [InlineData("unlock", HttpStatusCode.BadRequest)]
    [InlineData("unlock", HttpStatusCode.BadGateway)]
    public async Task Owner_write_with_unexpected_status_code_is_reported_as_user_service_unavailable(string operation, HttpStatusCode statusCode)
    {
        var handler = new FakeHandler(_ => Json(statusCode, """{ "status": 0, "title": "Lỗi" }"""));

        var ex = await Assert.ThrowsAsync<UserServiceUnavailableException>(
            () => CallOwnerEndpoint(CreateClient(handler), operation));

        Assert.Contains(((int)statusCode).ToString(), ex.Message);
    }

    [Theory]
    [InlineData("lock")]
    [InlineData("unlock")]
    public async Task Owner_write_connection_failure_is_reported_as_user_service_unavailable(string operation)
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<UserServiceUnavailableException>(
            () => CallOwnerEndpoint(CreateClient(handler), operation));
    }

    [Fact]
    public async Task Lock_owner_forwards_bearer_token_of_current_request()
    {
        var inner = new FakeHandler(_ => Json(HttpStatusCode.OK, OwnerAccountJson(isLocked: true, ownerStatus: "Suspended")));
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer admin-token";

        await CreateClient(Forwarding(inner, context)).LockOwnerAsync(2, "Gian lận doanh thu", null, 1, CancellationToken.None);

        Assert.Equal("admin-token", inner.Requests.Single().Headers.Authorization!.Parameter);
    }

    [Fact]
    public void Infrastructure_registers_user_service_client()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAdminInfrastructure(Configuration("http://localhost:5000"));

        // ValidateOnBuild giống môi trường Development: thiếu đăng ký nào là fail ngay tại đây.
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<UserServiceClient>(scope.ServiceProvider.GetRequiredService<IUserServiceClient>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("localhost:5000/api")]
    [InlineData("ftp://localhost:5000")]
    public void Missing_or_invalid_gateway_base_url_fails_at_startup(string? gatewayBaseUrl)
        => Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddAdminInfrastructure(Configuration(gatewayBaseUrl)));

    private static UserServiceClient CreateClient(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://gateway.test/") });

    private static Task<OwnerAccountDto?> CallOwnerEndpoint(UserServiceClient client, string operation) => operation switch
    {
        "find" => client.FindOwnerAccountAsync(2, CancellationToken.None),
        "lock" => client.LockOwnerAsync(2, "Gian lận doanh thu", null, 1, CancellationToken.None),
        "unlock" => client.UnlockOwnerAsync(2, "Đã khắc phục vi phạm", 1, CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static string OwnerAccountJson(bool isLocked, string ownerStatus) => $$"""
        {
          "ownerProfileId": 2, "userId": 3, "businessName": "Công ty CP Bãi xe Tân Sơn Nhất", "fullName": "Công ty Bãi xe Tân Sơn Nhất",
          "email": "owner.tsn@smartparking.vn", "phoneNumber": "0900000003",
          "isLocked": {{(isLocked ? "true" : "false")}}, "ownerStatus": "{{ownerStatus}}", "userStatus": "Active"
        }
        """;

    private static BearerTokenForwardingHandler Forwarding(HttpMessageHandler inner, HttpContext? context)
        => new(new HttpContextAccessor { HttpContext = context }) { InnerHandler = inner };

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body)
        => new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static IConfiguration Configuration(string? gatewayBaseUrl)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ServiceDb"] = "Host=localhost;Database=pm_admin_unit_test",
            ["Services:GatewayBaseUrl"] = gatewayBaseUrl,
        }).Build();

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        /// <summary>Body đọc ngay lúc gửi, vì adapter giải phóng request sau khi gọi xong.</summary>
        public List<string?> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return respond(request);
        }
    }
}
