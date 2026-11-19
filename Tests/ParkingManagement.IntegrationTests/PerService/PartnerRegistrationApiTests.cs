using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UserService.Infrastructure.Persistence;

namespace ParkingManagement.IntegrationTests.PerService;

/// <summary>
/// US-053 / US-116: form public đăng ký đối tác chủ bãi + giấy tờ pháp lý trên AdminService thật (pm_admin + kho file local).
/// Kiểm tra: nộp không cần đăng nhập → Submitted; chỉ Admin xem được giấy tờ (không token 401, chủ bãi / tài xế 403);
/// phải bắt đầu thẩm định rồi mới cấp tài khoản được.
/// </summary>
[Collection("Admin API")]
public class PartnerRegistrationApiTests(AdminApiFixture fixture)
{
    private const string RegisterUrl = "/api/v1/admin/partner-registrations";
    private const string AdminUrl = "/api/v1/admin/partner-applications";
    private static readonly byte[] Pdf = [.. "%PDF-1.7\n%test\n"u8];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private static MultipartFormDataContent Form(string taxCode, string email, byte[] license)
    {
        var form = new MultipartFormDataContent();
        void Text(string name, string value) => form.Add(new StringContent(value), name);
        void File(string name, byte[] bytes, string fileName, string type)
        {
            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue(type);
            form.Add(part, name, fileName);
        }

        Text("fullName", "Trần Thị Bình");
        Text("email", email);
        Text("phone", "0912345678");
        Text("businessType", "Company");
        Text("businessName", "Công ty TNHH Bãi xe Bình An");
        Text("taxCode", taxCode);
        Text("businessAddress", "1 Võ Văn Ngân, TP. Thủ Đức");
        Text("legalRepresentativeName", "Trần Thị Bình");
        Text("lotCount", "1");
        Text("primaryLotAddress", "10 Kha Vạn Cân, TP. Thủ Đức");
        Text("estimatedCarSlots", "30");
        Text("consentAccepted", "true");
        File("businessLicenseFiles", license, "dang-ky-kinh-doanh.pdf", "application/pdf");
        File("lotLegalProofFiles", Pdf, "hop-dong-thue-mat-bang.pdf", "application/pdf");
        File("idCardFrontFiles", Png, "cccd-mat-truoc.png", "image/png");
        File("idCardBackFiles", Png, "cccd-mat-sau.png", "image/png");
        return form;
    }

    // Lưu ý: form giới hạn 5 lần gửi / 10 phút / IP và TestServer chỉ có 1 IP – collection này đã gửi đúng 5 POST.
    // Thêm test gửi hồ sơ nữa sẽ nhận 429: gộp vào test có sẵn hoặc nâng giới hạn cho môi trường test.

    /// <summary>Tài khoản tài xế giả (id lớn, không trùng seed) – mỗi lần chạy fixture là DB mới nên không vướng "1 hồ sơ đang chờ / tài khoản".</summary>
    private const int ApplicantUserId = 9001;

    private static string UniqueTaxCode() => $"09{Random.Shared.NextInt64(10_000_000, 99_999_999)}";

