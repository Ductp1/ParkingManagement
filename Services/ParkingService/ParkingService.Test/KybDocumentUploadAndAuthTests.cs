using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.API.Controllers;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.Kyb;
using ParkingService.Domain.Entities;
using ParkingService.Infrastructure.Storage;

namespace ParkingService.Test;

public class KybDocumentUploadAndAuthTests : IDisposable
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), "pm-kyb-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); }
            catch { /* Ignore cleanup issues in temp */ }
        }
    }

    private static ParkingLot CreateLot(int id, int ownerProfileId)
    {
        return new ParkingLot(
            id: id,
            name: $"Bãi xe #{id}",
            address: "123 Đường Test",
            latitude: 10.7719,
            longitude: 106.7044,
            totalSlots: 30,
            availableSlots: 0,
            maxHeightCm: 210,
            openTime: new TimeOnly(6, 0),
            closeTime: new TimeOnly(22, 0),
            status: ParkingLotStatus.PendingApproval,
            ownerProfileId: ownerProfileId);
    }

    private static byte[] CreateValidPdfBytes()
    {
        var header = "%PDF-1.4\n"u8.ToArray();
        var body = Encoding.UTF8.GetBytes("Test PDF content for KYB");
        var result = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(body, 0, result, header.Length, body.Length);
        return result;
    }

    private static byte[] CreateValidPngBytes()
    {
        var header = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var body = new byte[] { 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
        var result = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(body, 0, result, header.Length, body.Length);
        return result;
    }

    private static byte[] CreateValidJpegBytes()
    {
        var header = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        return header;
    }

    private static byte[] CreateValidWebpBytes()
    {
        var bytes = new byte[16];
        // "RIFF"
        bytes[0] = 0x52; bytes[1] = 0x49; bytes[2] = 0x46; bytes[3] = 0x46;
        // length placeholder
        bytes[4] = 0x08; bytes[5] = 0x00; bytes[6] = 0x00; bytes[7] = 0x00;
        // "WEBP"
        bytes[8] = 0x57; bytes[9] = 0x45; bytes[10] = 0x42; bytes[11] = 0x50;
        return bytes;
    }

    // ==================== USE CASE UPLOAD TESTS ====================

    [Fact]
    public async Task UploadKybDocument_ValidPdfLicense_SucceedsAndReturnsResult()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10));

        var storage = new FakeKybDocumentStorage();
        var useCase = new UploadKybDocumentUseCase(lotRepo, storage);

        var pdfBytes = CreateValidPdfBytes();
        using var stream = new MemoryStream(pdfBytes);

        var result = await useCase.ExecuteAsync(
            lotId: 1,
            documentType: KybDocumentType.BusinessLicense,
            originalFileName: "giay_phep_kinh_doanh.pdf",
            contentType: "application/pdf",
            length: pdfBytes.Length,
            stream: stream,
            ownerProfileId: 10);

        Assert.NotNull(result);
        Assert.Equal("giay_phep_kinh_doanh.pdf", result.OriginalFileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Contains("/api/v1/parking-lots/1/kyb/documents/", result.DocumentUrl);
    }

    [Fact]
    public async Task UploadKybDocument_ValidPngSitePhoto_Succeeds()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10));

        var storage = new FakeKybDocumentStorage();
        var useCase = new UploadKybDocumentUseCase(lotRepo, storage);

        var pngBytes = CreateValidPngBytes();
        using var stream = new MemoryStream(pngBytes);

        var result = await useCase.ExecuteAsync(
            lotId: 1,
            documentType: KybDocumentType.SitePhoto,
            originalFileName: "hien_truong.png",
            contentType: "image/png",
            length: pngBytes.Length,
            stream: stream,
            ownerProfileId: 10);

        Assert.Equal("hien_truong.png", result.OriginalFileName);
        Assert.Equal("image/png", result.ContentType);
    }

    [Fact]
    public async Task UploadKybDocument_ValidWebpSitePhoto_Succeeds()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10));

        var storage = new FakeKybDocumentStorage();
        var useCase = new UploadKybDocumentUseCase(lotRepo, storage);

        var webpBytes = CreateValidWebpBytes();
        using var stream = new MemoryStream(webpBytes);

        var result = await useCase.ExecuteAsync(
            lotId: 1,
            documentType: KybDocumentType.SitePhoto,
            originalFileName: "cong_vao.webp",
            contentType: "image/webp",
            length: webpBytes.Length,
            stream: stream,
            ownerProfileId: 10);

        Assert.Equal("image/webp", result.ContentType);
    }

    [Fact]
    public async Task UploadKybDocument_WhenLotNotFound_ThrowsNotFoundException()
    {
        var lotRepo = new FakeLotManagementRepo();
        var storage = new FakeKybDocumentStorage();
        var useCase = new UploadKybDocumentUseCase(lotRepo, storage);

        using var stream = new MemoryStream(CreateValidPdfBytes());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(999, KybDocumentType.BusinessLicense, "file.pdf", "application/pdf", stream.Length, stream, 10));
    }

    [Fact]
    public async Task UploadKybDocument_WhenNotOwnerAndNotAdmin_ThrowsConflictException()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10)); // Owner là 10

        var storage = new FakeKybDocumentStorage();
        var useCase = new UploadKybDocumentUseCase(lotRepo, storage);

        using var stream = new MemoryStream(CreateValidPdfBytes());

        // Gọi với ownerProfileId = 99
        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(1, KybDocumentType.BusinessLicense, "file.pdf", "application/pdf", stream.Length, stream, ownerProfileId: 99, isAdmin: false));
    }

    [Fact]
    public async Task UploadKybDocument_WhenAdmin_SucceedsEvenIfNotOwner()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10));

        var storage = new FakeKybDocumentStorage();
        var useCase = new UploadKybDocumentUseCase(lotRepo, storage);

        var pdfBytes = CreateValidPdfBytes();
        using var stream = new MemoryStream(pdfBytes);

        var result = await useCase.ExecuteAsync(
            lotId: 1,
            documentType: KybDocumentType.BusinessLicense,
            originalFileName: "admin_upload.pdf",
            contentType: "application/pdf",
            length: pdfBytes.Length,
            stream: stream,
            ownerProfileId: null,
            isAdmin: true);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UploadKybDocument_WhenDocumentExceeds10Mb_ThrowsValidationException()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10));

        var storage = new FakeKybDocumentStorage();
        var useCase = new UploadKybDocumentUseCase(lotRepo, storage);

        using var stream = new MemoryStream(new byte[1]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(1, KybDocumentType.BusinessLicense, "big.pdf", "application/pdf", UploadKybDocumentUseCase.MaxDocumentBytes + 1, stream, 10));
    }

    [Fact]
    public async Task UploadKybDocument_WhenFakeExtensionWithInvalidMagicBytes_ThrowsValidationException()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10));

        var storage = new FakeKybDocumentStorage();
        var useCase = new UploadKybDocumentUseCase(lotRepo, storage);

        // File text thông thường nhưng đổi đuôi thành .pdf
        var fakeBytes = Encoding.UTF8.GetBytes("This is not a real PDF file.");
        using var stream = new MemoryStream(fakeBytes);

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(1, KybDocumentType.BusinessLicense, "fake.pdf", "application/pdf", fakeBytes.Length, stream, 10));
    }

    [Fact]
    public async Task UploadKybDocument_WhenInvalidExtensionForType_ThrowsValidationException()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10));

        var storage = new FakeKybDocumentStorage();
        var useCase = new UploadKybDocumentUseCase(lotRepo, storage);

        // Nộp file PDF cho SitePhoto (chỉ nhận ảnh)
        var pdfBytes = CreateValidPdfBytes();
        using var stream = new MemoryStream(pdfBytes);

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(1, KybDocumentType.SitePhoto, "test.pdf", "application/pdf", pdfBytes.Length, stream, 10));
    }

    // ==================== USE CASE GET DOCUMENT TESTS ====================

    [Fact]
    public async Task GetKybDocumentFile_WhenValidAndOwner_ReturnsContent()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10));

        var storage = new FakeKybDocumentStorage();
        storage.Files["license_abc.pdf"] = new KybDocumentFileContent(CreateValidPdfBytes(), "application/pdf", "license_abc.pdf");

        var useCase = new GetKybDocumentFileUseCase(lotRepo, storage);

        var file = await useCase.ExecuteAsync(lotId: 1, fileId: "license_abc.pdf", ownerProfileId: 10);

        Assert.NotNull(file);
        Assert.Equal("application/pdf", file.ContentType);
    }

    [Fact]
    public async Task GetKybDocumentFile_WhenAdmin_ReturnsContentEvenIfNotOwner()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10));

        var storage = new FakeKybDocumentStorage();
        storage.Files["license_abc.pdf"] = new KybDocumentFileContent(CreateValidPdfBytes(), "application/pdf", "license_abc.pdf");

        var useCase = new GetKybDocumentFileUseCase(lotRepo, storage);

        var file = await useCase.ExecuteAsync(lotId: 1, fileId: "license_abc.pdf", ownerProfileId: null, isAdmin: true);

        Assert.NotNull(file);
    }

    [Fact]
    public async Task GetKybDocumentFile_WhenNotOwnerAndNotAdmin_ThrowsConflictException()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10));

        var storage = new FakeKybDocumentStorage();
        storage.Files["license_abc.pdf"] = new KybDocumentFileContent(CreateValidPdfBytes(), "application/pdf", "license_abc.pdf");

        var useCase = new GetKybDocumentFileUseCase(lotRepo, storage);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(lotId: 1, fileId: "license_abc.pdf", ownerProfileId: 99, isAdmin: false));
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("..\\windows\\win.ini")]
    [InlineData("/etc/passwd")]
    public async Task GetKybDocumentFile_WhenPathTraversalAttempt_ThrowsValidationException(string evilFileId)
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10));

        var storage = new FakeKybDocumentStorage();
        var useCase = new GetKybDocumentFileUseCase(lotRepo, storage);

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(lotId: 1, fileId: evilFileId, ownerProfileId: 10));
    }

    // ==================== LOCAL STORAGE TESTS ====================

    [Fact]
    public async Task LocalKybDocumentStorage_SaveAndRead_SucceedsWithCorrectContentType()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:KybDocumentPath"] = _testDir
        }).Build();

        var hostEnv = new FakeHostEnvironment { ContentRootPath = _testDir };
        var storage = new LocalKybDocumentStorage(config, hostEnv, NullLogger<LocalKybDocumentStorage>.Instance);

        var pdfBytes = CreateValidPdfBytes();
        using var stream = new MemoryStream(pdfBytes);

        var url = await storage.SaveAsync(lotId: 1, KybDocumentType.BusinessLicense, "my_license.pdf", ".pdf", stream);
        Assert.StartsWith("/api/v1/parking-lots/1/kyb/documents/", url);

        var fileId = Path.GetFileName(url);
        var fileContent = await storage.ReadAsync(lotId: 1, fileId);

        Assert.NotNull(fileContent);
        Assert.Equal("application/pdf", fileContent.ContentType);
        Assert.Equal(pdfBytes.Length, fileContent.Bytes.Length);

        // Test delete
        await storage.DeleteAsync(lotId: 1, fileId);
        var afterDelete = await storage.ReadAsync(lotId: 1, fileId);
        Assert.Null(afterDelete);
    }

    // ==================== CONTROLLER AUTHORIZATION TESTS ====================

    [Fact]
    public void KybApplicationsController_HasAuthorizeAttribute()
    {
        var type = typeof(KybApplicationsController);
        var authorizeAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorizeAttr);
    }

    [Fact]
    public void KybApplicationsController_Submit_HasAllowAnonymousAttribute()
    {
        var type = typeof(KybApplicationsController);
        var submitMethod = type.GetMethod(nameof(KybApplicationsController.Submit));
        Assert.NotNull(submitMethod);
        var allowAnon = submitMethod.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(allowAnon);
    }

    // ==================== FAKE REPOSITORIES & STORAGE ====================

    private sealed class FakeLotManagementRepo : IParkingLotManagementRepository
    {
        private readonly List<ParkingLot> _lots = [];

        public Task<ParkingLot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_lots.FirstOrDefault(l => l.Id == id));

        public Task<ParkingLot?> GetHierarchyAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_lots.FirstOrDefault(l => l.Id == id));

        public Task<IReadOnlyList<ParkingLot>> GetByOwnerProfileIdAsync(int ownerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ParkingLot>>(_lots.Where(l => l.OwnerProfileId == ownerProfileId).ToList());

        public Task<bool> ExistsByNameAsync(int ownerProfileId, string name, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_lots.Any(l => l.OwnerProfileId == ownerProfileId && l.Name == name && (!excludeId.HasValue || l.Id != excludeId.Value)));

        public Task AddAsync(ParkingLot lot, CancellationToken cancellationToken = default)
        {
            _lots.Add(lot);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ParkingLot lot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeKybDocumentStorage : IKybDocumentStorage
    {
        public Dictionary<string, KybDocumentFileContent> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<string> SaveAsync(int lotId, KybDocumentType type, string originalFileName, string fileExtension, Stream input, CancellationToken ct = default)
        {
            var fileId = $"test_{Guid.NewGuid():N}{fileExtension}";
            using var ms = new MemoryStream();
            input.CopyTo(ms);
            var mime = fileExtension == ".pdf" ? "application/pdf" : "image/png";
            Files[fileId] = new KybDocumentFileContent(ms.ToArray(), mime, fileId);
            return Task.FromResult($"/api/v1/parking-lots/{lotId}/kyb/documents/{fileId}");
        }

        public Task<KybDocumentFileContent?> ReadAsync(int lotId, string fileId, CancellationToken ct = default)
        {
            Files.TryGetValue(fileId, out var content);
            return Task.FromResult(content);
        }

        public Task DeleteAsync(int lotId, string fileId, CancellationToken ct = default)
        {
            Files.Remove(fileId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ParkingService.Test";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
