using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using AdminService.API.Controllers;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Infrastructure.Persistence;
using AdminService.Infrastructure.Persistence.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

/// <summary>Hồ sơ đối tác giả: trả các dòng của chủ bãi được hỏi (database đã lọc trạng thái / đủ trường).</summary>
internal sealed class FakeOwnerPayoutAccounts(params OwnerPayoutAccountRow[] rows) : IOwnerPayoutAccountQueries
{
    public List<IReadOnlyCollection<int>> Calls { get; } = [];

    public Task<IReadOnlyList<OwnerPayoutAccountRow>> ListAsync(IReadOnlyCollection<int> ownerProfileIds, CancellationToken cancellationToken)
    {
        Calls.Add(ownerProfileIds);
        return Task.FromResult<IReadOnlyList<OwnerPayoutAccountRow>>([.. rows.Where(r => ownerProfileIds.Contains(r.OwnerProfileId))]);
    }
}

// US-080: tài khoản nhận tiền quyết toán của chủ bãi – quy tắc che số, chọn hồ sơ, use case và SQL.
public class OwnerPayoutAccountTests
{
    internal static OwnerPayoutAccountRow Row(int owner, int applicationId, string account = "0123456789", string business = "Công ty Bãi Xanh")
        => new(owner, applicationId, $"PA-2026-{applicationId:0000}", business, "970422", "MB Bank", account, "CONG TY BAI XANH");

    [Theory]
    [InlineData("0123456789", "****6789")]
    [InlineData("  0123456789 ", "****6789")]
    [InlineData("12345", "****2345")]
    [InlineData("1234", "****")]                // ≤ 4 ký tự → che hết, không lộ toàn bộ số
    [InlineData("", "****")]
    public void MaskAccountNumber_KeepsOnlyLastFourDigits(string input, string expected)
        => Assert.Equal(expected, OwnerPayoutAccountRules.MaskAccountNumber(input));

    [Fact]
    public void Latest_PicksNewestApplicationPerOwner()
    {
        var result = OwnerPayoutAccountRules.Latest([Row(2, 3, "1111222233"), Row(2, 8, "9999888877"), Row(5, 4)]);

        Assert.Equal(8, result[2].PartnerApplicationId);
        Assert.Equal("****8877", result[2].AccountNumberMasked);
        Assert.Equal(4, result[5].PartnerApplicationId);
    }

    [Fact]
    public async Task GetOne_ReturnsMaskedAccountWithOwnerNameAndSource()
    {
        var dto = await new GetOwnerPayoutAccountUseCase(new FakeOwnerPayoutAccounts(Row(2, 3))).ExecuteAsync(2);

        Assert.Equal(2, dto.OwnerProfileId);
        Assert.Equal("Công ty Bãi Xanh", dto.OwnerName);
        Assert.Equal("PA-2026-0003", dto.PartnerApplicationCode);
        Assert.Equal("970422", dto.BankBin);
        Assert.Equal("MB Bank", dto.BankName);
        Assert.Equal("CONG TY BAI XANH", dto.AccountName);
        Assert.Equal("****6789", dto.AccountNumberMasked);
        Assert.Equal("PartnerApplication", dto.Source);
        Assert.DoesNotContain("0123456789", JsonSerializer.Serialize(dto));     // số đầy đủ không rời Application
    }

    [Fact]
    public async Task GetOne_NoAccount_ThrowsNotFound()
        => await Assert.ThrowsAsync<NotFoundException>(() => new GetOwnerPayoutAccountUseCase(new FakeOwnerPayoutAccounts(Row(5, 4))).ExecuteAsync(2));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetOne_InvalidId_ThrowsValidation(int id)
    {
        var queries = new FakeOwnerPayoutAccounts();
        await Assert.ThrowsAsync<ValidationException>(() => new GetOwnerPayoutAccountUseCase(queries).ExecuteAsync(id));
        Assert.Empty(queries.Calls);
    }

    [Fact]
    public async Task List_KeepsRequestOrder_DeduplicatesAndReportsMissing()
    {
        var queries = new FakeOwnerPayoutAccounts(Row(2, 3), Row(7, 9, business: "Bãi Vincom"));

        var result = await new ListOwnerPayoutAccountsUseCase(queries).ExecuteAsync(" 7, 2,4,2 ");

        Assert.Equal([7, 2], result.Items.Select(i => i.OwnerProfileId));
        Assert.Equal([4], result.MissingOwnerProfileIds);
        Assert.Equal([7, 2, 4], Assert.Single(queries.Calls));                 // một truy vấn cho cả danh sách, đã bỏ trùng
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ")]
    [InlineData("1,abc")]
    [InlineData("1,0")]
    [InlineData("-3")]
    [InlineData("1.5")]
    public async Task List_InvalidIds_ThrowsValidation(string? ids)
        => await Assert.ThrowsAsync<ValidationException>(() => new ListOwnerPayoutAccountsUseCase(new FakeOwnerPayoutAccounts()).ExecuteAsync(ids));

    [Fact]
    public async Task List_MoreThan100Owners_ThrowsValidation_ExactlyHundredIsAccepted()
    {
        var useCase = new ListOwnerPayoutAccountsUseCase(new FakeOwnerPayoutAccounts());
        await useCase.ExecuteAsync(string.Join(',', Enumerable.Range(1, 100)));
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(string.Join(',', Enumerable.Range(1, 101))));
    }

