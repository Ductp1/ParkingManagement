using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ParkingManagement.IntegrationTests.PerService;

/// <summary>
/// US-053: form public đăng ký đối tác chủ bãi + giấy tờ pháp lý trên AdminService thật (pm_admin + kho file local).
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
        File("representativeIdFiles", Png, "cccd.png", "image/png");
        return form;
    }

    private static string UniqueTaxCode() => $"09{Random.Shared.NextInt64(10_000_000, 99_999_999)}";

    [Fact]
    public async Task Public_registration_keeps_legal_documents_admin_only_until_reviewed()
    {
        var taxCode = UniqueTaxCode();
        using var anonymous = fixture.AdminFactory.CreateClient();

        // 1. Nộp hồ sơ không cần đăng nhập → 201 + mã hồ sơ, trạng thái Submitted
        var submit = await anonymous.PostAsync(RegisterUrl, Form(taxCode, $"it.{taxCode}@baixe.test", Pdf));
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var receipt = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var code = receipt.GetProperty("code").GetString()!;
        Assert.Matches(@"^PA-\d{4}-\d{4}$", code);
        Assert.Equal("Submitted", receipt.GetProperty("status").GetString());

        // Gửi lại cùng mã số thuế khi hồ sơ cũ còn chờ → 409
        Assert.Equal(HttpStatusCode.Conflict, (await anonymous.PostAsync(RegisterUrl, Form(taxCode, $"other.{taxCode}@baixe.test", Pdf))).StatusCode);

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
        Assert.Equal(2, documents.GetArrayLength());
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

    [Fact] // File đổi đuôi .pdf nhưng nội dung không phải PDF / ảnh → 400, không tạo hồ sơ.
    public async Task Public_registration_rejects_file_whose_content_is_not_pdf_or_image()
    {
        var taxCode = UniqueTaxCode();
        using var anonymous = fixture.AdminFactory.CreateClient();

        var response = await anonymous.PostAsync(RegisterUrl, Form(taxCode, $"bad.{taxCode}@baixe.test", "<html>not a pdf</html>"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var page = await fixture.AdminClient.GetFromJsonAsync<JsonElement>($"{AdminUrl}?search={taxCode}");
        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
    }
}
