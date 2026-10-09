using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.Kyb;
using ParkingService.Domain.Entities;

namespace ParkingService.Test;

public class KybApplicationTests
{
    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);
        return entity;
    }

    private static ParkingLot CreateLot(int id, int ownerProfileId, int totalSlots, double lat = 10.7719, double lng = 106.7044)
    {
        return new ParkingLot(
            id: id,
            name: $"Bãi xe #{id}",
            address: "123 Đường Test, Q.1",
            latitude: lat,
            longitude: lng,
            totalSlots: totalSlots,
            availableSlots: 0,
            maxHeightCm: 210,
            openTime: new TimeOnly(6, 0),
            closeTime: new TimeOnly(22, 0),
            status: ParkingLotStatus.PendingApproval,
            ownerProfileId: ownerProfileId);
    }

    // ==================== GET / INIT DRAFT TESTS ====================

    [Fact]
    public async Task GetKyb_WhenNoExisting_SmallLot_InitializesDraftWithSurveyNotRequired()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30)); // <= 50 chỗ

        var kybRepo = new FakeKybRepo();
        var useCase = new GetKybApplicationUseCase(kybRepo, lotRepo, NullLogger<GetKybApplicationUseCase>.Instance);

        var result = await useCase.ExecuteAsync(new GetKybApplicationQuery(ParkingLotId: 1, OwnerProfileId: 10));

        Assert.NotNull(result);
        Assert.Equal("Draft", result.Status);
        Assert.False(result.FieldSurveyRequired); // Không bắt buộc khảo sát thực địa
    }

    [Fact]
    public async Task GetKyb_WhenNoExisting_BigLot_InitializesDraftWithSurveyRequired()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(2, ownerProfileId: 10, totalSlots: 80)); // > 50 chỗ

        var kybRepo = new FakeKybRepo();
        var useCase = new GetKybApplicationUseCase(kybRepo, lotRepo, NullLogger<GetKybApplicationUseCase>.Instance);

        var result = await useCase.ExecuteAsync(new GetKybApplicationQuery(ParkingLotId: 2, OwnerProfileId: 10));

        Assert.NotNull(result);
        Assert.Equal("Draft", result.Status);
        Assert.True(result.FieldSurveyRequired); // Tự động bắt buộc khảo sát thực địa
    }

    [Fact]
    public async Task GetKyb_WhenNotOwner_ThrowsConflictException()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));

        var kybRepo = new FakeKybRepo();
        var useCase = new GetKybApplicationUseCase(kybRepo, lotRepo, NullLogger<GetKybApplicationUseCase>.Instance);

        // Chủ bãi 99 truy cập bãi của chủ 10
        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new GetKybApplicationQuery(ParkingLotId: 1, OwnerProfileId: 99)));
    }

    // ==================== SAVE DRAFT TESTS ====================

    [Fact]
    public async Task SaveKybDraft_WithPartialData_Succeeds()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));

        var kybRepo = new FakeKybRepo();
        var useCase = new SaveKybDraftUseCase(kybRepo, lotRepo, NullLogger<SaveKybDraftUseCase>.Instance);

        // Chỉ lưu Bước 1 (ĐKKD) và ghi chú Bước 4
        var command = new SaveKybDraftCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/uploads/giay-phep-kd.pdf",
            FieldSurveyNote: "Vui lòng gọi hotline trước khi đến");

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal("Draft", result.Status);
        Assert.Equal("/uploads/giay-phep-kd.pdf", result.BusinessLicenseUrl);
        Assert.Equal("Vui lòng gọi hotline trước khi đến", result.FieldSurveyNote);
        Assert.Empty(result.SitePhotoUrls);
        Assert.Null(result.FireSafetyCertificateUrl);
    }

    [Theory]
    [InlineData(KybStatus.Submitted)]
    [InlineData(KybStatus.UnderReview)]
    [InlineData(KybStatus.Approved)]
    public async Task SaveKybDraft_WhenLockedStatus_ThrowsConflictException(KybStatus lockedStatus)
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));

        var kybRepo = new FakeKybRepo();
        await kybRepo.AddAsync(WithId(new KybApplication { ParkingLotId = 1, Status = lockedStatus }, 1));

        var useCase = new SaveKybDraftUseCase(kybRepo, lotRepo, NullLogger<SaveKybDraftUseCase>.Instance);

        var command = new SaveKybDraftCommand(ParkingLotId: 1, OwnerProfileId: 10, BusinessLicenseUrl: "test.pdf");
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    // ==================== SUBMIT VALIDATION TESTS ====================

    [Fact]
    public async Task SubmitKyb_MissingBusinessLicense_ThrowsValidationException()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));

        var kybRepo = new FakeKybRepo();
        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "   ", // Rỗng
            SitePhotoUrls: ["/photo1.jpg"],
            PhotoLatitude: null,
            PhotoLongitude: null,
            FireSafetyCertificateUrl: "/pccc.pdf",
            FieldSurveyAtUtc: null,
            FieldSurveyNote: null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Bước 1", ex.Message);
    }

    [Fact]
    public async Task SubmitKyb_MissingSitePhotos_ThrowsValidationException()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));

        var kybRepo = new FakeKybRepo();
        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/gpdkkd.pdf",
            SitePhotoUrls: [], // Không có ảnh
            PhotoLatitude: null,
            PhotoLongitude: null,
            FireSafetyCertificateUrl: "/pccc.pdf",
            FieldSurveyAtUtc: null,
            FieldSurveyNote: null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Bước 2", ex.Message);
    }

    [Fact]
    public async Task SubmitKyb_PhotoGpsTooFarFromLot_ThrowsValidationException()
    {
        var lotRepo = new FakeLotManagementRepo();
        // Bãi ở Quận 1 (10.7719, 106.7044)
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30, lat: 10.7719, lng: 106.7044));

        var kybRepo = new FakeKybRepo();
        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        // Ảnh chụp cách xa ~10km (10.8500, 106.7800)
        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/gpdkkd.pdf",
            SitePhotoUrls: ["/photo1.jpg"],
            PhotoLatitude: 10.8500,
            PhotoLongitude: 106.7800,
            FireSafetyCertificateUrl: "/pccc.pdf",
            FieldSurveyAtUtc: null,
            FieldSurveyNote: null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("500 m", ex.Message);
    }

    [Fact]
    public async Task SubmitKyb_MissingFireSafetyCertificate_ThrowsValidationException()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));

        var kybRepo = new FakeKybRepo();
        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/gpdkkd.pdf",
            SitePhotoUrls: ["/photo1.jpg"],
            PhotoLatitude: null,
            PhotoLongitude: null,
            FireSafetyCertificateUrl: "", // Thiếu PCCC
            FieldSurveyAtUtc: null,
            FieldSurveyNote: null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Bước 3", ex.Message);
    }

    [Fact]
    public async Task SubmitKyb_BigLotWithoutSurveyDate_ThrowsValidationException()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 100)); // Bãi lớn > 50 chỗ

        var kybRepo = new FakeKybRepo();
        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/gpdkkd.pdf",
            SitePhotoUrls: ["/photo1.jpg"],
            PhotoLatitude: null,
            PhotoLongitude: null,
            FireSafetyCertificateUrl: "/pccc.pdf",
            FieldSurveyAtUtc: null, // Thiếu lịch hẹn khảo sát
            FieldSurveyNote: null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Bước 4", ex.Message);
    }

    [Fact]
    public async Task SubmitKyb_SmallLotWithoutSurveyDate_Succeeds()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 35)); // Bãi <= 50 chỗ

        var kybRepo = new FakeKybRepo();
        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/gpdkkd.pdf",
            SitePhotoUrls: ["/photo1.jpg", "/photo2.jpg"],
            PhotoLatitude: null,
            PhotoLongitude: null,
            FireSafetyCertificateUrl: "/pccc.pdf",
            FieldSurveyAtUtc: null, // Bãi nhỏ không bắt buộc
            FieldSurveyNote: "Gần ngã tư");

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal("Submitted", result.Status);
        Assert.False(result.FieldSurveyRequired);
        Assert.NotNull(result.SubmittedAtUtc);
        Assert.Equal(2, result.SitePhotoUrls.Count);
    }

    [Fact]
    public async Task SubmitKyb_BigLotWithValidSurveyDate_Succeeds()
    {
        var lotRepo = new FakeLotManagementRepo();
        // Bãi Bitexco (10.7719, 106.7044)
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 120, lat: 10.7719, lng: 106.7044));

        var kybRepo = new FakeKybRepo();
        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        var futureDate = DateTime.UtcNow.AddDays(3);
        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/gpdkkd.pdf",
            SitePhotoUrls: ["/photo1.jpg"],
            PhotoLatitude: 10.7720, // Sai lệch ~15m (hợp lệ <= 500m)
            PhotoLongitude: 106.7045,
            FireSafetyCertificateUrl: "/pccc.pdf",
            FieldSurveyAtUtc: futureDate,
            FieldSurveyNote: "Gặp ban quản lý tại cổng A");

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal("Submitted", result.Status);
        Assert.True(result.FieldSurveyRequired);
        Assert.Equal(futureDate, result.FieldSurveyAtUtc);
        Assert.NotNull(result.SubmittedAtUtc);
    }

    // ==================== FAKE REPOSITORIES ====================

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

    private sealed class FakeKybRepo : IKybApplicationRepository
    {
        private readonly List<KybApplication> _apps = [];
        private int _nextId = 1;

        public Task<KybApplication?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_apps.FirstOrDefault(a => a.Id == id));

        public Task<KybApplication?> GetLatestByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(_apps.Where(a => a.ParkingLotId == parkingLotId).OrderByDescending(a => a.Id).FirstOrDefault());

        public Task<IReadOnlyList<KybApplication>> GetHistoryByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<KybApplication>>(_apps.Where(a => a.ParkingLotId == parkingLotId).OrderByDescending(a => a.Id).ToList());

        public Task AddAsync(KybApplication application, CancellationToken cancellationToken = default)
        {
            if (application.Id == 0) WithId(application, _nextId++);
            _apps.Add(application);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(KybApplication application, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
