using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdminService.API.Controllers;
using AdminService.Application.Features.Owners;
using AdminService.Application.Features.Sanctions;
using AdminService.Application.Features.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using static AdminService.Test.SanctionFixtures;

namespace AdminService.Test;

// US-097: API chế tài chạy trên TestServer với pipeline thật của AdminService (AddServiceDefaults + AddJwtAuth + UseServiceDefaults):
// JWT RS256 ký bằng khóa test, policy RequireAdmin, ExceptionHandlingMiddleware → ProblemDetails. Chỉ thay database / service khác bằng fake.
public class SanctionsEndpointTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Without_token_returns_401()
    {
        await using var host = await SanctionApiHost.StartAsync();

        var response = await host.Client.GetAsync("api/v1/admin/sanctions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("LotOwner")]
    [InlineData("Driver")]
    public async Task Non_admin_role_returns_403(string role)
    {
        await using var host = await SanctionApiHost.StartAsync();

        var list = await host.SendAsync(HttpMethod.Get, "api/v1/admin/sanctions", role);
        var issue = await host.SendAsync(HttpMethod.Post, "api/v1/admin/sanctions", role, WarningBody([LotBenThanh]));
        var suggestion = await host.SendAsync(HttpMethod.Get, $"api/v1/admin/owners/{OwnerTsn}/sanction-suggestion", role);

        Assert.All([list, issue, suggestion], r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        Assert.Empty(host.Store.Sanctions);
    }

    [Fact]
    public async Task Admin_issues_lists_reads_and_revokes_a_sanction()
    {
        await using var host = await SanctionApiHost.StartAsync();

        var issued = await host.SendAsync(HttpMethod.Post, "api/v1/admin/sanctions", "Admin", WarningBody([LotBenThanh, LotTanSonNhat]));
        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
        var result = await issued.Content.ReadFromJsonAsync<IssueSanctionResultDto>(Json);
        Assert.Equal(2, result!.Sanctions.Count);
        Assert.All(result.Sanctions, s => Assert.Equal(Admin, s.IssuedByUserId));      // người áp lấy từ JWT

        var list = await (await host.SendAsync(HttpMethod.Get, "api/v1/admin/sanctions?status=Active&level=Warning&page=1&pageSize=1", "Admin"))
            .Content.ReadFromJsonAsync<PagedResult<SanctionSummaryDto>>(Json);
        Assert.Equal((2, 1), (list!.TotalCount, list.Items.Count));

        var id = result.Sanctions[0].Id;
        var detail = await (await host.SendAsync(HttpMethod.Get, $"api/v1/admin/sanctions/{id}", "Admin"))
            .Content.ReadFromJsonAsync<SanctionDetailDto>(Json);
        Assert.Equal("Sanction.Issued", Assert.Single(detail!.Timeline).Action);

        var stats = await (await host.SendAsync(HttpMethod.Get, "api/v1/admin/sanctions/stats", "Admin")).Content.ReadFromJsonAsync<SanctionStatsDto>(Json);
        Assert.Equal(2, stats!.Warning);

        var revoked = await host.SendAsync(HttpMethod.Post, $"api/v1/admin/sanctions/{id}/revoke", "Admin", new { reason = "Đã khắc phục" });
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal("Revoked", (await revoked.Content.ReadFromJsonAsync<SanctionDetailDto>(Json))!.Sanction.Status);

        var suggestion = await host.SendAsync(HttpMethod.Get, $"api/v1/admin/owners/{OwnerTsn}/sanction-suggestion", "Admin");
        Assert.Equal("RankingPenalty", (await suggestion.Content.ReadFromJsonAsync<SanctionSuggestionDto>(Json))!.SuggestedLevel);
    }

    [Fact]
    public async Task Invalid_input_returns_400_problem_details()
    {
        await using var host = await SanctionApiHost.StartAsync();

        var noLevel = await host.SendAsync(HttpMethod.Post, "api/v1/admin/sanctions", "Admin",
            new { ownerProfileId = OwnerTsn, ownerUserId = OwnerTsnUser, reason = "x" });
        var badEnum = await host.SendAsync(HttpMethod.Get, "api/v1/admin/sanctions?status=Paused", "Admin");
        var longRanking = await host.SendAsync(HttpMethod.Post, "api/v1/admin/sanctions", "Admin", new
        {
            ownerProfileId = OwnerTsn, ownerUserId = OwnerTsnUser, level = "RankingPenalty", parkingLotIds = new[] { LotBenThanh },
            reason = "Tụt hạng", endsAtUtc = NowUtc.AddDays(31),
        });

        Assert.Equal(HttpStatusCode.BadRequest, noLevel.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badEnum.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longRanking.StatusCode);
        Assert.Contains("30 ngày", (await longRanking.Content.ReadFromJsonAsync<ProblemDetails>(Json))!.Detail);
    }

    [Fact]
    public async Task Missing_sanction_returns_404()
    {
        await using var host = await SanctionApiHost.StartAsync();

        var detail = await host.SendAsync(HttpMethod.Get, "api/v1/admin/sanctions/999", "Admin");
        var revoke = await host.SendAsync(HttpMethod.Post, "api/v1/admin/sanctions/999/revoke", "Admin", new { reason = "x" });

        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
    }

    [Fact]
    public async Task Duplicate_listing_suspension_returns_409_naming_the_lot()
    {
        await using var host = await SanctionApiHost.StartAsync();
        object Suspend(params int[] lots) => new
        {
            ownerProfileId = OwnerTsn, ownerUserId = OwnerTsnUser, level = "TemporarySuspension", parkingLotIds = lots,
            reason = "Mất kết nối thiết bị kéo dài", endsAtUtc = NowUtc.AddDays(3),
        };

        Assert.Equal(HttpStatusCode.Created, (await host.SendAsync(HttpMethod.Post, "api/v1/admin/sanctions", "Admin", Suspend(LotTanSonNhat))).StatusCode);
        var conflict = await host.SendAsync(HttpMethod.Post, "api/v1/admin/sanctions", "Admin", Suspend(LotBenThanh, LotTanSonNhat));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains($"Bãi {LotTanSonNhat}", (await conflict.Content.ReadFromJsonAsync<ProblemDetails>(Json))!.Detail);
        Assert.Single(host.Store.Sanctions);
    }

    private static object WarningBody(int[] lots) => new
    {
        ownerProfileId = OwnerTsn, ownerUserId = OwnerTsnUser, level = "Warning", parkingLotIds = lots,
        reason = "Phản hồi khiếu nại quá 48 giờ", correctionDueAtUtc = NowUtc.AddDays(7), penaltyAmount = 200000,
    };

    private sealed class SanctionApiHost : IAsyncDisposable
    {
        private readonly RSA signingKey = RSA.Create(2048);
        private readonly string publicKeyPath = Path.Combine(Path.GetTempPath(), $"admin-sanctions-{Guid.NewGuid():N}.pem");
        private WebApplication app = null!;

        public HttpClient Client { get; private set; } = null!;
        public InMemorySanctionStore Store { get; } = new();

        public static async Task<SanctionApiHost> StartAsync()
        {
            var host = new SanctionApiHost();
            await File.WriteAllTextAsync(host.publicKeyPath, host.signingKey.ExportSubjectPublicKeyInfoPem());

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing",
                ApplicationName = typeof(AdminSanctionsController).Assembly.GetName().Name,
            });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.AddServiceDefaults("AdminService");
            builder.Services.AddJwtAuth(host.publicKeyPath);

            var time = new FixedTime(Now);
            builder.Services.AddSingleton<TimeProvider>(time);
            builder.Services.AddSingleton<IUserServiceClient>(new FakeOwnerDirectoryUserService(TsnOwner, VincomOwner));
            builder.Services.AddSingleton<IParkingLotDirectory>(new FakeLotDirectory((OwnerTsn, LotBenThanh), (OwnerTsn, LotTanSonNhat)));
            builder.Services.AddSingleton(new SanctionSettings());
            builder.Services.AddSingleton<IOwnerLockRepository>(host.Store);
            builder.Services.AddSingleton<ISanctionRepository>(host.Store);
            builder.Services.AddSingleton<ISanctionAdminQueries>(host.Store);
            builder.Services.AddSingleton<ISanctionQueries, NoOwnerSanctionQueries>();
            builder.Services.AddScoped<ILockOwnerUseCase, LockOwnerUseCase>();
            builder.Services.AddScoped<IUnlockOwnerUseCase, UnlockOwnerUseCase>();
            builder.Services.AddScoped<IGetOwnerSanctionsUseCase, GetOwnerSanctionsUseCase>();
            builder.Services.AddScoped<IIssueSanctionUseCase, IssueSanctionUseCase>();
            builder.Services.AddScoped<IRevokeSanctionUseCase, RevokeSanctionUseCase>();
            builder.Services.AddScoped<IListSanctionsUseCase, ListSanctionsUseCase>();
            builder.Services.AddScoped<IGetSanctionUseCase, GetSanctionUseCase>();
            builder.Services.AddScoped<IGetSanctionStatsUseCase, GetSanctionStatsUseCase>();
            builder.Services.AddScoped<IGetSanctionSuggestionUseCase, GetSanctionSuggestionUseCase>();

            host.app = builder.Build();
            host.app.UseServiceDefaults();
            await host.app.StartAsync();
            host.Client = host.app.GetTestClient();
            return host;
        }

        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string role, object? body = null)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MintToken(role));
            if (body is not null) request.Content = JsonContent.Create(body, options: Json);
            return Client.SendAsync(request);
        }

        /// <summary>JWT RS256 đúng hợp đồng UserService (issuer / audience của AuthExtensions, claim "sub", "role").</summary>
        private string MintToken(string role)
        {
            var now = DateTime.UtcNow;
            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([new Claim("sub", Admin.ToString()), new Claim("role", role)]),
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(30),
                Issuer = "ParkingManagement",
                Audience = "ParkingManagement.Clients",
                SigningCredentials = new SigningCredentials(new RsaSecurityKey(signingKey), SecurityAlgorithms.RsaSha256),
            });
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
            signingKey.Dispose();
            File.Delete(publicKeyPath);
        }
    }

    private sealed class NoOwnerSanctionQueries : ISanctionQueries
    {
        public Task<PagedResult<SanctionDto>> ListByOwnerAsync(int ownerProfileId, int page, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult(new PagedResult<SanctionDto>([], page, pageSize, 0));
    }
}
