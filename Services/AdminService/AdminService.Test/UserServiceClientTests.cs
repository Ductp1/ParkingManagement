using System.Net;
using System.Text;
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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
