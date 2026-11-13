using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-053: form public đăng ký đối tác chủ bãi + giấy tờ pháp lý (fake repository / storage, không cần database).
public class PartnerRegistrationTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 3, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Pdf = [.. "%PDF-1.7\n"u8, 1, 2, 3];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9];

    private static SubmitPartnerApplicationRequest Valid(bool consent = true) => new(
        "Nguyễn Văn An", "An.Nguyen@BaiXe.vn", "090 123 4567", "Giám đốc", PartnerBusinessType.Company, "Công ty TNHH Bãi xe An Phú",
        "0312345678", "12 Lê Lợi, Quận 1, TP.HCM", "Nguyễn Văn An", "Giám đốc", 2, "45 Nguyễn Huệ, Quận 1", 40, 120, "06:00-22:00",
        consent);

    private static PartnerDocumentUpload Doc(PartnerDocumentType type, byte[] content, string name = "giay-to.pdf")
        => new(type, new UploadedFile(name, content.Length, content));

    private static List<PartnerDocumentUpload> ValidDocs() =>
    [
        Doc(PartnerDocumentType.BusinessLicense, Pdf, @"C:\Users\an\Desktop\dkkd.pdf"),
        Doc(PartnerDocumentType.RepresentativeId, Png, "cccd.png"),
    ];

    private sealed class FakeStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken) { Files[key] = content; return Task.CompletedTask; }
        public Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult(Files.GetValueOrDefault(key));
        public Task DeleteAsync(string key, CancellationToken cancellationToken) { Files.Remove(key); return Task.CompletedTask; }
    }

    private sealed class FakeRepository : IPartnerRegistrationRepository
    {
        public bool HasOpen { get; init; }
        public bool FailOnSave { get; init; }
        public PartnerApplication? Saved { get; private set; }
        public IReadOnlyList<PartnerApplicationDocument> SavedDocuments { get; private set; } = [];

        public Task<bool> HasOpenApplicationAsync(string taxCode, string email, CancellationToken cancellationToken) => Task.FromResult(HasOpen);

        public Task<string> AddWithNewCodeAsync(PartnerApplication application, IReadOnlyList<PartnerApplicationDocument> documents, int year,
            CancellationToken cancellationToken)
        {
            if (FailOnSave) throw new InvalidOperationException("database down");
            application.Code = $"PA-{year}-0013";
            Saved = application;
            SavedDocuments = documents;
            return Task.FromResult(application.Code);
        }
    }

    private static SubmitPartnerApplicationUseCase UseCase(FakeRepository repository, FakeStorage storage)
        => new(repository, storage, new FixedClock());

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    [Fact] // AC: nộp đủ thông tin + giấy tờ → hồ sơ Submitted chờ Admin thẩm định, file lưu bằng khóa do server sinh.
    public async Task Submit_valid_application_stores_documents_and_waits_for_review()
    {
        var repository = new FakeRepository();
        var storage = new FakeStorage();

        var receipt = await UseCase(repository, storage).ExecuteAsync(Valid(), ValidDocs());

        Assert.Equal("PA-2026-0013", receipt.Code);
        Assert.Equal(nameof(PartnerApplicationStatus.Submitted), receipt.Status);
        var app = repository.Saved!;
        Assert.Equal(PartnerApplicationStatus.Submitted, app.Status);
        Assert.Equal("an.nguyen@baixe.vn", app.Email);
        Assert.Equal("0901234567", app.Phone);
        Assert.Equal(Now, app.ConsentAcceptedAtUtc);
        Assert.Null(app.BusinessLicenseUrl);                         // không lưu URL do client gửi

        Assert.Equal(2, repository.SavedDocuments.Count);
        var license = repository.SavedDocuments[0];
        Assert.Equal(PartnerDocumentType.BusinessLicense, license.DocumentType);
        Assert.Equal("application/pdf", license.ContentType);
        Assert.Equal("dkkd.pdf", license.OriginalFileName);         // bỏ đường dẫn máy người nộp
        Assert.Matches(@"^partner-applications/[0-9a-f]{32}/business-license-1\.pdf$", license.FileKey);
        Assert.Equal("image/png", repository.SavedDocuments[1].ContentType);
        Assert.Equal(64, license.Sha256.Length);
        Assert.Equal(Pdf, storage.Files[license.FileKey]);
    }

    [Theory] // Thiếu giấy tờ bắt buộc → 400, không ghi file nào.
    [InlineData(PartnerDocumentType.BusinessLicense, "giấy đăng ký kinh doanh")]
    [InlineData(PartnerDocumentType.RepresentativeId, "giấy tờ tùy thân")]
    public async Task Submit_without_required_document_is_rejected(PartnerDocumentType missing, string message)
    {
        var storage = new FakeStorage();
        var docs = ValidDocs().Where(d => d.Type != missing).ToList();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => UseCase(new FakeRepository(), storage).ExecuteAsync(Valid(), docs));

        Assert.Contains(message, ex.Message);
        Assert.Empty(storage.Files);
    }

    [Fact] // File đổi đuôi .pdf nhưng nội dung không phải PDF / ảnh → 400.
    public async Task Submit_with_fake_pdf_is_rejected()
    {
        var docs = ValidDocs();
        docs[0] = Doc(PartnerDocumentType.BusinessLicense, "<script>alert(1)</script>"u8.ToArray(), "dkkd.pdf");
        var storage = new FakeStorage();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => UseCase(new FakeRepository(), storage).ExecuteAsync(Valid(), docs));

        Assert.Contains("PDF, JPG hoặc PNG", ex.Message);
        Assert.Empty(storage.Files);
    }

    [Fact] // File > 10 MB → 400.
    public async Task Submit_with_oversized_document_is_rejected()
    {
        var docs = ValidDocs();
        docs[1] = new PartnerDocumentUpload(PartnerDocumentType.RepresentativeId,
            new UploadedFile("cccd.png", PartnerDocumentRules.MaxFileBytes + 1, []));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => UseCase(new FakeRepository(), new FakeStorage()).ExecuteAsync(Valid(), docs));
        Assert.Contains("10 MB", ex.Message);
    }

    [Fact] // Quá 6 file → 400.
    public async Task Submit_with_too_many_documents_is_rejected()
    {
        var docs = ValidDocs();
        for (var i = 0; i < 5; i++) docs.Add(Doc(PartnerDocumentType.Other, Pdf));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => UseCase(new FakeRepository(), new FakeStorage()).ExecuteAsync(Valid(), docs));
        Assert.Contains("Tối đa 6 file", ex.Message);
    }

    [Fact] // Không đồng ý điều khoản → 400.
    public async Task Submit_without_consent_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(
            () => UseCase(new FakeRepository(), new FakeStorage()).ExecuteAsync(Valid(consent: false), ValidDocs()));

    [Theory] // Dữ liệu sai định dạng → 400.
    [InlineData("taxCode", "12345", "Mã số thuế")]
    [InlineData("email", "khong-phai-email", "Email")]
    [InlineData("phone", "12ab", "Số điện thoại")]
    public async Task Submit_with_invalid_field_is_rejected(string field, string value, string message)
    {
        var request = field switch
        {
            "taxCode" => Valid() with { TaxCode = value },
            "email" => Valid() with { Email = value },
            _ => Valid() with { Phone = value },
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => UseCase(new FakeRepository(), new FakeStorage()).ExecuteAsync(request, ValidDocs()));
        Assert.Contains(message, ex.Message);
    }

    [Fact] // Đã có hồ sơ đang chờ cùng MST / email → 409, không ghi file.
    public async Task Submit_when_open_application_exists_is_conflict()
    {
        var storage = new FakeStorage();
        await Assert.ThrowsAsync<ConflictException>(
            () => UseCase(new FakeRepository { HasOpen = true }, storage).ExecuteAsync(Valid(), ValidDocs()));
        Assert.Empty(storage.Files);
    }

    [Fact] // Lưu database lỗi → xóa các file đã ghi, không để file mồ côi.
    public async Task Submit_cleans_up_files_when_database_save_fails()
    {
        var storage = new FakeStorage();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => UseCase(new FakeRepository { FailOnSave = true }, storage).ExecuteAsync(Valid(), ValidDocs()));
        Assert.Empty(storage.Files);
    }

    [Fact] // "Chờ Admin xem xong rồi duyệt": hồ sơ mới nộp chưa cấp tài khoản được; phải bắt đầu thẩm định trước.
    public void New_application_cannot_be_approved_before_admin_starts_review()
    {
        var app = PartnerApplication.Submit("PA-2026-0013", Now);

        Assert.Throws<ConflictException>(app.EnsureCanIssueAccount);
        app.StartReview(adminUserId: 1);
        app.EnsureCanIssueAccount();
        Assert.Equal(PartnerApplicationStatus.UnderReview, app.Status);
    }

    // ===== Admin xem giấy tờ =====

    private sealed class FakeDocumentQueries(params PartnerDocumentRecord[] rows) : IPartnerDocumentQueries
    {
        public Task<bool> ApplicationExistsAsync(int applicationId, CancellationToken cancellationToken)
            => Task.FromResult(applicationId == 7);

        public Task<IReadOnlyList<PartnerDocumentRecord>> ListAsync(int applicationId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PartnerDocumentRecord>>(rows.Where(r => r.PartnerApplicationId == applicationId).ToList());

        public Task<PartnerDocumentRecord?> FindAsync(int applicationId, int documentId, CancellationToken cancellationToken)
            => Task.FromResult(rows.FirstOrDefault(r => r.PartnerApplicationId == applicationId && r.Id == documentId));
    }

    private static readonly PartnerDocumentRecord LicenseRow =
        new(3, 7, PartnerDocumentType.BusinessLicense, "partner-applications/abc/business-license-1.pdf", "application/pdf", "dkkd.pdf", 12, Now);

    [Fact]
    public async Task Admin_lists_and_opens_documents()
    {
        var storage = new FakeStorage();
        storage.Files[LicenseRow.FileKey] = Pdf;
        var queries = new FakeDocumentQueries(LicenseRow);

        var list = await new ListPartnerDocumentsUseCase(queries).ExecuteAsync(7);
        var file = await new GetPartnerDocumentFileUseCase(queries, storage).ExecuteAsync(7, 3);

        Assert.Equal("BusinessLicense", Assert.Single(list).DocumentType);
        Assert.Equal(Pdf, file.Content);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("dkkd.pdf", file.FileName);
    }

    [Fact] // Giấy tờ của hồ sơ khác / hồ sơ không tồn tại / file mất trong kho → 404.
    public async Task Admin_document_lookups_are_scoped_to_the_application()
    {
        var queries = new FakeDocumentQueries(LicenseRow);
        var storage = new FakeStorage();

        await Assert.ThrowsAsync<NotFoundException>(() => new ListPartnerDocumentsUseCase(queries).ExecuteAsync(8));
        await Assert.ThrowsAsync<NotFoundException>(() => new GetPartnerDocumentFileUseCase(queries, storage).ExecuteAsync(8, 3));
        await Assert.ThrowsAsync<NotFoundException>(() => new GetPartnerDocumentFileUseCase(queries, storage).ExecuteAsync(7, 3));
    }
}
