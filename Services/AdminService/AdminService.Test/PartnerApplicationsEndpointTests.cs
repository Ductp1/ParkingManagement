using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdminService.API.Controllers;
using AdminService.API.Security;
using AdminService.Application.Features.PartnerOnboarding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ParkingManagement.ServiceDefaults;
using static AdminService.Test.PartnerApplicationSubmitTests;

namespace AdminService.Test;

// US-116 T1 / US-053: API hồ sơ đối tác mới trên TestServer với pipeline thật (AddServiceDefaults + AddJwtAuth + rate limit + UseServiceDefaults).
public class PartnerApplicationsEndpointTests
{
    private const string PublicUrl = "api/v1/admin/partner-registrations";
    private const string EditUrl = "api/v1/partner-applications";
    private const string AdminUrl = "api/v1/admin/partner-applications";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Form multipart đúng như trang /partner/register gửi: các trường + 4 giấy tờ bắt buộc.</summary>
    private static MultipartFormDataContent Form(SubmitPartnerApplicationRequest r)
    {
        var form = new MultipartFormDataContent();
        void Field(string name, object? value)
        {
            if (value is not null) form.Add(new StringContent(value is bool b ? (b ? "true" : "false") : value.ToString()!), name);
        }
        Field("fullName", r.FullName); Field("email", r.Email); Field("phone", r.Phone); Field("position", r.Position);
        Field("businessType", r.BusinessType); Field("businessName", r.BusinessName); Field("taxCode", r.TaxCode);
        Field("businessAddress", r.BusinessAddress); Field("legalRepresentativeName", r.LegalRepresentativeName);
        Field("legalRepresentativePosition", r.LegalRepresentativePosition); Field("lotCount", r.LotCount);
        Field("primaryLotAddress", r.PrimaryLotAddress); Field("estimatedCarSlots", r.EstimatedCarSlots);
        Field("estimatedMotorbikeSlots", r.EstimatedMotorbikeSlots); Field("operatingHours", r.OperatingHours);
        Field("consentAccepted", r.ConsentAccepted);
        void File(string name, byte[] bytes, string fileName, string type)
        {
            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue(type);
            form.Add(part, name, fileName);
        }
        byte[] pdf = [.. "%PDF-1.7\n"u8, 1, 2, 3];
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9];
        File("businessLicenseFiles", pdf, "dkkd.pdf", "application/pdf");
        File("lotLegalProofFiles", pdf, "mat-bang.pdf", "application/pdf");
        File("idCardFrontFiles", png, "cccd-truoc.png", "image/png");
        File("idCardBackFiles", png, "cccd-sau.png", "image/png");
        return form;
    }

    [Fact] // US-116 T1: chủ bãi chưa có tài khoản nộp hồ sơ + giấy tờ KHÔNG cần đăng nhập → 201, chỉ trả mã + trạng thái.
    public async Task Anonymous_submit_returns_201_with_code_only()
    {
        await using var host = await ApiHost.StartAsync();

        var response = await host.Client.PostAsync(PublicUrl, Form(Valid()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("PA-2026-0001", body.GetProperty("code").GetString());
        Assert.Equal("Submitted", body.GetProperty("status").GetString());
        Assert.False(body.TryGetProperty("email", out _));
        var app = Assert.Single(host.Store.Applications);
        Assert.Null(app.ApplicantUserId);
        Assert.Equal(4, host.Store.Documents.Count);
        Assert.Null(Assert.Single(host.Store.Audits).UserId);
    }

    [Fact] // Đang đăng nhập (vd. tài xế muốn làm chủ bãi) → hồ sơ gắn tài khoản trong JWT.
    public async Task Logged_in_submit_links_the_account()
    {
        await using var host = await ApiHost.StartAsync();

        var response = await host.SendAsync(HttpMethod.Post, PublicUrl, "Driver", content: Form(Valid()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(7, Assert.Single(host.Store.Applications).ApplicantUserId);
    }

    [Fact]
    public async Task Invalid_submit_returns_400_problem_details()
    {
        await using var host = await ApiHost.StartAsync();

        var response = await host.Client.PostAsync(PublicUrl, Form(Valid() with { ConsentAccepted = false }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        Assert.Contains("điều khoản xử lý dữ liệu", problem!.Detail);
        Assert.Empty(host.Store.Applications);
    }

    [Fact]
    public async Task Duplicate_submit_returns_409()
    {
        await using var host = await ApiHost.StartAsync();
        await host.Client.PostAsync(PublicUrl, Form(Valid()));

        var response = await host.Client.PostAsync(PublicUrl, Form(Valid()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(host.Store.Applications);
    }

    [Fact]
    public async Task Too_many_submits_from_one_ip_returns_429()
    {
        await using var host = await ApiHost.StartAsync();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i <= PartnerRegistrationRateLimit.PermitLimit; i++)
        {
            var body = Valid() with { Email = $"p{i}@example.vn", Phone = $"090000000{i}", TaxCode = $"031234567{i}" };
            statuses.Add((await host.Client.PostAsync(PublicUrl, Form(body))).StatusCode);
        }

        Assert.All(statuses.Take(PartnerRegistrationRateLimit.PermitLimit), s => Assert.Equal(HttpStatusCode.Created, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.Equal(PartnerRegistrationRateLimit.PermitLimit, host.Store.Applications.Count);
    }

    [Fact]
    public async Task Admin_intake_requires_admin_token()
    {
        await using var host = await ApiHost.StartAsync();
        var body = new { fullName = "Trần Minh" };

        var anonymous = await host.Client.PostAsJsonAsync(AdminUrl, body, Json);
        var owner = await host.SendAsync(HttpMethod.Post, AdminUrl, "LotOwner", body);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden), (anonymous.StatusCode, owner.StatusCode));
        Assert.Empty(host.Store.Applications);
    }

    [Fact]
    public async Task Admin_intake_returns_201_with_detail_and_location()
    {
        await using var host = await ApiHost.StartAsync();
        var r = Valid();

        var response = await host.SendAsync(HttpMethod.Post, AdminUrl, "Admin", new
        {
            r.FullName, r.Email, r.Phone, r.Position, r.BusinessType, r.BusinessName, r.TaxCode, r.BusinessAddress,
            r.LegalRepresentativeName, r.LegalRepresentativePosition, r.LotCount, r.PrimaryLotAddress, r.EstimatedCarSlots,
            r.EstimatedMotorbikeSlots, r.OperatingHours, r.ConsentAccepted, intakeNote = "Gọi hotline",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PartnerApplicationDetailDto>(Json);
        Assert.Equal(("PA-2026-0001", "Submitted", "minh@example.vn"), (dto!.Code, dto.Status, dto.Email));
        Assert.EndsWith($"/{AdminUrl}/{dto.Id}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(7, Assert.Single(host.Store.Audits).UserId);
    }

    /// <summary>Phụ thuộc của controller admin không dùng trong test: tạo được (constructor), gọi tới thì ném lỗi.</summary>
    public class UnusedProxy : DispatchProxy
    {
        public static object For(Type type)
            => typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), 2, Type.EmptyTypes)!
                .MakeGenericMethod(type, typeof(UnusedProxy)).Invoke(null, null)!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new NotSupportedException($"{targetMethod?.DeclaringType?.Name}.{targetMethod?.Name} không dùng trong test này.");
    }

    [Fact]
    public async Task Partner_reads_and_resubmits_with_the_edit_link_header()
    {
        await using var host = await ApiHost.StartAsync();
        await host.Client.PostAsync(PublicUrl, Form(Valid()));
        var app = host.Store.Applications.Single();
        app.StartReview(1);
        app.RequestInfo(1, "Bổ sung số chỗ ô tô");
        var link = await new IssuePartnerEditLinkUseCase(host.Store, PartnerEditLinkOptions.Default, TimeProvider.System).ExecuteAsync(app.Id, 1);

        var noToken = await host.Client.GetAsync($"{EditUrl}/{app.Code}");
        var read = new HttpRequestMessage(HttpMethod.Get, $"{EditUrl}/{app.Code}");
        read.Headers.Add(PartnerApplicationsController.EditTokenHeader, link.Token);
        var readResponse = await host.Client.SendAsync(read);
        var put = new HttpRequestMessage(HttpMethod.Put, $"{EditUrl}/{app.Code}/resubmit") { Content = Form(Valid() with { LotCount = 4 }) };
        put.Headers.Add(PartnerApplicationsController.EditTokenHeader, link.Token);
        var putResponse = await host.Client.SendAsync(put);

        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Equal("Bổ sung số chỗ ô tô", (await readResponse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("infoRequestNote").GetString());
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        Assert.Equal((AdminService.Domain.Enums.PartnerApplicationStatus.UnderReview, 4), (app.Status, app.LotCount));
    }

    private sealed class MemoryFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> files = [];
        public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken) { files[key] = content; return Task.CompletedTask; }
        public Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult(files.GetValueOrDefault(key));
        public Task DeleteAsync(string key, CancellationToken cancellationToken) { files.Remove(key); return Task.CompletedTask; }
    }

    private sealed class FixedTime(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class ApiHost : IAsyncDisposable
    {
        private const int AdminUserId = 7;
        private readonly RSA signingKey = RSA.Create(2048);
        private readonly string publicKeyPath = Path.Combine(Path.GetTempPath(), $"admin-partner-apply-{Guid.NewGuid():N}.pem");
        private WebApplication app = null!;

        public HttpClient Client { get; private set; } = null!;
        public InMemoryIntake Store { get; } = new();

        public static async Task<ApiHost> StartAsync(int perHour = 5)
        {
            var host = new ApiHost();
            await File.WriteAllTextAsync(host.publicKeyPath, host.signingKey.ExportSubjectPublicKeyInfoPem());

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing",
                ApplicationName = typeof(PartnerApplicationsController).Assembly.GetName().Name,
            });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PartnerApplicationRateLimit.PerHourKey] = perHour.ToString(),
            });
            builder.AddServiceDefaults("AdminService");
            builder.Services.AddJwtAuth(host.publicKeyPath);
            builder.Services.AddPartnerRegistrationRateLimit();
            builder.Services.AddPartnerApplicationRateLimit(builder.Configuration);
            // Chỉ dùng 2 endpoint tạo hồ sơ – các controller khác của assembly không được gọi nên không cần đăng ký phụ thuộc.
            builder.Services.AddSingleton<TimeProvider>(new FixedTime(new DateTime(2026, 11, 13, 2, 0, 0, DateTimeKind.Utc)));
            builder.Services.AddSingleton<IPartnerApplicationIntakeRepository>(host.Store);
            builder.Services.AddSingleton<IPartnerApplicationQueries>(host.Store);
            builder.Services.AddSingleton<IPartnerRegistrationRepository>(host.Store);
            builder.Services.AddSingleton<IFileStorage, MemoryFileStorage>();
            builder.Services.AddSingleton(TestAdminAlerts.Create());
            builder.Services.AddScoped<ISubmitPartnerApplicationUseCase, SubmitPartnerApplicationUseCase>();
            builder.Services.AddScoped<ICreatePartnerApplicationUseCase, CreatePartnerApplicationUseCase>();
            builder.Services.AddSingleton<IPartnerApplicationRepository>(host.Store);
            builder.Services.AddScoped<IGetPartnerApplicationForEditUseCase, GetPartnerApplicationForEditUseCase>();
            builder.Services.AddScoped<IResubmitPartnerApplicationUseCase, ResubmitPartnerApplicationUseCase>();
            foreach (var type in new[]
                     {
                         typeof(IListPartnerApplicationsUseCase), typeof(IGetPartnerApplicationUseCase), typeof(IStartPartnerReviewUseCase),
                         typeof(IRequestPartnerInfoUseCase), typeof(IRejectPartnerApplicationUseCase), typeof(ICancelPartnerApplicationUseCase),
                         typeof(IIssueOwnerAccountUseCase), typeof(IResendOwnerActivationUseCase), typeof(IIssuePartnerEditLinkUseCase),
                         typeof(IListMyPartnerApplicationsUseCase), typeof(IListPartnerDocumentsUseCase), typeof(IGetPartnerDocumentFileUseCase),
                         typeof(IRetryPartnerCleanupUseCase), typeof(IResendPartnerInfoRequestUseCase),
                     })
                builder.Services.AddSingleton(type, _ => UnusedProxy.For(type));

            host.app = builder.Build();
            host.app.UsePartnerApplicationRateLimit();
            host.app.UseServiceDefaults();
            await host.app.StartAsync();
            host.Client = host.app.GetTestClient();
            return host;
        }

        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string role, object? body = null, HttpContent? content = null)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MintToken(role));
            if (content is not null) request.Content = content;
            else if (body is not null) request.Content = JsonContent.Create(body, options: Json);
            return Client.SendAsync(request);
        }

        private string MintToken(string role)
        {
            var now = DateTime.UtcNow;
            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([new Claim("sub", AdminUserId.ToString()), new Claim("role", role)]),
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
}