    [Fact]
    public async Task Registration_without_or_with_login_keeps_legal_documents_admin_only_until_reviewed()
    {
        var taxCode = UniqueTaxCode();
        using var anonymous = fixture.AdminFactory.CreateClient();
        using var applicant = fixture.CreateClientWithRole(ApplicantUserId, "Driver");

        // 1. Chủ bãi chưa có tài khoản nộp được KHÔNG cần đăng nhập (US-116) → 201, hồ sơ không gắn tài khoản.
        var anonymousTax = UniqueTaxCode();
        var anonymousSubmit = await anonymous.PostAsync(RegisterUrl, Form(anonymousTax, $"anon.{anonymousTax}@baixe.test", Pdf));
        Assert.Equal(HttpStatusCode.Created, anonymousSubmit.StatusCode);
        Assert.Equal("Submitted", (await anonymousSubmit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        // Tài khoản đã đăng nhập nộp → 201 + mã hồ sơ, trạng thái Submitted, hồ sơ gắn với tài khoản đó
        var submit = await applicant.PostAsync(RegisterUrl, Form(taxCode, $"it.{taxCode}@baixe.test", Pdf));
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var receipt = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var code = receipt.GetProperty("code").GetString()!;
        Assert.Matches(@"^PA-\d{4}-\d{4}$", code);
        Assert.Equal("Submitted", receipt.GetProperty("status").GetString());

        // Cùng tài khoản nộp tiếp khi hồ sơ cũ còn chờ (dù khác MST / email) → 409
        var otherTax = UniqueTaxCode();
        Assert.Equal(HttpStatusCode.Conflict,
            (await applicant.PostAsync(RegisterUrl, Form(otherTax, $"other.{otherTax}@baixe.test", Pdf))).StatusCode);

        // Người nộp xem trạng thái hồ sơ của mình; tài khoản khác không thấy; chưa đăng nhập 401
        var mine = await applicant.GetFromJsonAsync<JsonElement>($"{RegisterUrl}/mine");
        var own = mine.EnumerateArray().Single(a => a.GetProperty("code").GetString() == code);
        Assert.Equal("Submitted", own.GetProperty("status").GetString());
        Assert.Equal(4, own.GetProperty("documentCount").GetInt32());
        Assert.False(own.TryGetProperty("documents", out _));     // không trả giấy tờ cho người nộp
        using var stranger = fixture.CreateClientWithRole(ApplicantUserId + 100, "Driver");
        Assert.Equal(0, (await stranger.GetFromJsonAsync<JsonElement>($"{RegisterUrl}/mine")).GetArrayLength());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{RegisterUrl}/mine")).StatusCode);

        // 2. Admin thấy hồ sơ trong hàng chờ
        var page = await fixture.AdminClient.GetFromJsonAsync<JsonElement>($"{AdminUrl}?search={code}");
        var id = page.GetProperty("items")[0].GetProperty("id").GetInt32();
        var documentsUrl = $"{AdminUrl}/{id}/documents";

        // 3. Giấy tờ: không token 401, chủ bãi / tài xế 403, Admin 200
        using var owner = fixture.CreateClientWithRole(2, "LotOwner");
        using var driver = fixture.CreateClientWithRole(5, "Driver");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(documentsUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(documentsUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await driver.GetAsync(documentsUrl)).StatusCode);

        var documents = await fixture.AdminClient.GetFromJsonAsync<JsonElement>(documentsUrl);
        Assert.Equal(4, documents.GetArrayLength());
        Assert.Equal(["BusinessLicense", "IdCardBack", "IdCardFront", "LotLegalProof"],
            documents.EnumerateArray().Select(d => d.GetProperty("documentType").GetString()!).Order().ToArray());
        var license = documents.EnumerateArray().Single(d => d.GetProperty("documentType").GetString() == "BusinessLicense");
        Assert.Equal("dang-ky-kinh-doanh.pdf", license.GetProperty("originalFileName").GetString());
        var fileUrl = $"{documentsUrl}/{license.GetProperty("id").GetInt32()}/file";

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(fileUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(fileUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await driver.GetAsync(fileUrl)).StatusCode);

        var file = await fixture.AdminClient.GetAsync(fileUrl);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("application/pdf", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Pdf, await file.Content.ReadAsByteArrayAsync());
        Assert.True(file.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.AdminClient.GetAsync($"{documentsUrl}/999999/file")).StatusCode);

        // 4. Chưa bắt đầu thẩm định thì chưa cấp tài khoản được (409); bắt đầu thẩm định → UnderReview
        var bank = new { bank = new { bin = "970436", name = "Vietcombank", accountNumber = "0123456789", accountName = "CONG TY BINH AN" } };
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.AdminClient.PostAsJsonAsync($"{AdminUrl}/{id}/issue-account", bank)).StatusCode);
        var review = await fixture.AdminClient.PostAsync($"{AdminUrl}/{id}/start-review", null);
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.Equal("UnderReview", (await review.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact] // US-053 trọn luồng: driver2 (tài khoản seed thật) nộp hồ sơ → Admin thẩm định → duyệt → driver2 có 2 role Driver + LotOwner.
    public async Task Approved_applicant_account_gets_lot_owner_role_and_keeps_driver_role()
    {
        const int driver2 = 6;   // DemoIds: driver2@smartparking.vn
        var taxCode = UniqueTaxCode();

        // Phiên đăng nhập thật của driver2 (refresh token trong pm_user) để kiểm tra token mới sau khi được duyệt.
        var rawRefresh = UserService.Application.Features.Auth.RefreshTokenCodec.Generate();
        await using (var scope = fixture.UserFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            db.RefreshTokens.Add(new UserService.Domain.Entities.RefreshToken
            {
                UserId = driver2,
                TokenHash = UserService.Application.Features.Auth.RefreshTokenCodec.Hash(rawRefresh),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                CreatedByIp = "integration-test",
            });
            await db.SaveChangesAsync();
        }

        using var applicant = fixture.CreateClientWithRole(driver2, "Driver");
        var submit = await applicant.PostAsync(RegisterUrl, Form(taxCode, $"driver2.{taxCode}@baixe.test", Pdf));
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var code = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
        var id = (await fixture.AdminClient.GetFromJsonAsync<JsonElement>($"{AdminUrl}?search={code}"))
            .GetProperty("items")[0].GetProperty("id").GetInt32();

        // Admin xem giấy tờ → bắt đầu thẩm định → duyệt (cấp quyền chủ bãi + tài khoản nhận tiền)
        Assert.Equal(HttpStatusCode.OK, (await fixture.AdminClient.GetAsync($"{AdminUrl}/{id}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.AdminClient.PostAsync($"{AdminUrl}/{id}/start-review", null)).StatusCode);
        var bank = new { bank = new { bin = "970436", name = "Vietcombank", accountNumber = "0123456789", accountName = "TRAN THI BINH" } };
        var approve = await fixture.AdminClient.PostAsJsonAsync($"{AdminUrl}/{id}/issue-account", bank);
        Assert.True(approve.IsSuccessStatusCode, await approve.Content.ReadAsStringAsync());
        var approved = await approve.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Activated", approved.GetProperty("status").GetString());   // tài khoản đã hoạt động sẵn, không cần link kích hoạt
        Assert.Equal(driver2, approved.GetProperty("createdUserId").GetInt32());
        var ownerProfileId = approved.GetProperty("createdOwnerProfileId").GetInt32();

        // Chính tài khoản driver2 giờ có 2 role và hồ sơ chủ bãi khớp
        var user = await fixture.AdminClient.GetFromJsonAsync<JsonElement>($"/api/v1/admin/users/{driver2}");
        var roles = user.GetProperty("roles").EnumerateArray().Select(r => r.GetString()!).Order().ToArray();
        Assert.Equal(["Driver", "LotOwner"], roles);
        Assert.Equal(ownerProfileId, user.GetProperty("ownerProfile").GetProperty("id").GetInt32());

        // Làm mới phiên → token mới có cả 2 role + OwnerProfileId (vào được phân hệ chủ bãi, thêm Staff…)
        using var userApi = fixture.UserFactory.CreateClient();
        var refreshed = await userApi.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = rawRefresh });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var accessToken = (await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var claims = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.ToList();
        Assert.Contains(claims, c => c.Type == "role" && c.Value == "Driver");
        Assert.Contains(claims, c => c.Type == "role" && c.Value == "LotOwner");
        Assert.Contains(claims, c => c.Type == "OwnerProfileId" && c.Value == ownerProfileId.ToString());

        // Chủ bãi mới vào được chức năng chủ bãi: xem / thêm Staff (US-075, policy RequireOwner)
        userApi.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        Assert.Equal(HttpStatusCode.OK, (await userApi.GetAsync("/api/v1/users/me/staff")).StatusCode);

        // Duyệt lại → 409 (hồ sơ đã kết thúc), không tạo hồ sơ chủ bãi thứ hai
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.AdminClient.PostAsJsonAsync($"{AdminUrl}/{id}/issue-account", bank)).StatusCode);
        var again = await fixture.AdminClient.GetFromJsonAsync<JsonElement>($"/api/v1/admin/users/{driver2}");
        Assert.Equal(ownerProfileId, again.GetProperty("ownerProfile").GetProperty("id").GetInt32());
    }

    [Fact] // File đổi đuôi .pdf nhưng nội dung không phải PDF / ảnh → 400, không tạo hồ sơ.
    public async Task Registration_rejects_file_whose_content_is_not_pdf_or_image()
    {
        var taxCode = UniqueTaxCode();
        using var applicant = fixture.CreateClientWithRole(ApplicantUserId + 1, "Driver");

        var response = await applicant.PostAsync(RegisterUrl, Form(taxCode, $"bad.{taxCode}@baixe.test", "<html>not a pdf</html>"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var page = await fixture.AdminClient.GetFromJsonAsync<JsonElement>($"{AdminUrl}?search={taxCode}");
        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
    }
}