    [Fact]
    public void Sql_FiltersOwnersStatusAndBankFieldsInDatabase()
    {
        using var db = new AdminDbContext(new DbContextOptionsBuilder<AdminDbContext>().UseNpgsql("Host=unused").Options);

        var sql = OwnerPayoutAccountQueries.Shape(db.PartnerApplications.AsNoTracking(), [2, 7]).ToQueryString();

        Assert.Contains("\"CreatedOwnerProfileId\" = ANY (", sql);            // lọc chủ bãi ở database (index CreatedOwnerProfileId)
        Assert.Contains("'AccountIssued'", sql);
        Assert.Contains("'Activated'", sql);
        Assert.Contains("\"PayoutBankAccountNumber\" IS NOT NULL", sql);
        Assert.DoesNotContain("\"TaxCode\"", sql);                             // chỉ chọn cột cần, không đọc thông tin pháp lý / liên hệ
        Assert.DoesNotContain("\"Phone\"", sql);
        Assert.DoesNotContain("\"Email\"", sql);
    }
}

// US-080: hai endpoint tài khoản nhận tiền trên TestServer với pipeline thật (JWT RS256 + policy RequireAdmin); database thay bằng fake.
public sealed class OwnerPayoutAccountEndpointTests : IAsyncLifetime
{
    private readonly RSA signingKey = RSA.Create(2048);
    private readonly string publicKeyPath = Path.Combine(Path.GetTempPath(), $"admin-payout-{Guid.NewGuid():N}.pem");
    private WebApplication app = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        File.WriteAllText(publicKeyPath, signingKey.ExportSubjectPublicKeyInfoPem());
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ApplicationName = typeof(AdminOwnerPayoutAccountsController).Assembly.GetName().Name,
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.AddServiceDefaults("AdminService");
        builder.Services.AddJwtAuth(publicKeyPath);
        builder.Services.AddSingleton<IOwnerPayoutAccountQueries>(new FakeOwnerPayoutAccounts(
            OwnerPayoutAccountTests.Row(2, 3), OwnerPayoutAccountTests.Row(7, 9, "5555666677", "Bãi Vincom")));
        builder.Services.AddScoped<IGetOwnerPayoutAccountUseCase, GetOwnerPayoutAccountUseCase>();
        builder.Services.AddScoped<IListOwnerPayoutAccountsUseCase, ListOwnerPayoutAccountsUseCase>();

        app = builder.Build();
        app.UseServiceDefaults();
        await app.StartAsync();
        client = app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
        signingKey.Dispose();
        try { File.Delete(publicKeyPath); } catch (IOException) { }
    }

    private string Token(string role)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", "1"), new Claim("role", role)]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(30),
            Issuer = "ParkingManagement",
            Audience = "ParkingManagement.Clients",
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(signingKey), SecurityAlgorithms.RsaSha256),
        });
    }

    private Task<HttpResponseMessage> Get(string url, string? role = "Admin")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (role is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(role));
        return client.SendAsync(request);
    }

    [Fact]
    public async Task GetOne_Admin_Returns200Masked()
    {
        var response = await Get("/api/v1/admin/owners/2/payout-account");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("0123456789", raw);
        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal("****6789", body.GetProperty("accountNumberMasked").GetString());
        Assert.Equal("Công ty Bãi Xanh", body.GetProperty("ownerName").GetString());
        Assert.Equal("PartnerApplication", body.GetProperty("source").GetString());
    }

    [Fact]
    public async Task GetOne_UnknownOwner_Returns404()
        => Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/v1/admin/owners/99/payout-account")).StatusCode);

    [Fact]
    public async Task GetOne_NonPositiveId_Returns400()
        => Assert.Equal(HttpStatusCode.BadRequest, (await Get("/api/v1/admin/owners/0/payout-account")).StatusCode);

    [Fact]
    public async Task List_Admin_Returns200WithMissing()
    {
        var response = await Get("/api/v1/admin/owners/payout-accounts?ownerProfileIds=7,99,2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([7, 2], body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("ownerProfileId").GetInt32()));
        Assert.Equal(99, Assert.Single(body.GetProperty("missingOwnerProfileIds").EnumerateArray()).GetInt32());
        Assert.Equal("****6677", body.GetProperty("items")[0].GetProperty("accountNumberMasked").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?ownerProfileIds=abc")]
    public async Task List_InvalidIds_Returns400(string query)
        => Assert.Equal(HttpStatusCode.BadRequest, (await Get("/api/v1/admin/owners/payout-accounts" + query)).StatusCode);

    [Theory]
    [InlineData("/api/v1/admin/owners/2/payout-account")]
    [InlineData("/api/v1/admin/owners/payout-accounts?ownerProfileIds=2")]
    public async Task WithoutToken_Returns401(string url)
        => Assert.Equal(HttpStatusCode.Unauthorized, (await Get(url, role: null)).StatusCode);

    [Theory]
    [InlineData("LotOwner")]
    [InlineData("Staff")]
    [InlineData("Driver")]
    public async Task NonAdmin_Returns403(string role)
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/api/v1/admin/owners/2/payout-account", role)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/api/v1/admin/owners/payout-accounts?ownerProfileIds=2", role)).StatusCode);
    }
}
