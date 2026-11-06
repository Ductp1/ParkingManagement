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
        Assert.Equal(0, kybRepo.Count); // GET không ghi CSDL, tránh sinh bản ghi duplicate
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

    [Fact]
    public async Task GetKyb_WhenNeedsMoreInfo_ReturnsAdminReviewNote()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));
        var kybRepo = new FakeKybRepo();
        var app = new KybApplication { ParkingLotId = 1, Status = KybStatus.UnderReview };
        app.RequestMoreInfo(adminUserId: 1, "  Bổ sung chứng nhận PCCC còn hiệu lực  ", new DateTime(2026, 10, 30, 1, 0, 0, DateTimeKind.Utc));
        await kybRepo.AddAsync(app);
        var useCase = new GetKybApplicationUseCase(kybRepo, lotRepo, NullLogger<GetKybApplicationUseCase>.Instance);

        var result = await useCase.ExecuteAsync(new GetKybApplicationQuery(ParkingLotId: 1, OwnerProfileId: 10));

        Assert.Equal("NeedsMoreInfo", result.Status);
        Assert.Equal("Bổ sung chứng nhận PCCC còn hiệu lực", result.ReviewNote);
        Assert.Null(result.RejectReason); // field cũ giữ nguyên ý nghĩa: chỉ là lý do từ chối
    }

    [Fact]
    public async Task GetKyb_WhenNoExisting_ReviewNoteIsNull()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));
        var useCase = new GetKybApplicationUseCase(new FakeKybRepo(), lotRepo, NullLogger<GetKybApplicationUseCase>.Instance);

        var result = await useCase.ExecuteAsync(new GetKybApplicationQuery(ParkingLotId: 1, OwnerProfileId: 10));

        Assert.Null(result.ReviewNote);
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

    // ==================== LOCKED STATUS, UPDATE-PATH & RESUBMIT TESTS ====================

    [Theory]
    [InlineData(KybStatus.Submitted)]
    [InlineData(KybStatus.UnderReview)]
    [InlineData(KybStatus.Approved)]
    public async Task SubmitKyb_WhenLockedStatus_ThrowsConflictException(KybStatus lockedStatus)
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));

        var kybRepo = new FakeKybRepo();
        await kybRepo.AddAsync(WithId(new KybApplication { ParkingLotId = 1, Status = lockedStatus }, 1));

        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/gpdkkd.pdf",
            SitePhotoUrls: ["/photo1.jpg"],
            PhotoLatitude: null,
            PhotoLongitude: null,
            FireSafetyCertificateUrl: "/pccc.pdf",
            FieldSurveyAtUtc: null,
            FieldSurveyNote: null);

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task SubmitKyb_WhenDraftExists_UpdatesExistingRecord_NoDuplicateCreated()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));

        var kybRepo = new FakeKybRepo();
        // Giả lập bãi đã có sẵn 1 bản ghi Draft từ trước (Id = 1)
        var existingDraft = WithId(new KybApplication
        {
            ParkingLotId = 1,
            Status = KybStatus.Draft,
            BusinessLicenseUrl = "/old-license.pdf",
            FieldSurveyRequired = false
        }, 1);
        await kybRepo.AddAsync(existingDraft);

        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/new-license.pdf",
            SitePhotoUrls: ["/photo1.jpg"],
            PhotoLatitude: null,
            PhotoLongitude: null,
            FireSafetyCertificateUrl: "/pccc.pdf",
            FieldSurveyAtUtc: null,
            FieldSurveyNote: null);

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal(1, result.Id); // Cập nhật đúng bản ghi Id = 1 hiện có
        Assert.Equal("Submitted", result.Status);
        Assert.Equal("/new-license.pdf", result.BusinessLicenseUrl);
        Assert.Equal(1, kybRepo.Count); // Bắt buộc chỉ có 1 bản ghi duy nhất, KHÔNG sinh duplicate!
    }

    [Fact]
    public async Task SaveKybDraft_WhenDraftExists_UpdatesExistingRecord_NoDuplicateCreated()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));

        var kybRepo = new FakeKybRepo();
        var existingDraft = WithId(new KybApplication
        {
            ParkingLotId = 1,
            Status = KybStatus.Draft,
            BusinessLicenseUrl = "/step1.pdf",
            FieldSurveyRequired = false
        }, 1);
        await kybRepo.AddAsync(existingDraft);

        var useCase = new SaveKybDraftUseCase(kybRepo, lotRepo, NullLogger<SaveKybDraftUseCase>.Instance);

        // Lưu tiếp Bước 3 (PCCC)
        var command = new SaveKybDraftCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            FireSafetyCertificateUrl: "/step3-pccc.pdf");

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal(1, result.Id);
        Assert.Equal("Draft", result.Status);
        Assert.Equal("/step1.pdf", result.BusinessLicenseUrl); // Giữ nguyên dữ liệu cũ
        Assert.Equal("/step3-pccc.pdf", result.FireSafetyCertificateUrl); // Cập nhật trường mới
        Assert.Equal(1, kybRepo.Count); // Đảm bảo không tạo thêm bản ghi thứ 2
    }

    [Fact]
    public async Task SubmitKyb_WhenRejected_Resubmit_UpdatesExistingRecord_ClearsRejectReason()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));

        var kybRepo = new FakeKybRepo();
        var rejectedApp = WithId(new KybApplication
        {
            ParkingLotId = 1,
            Status = KybStatus.Rejected,
            RejectReason = "Ảnh hiện trường không rõ biển hiệu",
            BusinessLicenseUrl = "/old-license.pdf",
            FireSafetyCertificateUrl = "/pccc.pdf"
        }, 1);
        await kybRepo.AddAsync(rejectedApp);

        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/fixed-license.pdf",
            SitePhotoUrls: ["/new-photo-clear.jpg"],
            PhotoLatitude: null,
            PhotoLongitude: null,
            FireSafetyCertificateUrl: "/pccc.pdf",
            FieldSurveyAtUtc: null,
            FieldSurveyNote: null);

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal(1, result.Id);
        Assert.Equal("Submitted", result.Status);
        Assert.Null(result.RejectReason); // Đã xóa lý do từ chối khi nộp lại thành công
        Assert.NotNull(result.SubmittedAtUtc);
        Assert.Equal(1, kybRepo.Count); // Giữ nguyên 1 bản ghi duy nhất
    }

    [Fact]
    public async Task SubmitKyb_WhenNeedsMoreInfo_Resubmit_UpdatesExistingRecord()
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));

        var kybRepo = new FakeKybRepo();
        var needsInfoApp = WithId(new KybApplication
        {
            ParkingLotId = 1,
            Status = KybStatus.NeedsMoreInfo,
            RejectReason = "Vui lòng bổ sung chứng nhận PCCC mới nhất",
            BusinessLicenseUrl = "/license.pdf"
        }, 1);
        await kybRepo.AddAsync(needsInfoApp);

        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/license.pdf",
            SitePhotoUrls: ["/photo1.jpg"],
            PhotoLatitude: null,
            PhotoLongitude: null,
            FireSafetyCertificateUrl: "/new-pccc.pdf",
            FieldSurveyAtUtc: null,
            FieldSurveyNote: null);

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal(1, result.Id);
        Assert.Equal("Submitted", result.Status);
        Assert.Null(result.RejectReason);
        Assert.Equal(1, kybRepo.Count);
    }

    [Theory]
    [InlineData(KybStatus.Rejected)]
    [InlineData(KybStatus.NeedsMoreInfo)]
    public async Task SaveKybDraft_WhenRejectedOrNeedsMoreInfo_ResetsStatusToDraft(KybStatus priorStatus)
    {
        var lotRepo = new FakeLotManagementRepo();
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30));

        var kybRepo = new FakeKybRepo();
        var app = WithId(new KybApplication
        {
            ParkingLotId = 1,
            Status = priorStatus,
            BusinessLicenseUrl = "/license.pdf"
        }, 1);
        await kybRepo.AddAsync(app);

        var useCase = new SaveKybDraftUseCase(kybRepo, lotRepo, NullLogger<SaveKybDraftUseCase>.Instance);

        var command = new SaveKybDraftCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/license-updated.pdf");

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal(1, result.Id);
        Assert.Equal("Draft", result.Status); // Trạng thái tự động reset về Draft khi lưu nháp sửa đổi
        Assert.Equal("/license-updated.pdf", result.BusinessLicenseUrl);
        Assert.Equal(1, kybRepo.Count);
    }

    // ==================== BOUNDARY TESTS (50/51 SLOTS & 500M GPS) ====================

    [Fact]
    public async Task Boundary_SlotCapacity_50Slots_DoesNotRequireSurvey_SubmitSucceedsWithoutDate()
    {
        var lotRepo = new FakeLotManagementRepo();
        // Biên 50 chỗ: <= 50 chỗ không bắt buộc khảo sát thực địa
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 50));

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
            FieldSurveyAtUtc: null, // Không truyền ngày khảo sát
            FieldSurveyNote: null);

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal("Submitted", result.Status);
        Assert.False(result.FieldSurveyRequired); // Đúng 50 chỗ -> FieldSurveyRequired = false
        Assert.Null(result.FieldSurveyAtUtc);
    }

    [Fact]
    public async Task Boundary_SlotCapacity_51Slots_RequiresSurvey_SubmitWithoutDateThrowsValidationException()
    {
        var lotRepo = new FakeLotManagementRepo();
        // Biên 51 chỗ: > 50 chỗ BẮT BUỘC phải có lịch hẹn khảo sát thực địa
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 51));

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
            FieldSurveyAtUtc: null, // Thiếu ngày hẹn khảo sát
            FieldSurveyNote: null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Bước 4: Bãi đỗ có quy mô trên 50 chỗ bắt buộc phải đặt lịch khảo sát thực địa", ex.Message);
    }

    [Fact]
    public async Task Boundary_PhotoGpsDistance_AtOrUnder500m_Succeeds()
    {
        var lotRepo = new FakeLotManagementRepo();
        // Bãi tại tọa độ (10.7719, 106.7044)
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30, lat: 10.7719, lng: 106.7044));

        var kybRepo = new FakeKybRepo();
        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        // Độ lệch vĩ độ dLat = 0.0044 -> Khoảng cách Haversine ~ 0.489 km (<= 0.5 km / 500m)
        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/gpdkkd.pdf",
            SitePhotoUrls: ["/photo1.jpg"],
            PhotoLatitude: 10.7719 + 0.0044,
            PhotoLongitude: 106.7044,
            FireSafetyCertificateUrl: "/pccc.pdf",
            FieldSurveyAtUtc: null,
            FieldSurveyNote: null);

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal("Submitted", result.Status);
        Assert.Equal(10.7719 + 0.0044, result.PhotoLatitude);
    }

    [Fact]
    public async Task Boundary_PhotoGpsDistance_Over500m_ThrowsValidationException()
    {
        var lotRepo = new FakeLotManagementRepo();
        // Bãi tại tọa độ (10.7719, 106.7044)
        await lotRepo.AddAsync(CreateLot(1, ownerProfileId: 10, totalSlots: 30, lat: 10.7719, lng: 106.7044));

        var kybRepo = new FakeKybRepo();
        var useCase = new SubmitKybApplicationUseCase(kybRepo, lotRepo, NullLogger<SubmitKybApplicationUseCase>.Instance);

        // Độ lệch vĩ độ dLat = 0.0046 -> Khoảng cách Haversine ~ 0.511 km (> 0.5 km / 500m)
        var command = new SubmitKybApplicationCommand(
            ParkingLotId: 1,
            OwnerProfileId: 10,
            BusinessLicenseUrl: "/gpdkkd.pdf",
            SitePhotoUrls: ["/photo1.jpg"],
            PhotoLatitude: 10.7719 + 0.0046,
            PhotoLongitude: 106.7044,
            FireSafetyCertificateUrl: "/pccc.pdf",
            FieldSurveyAtUtc: null,
            FieldSurveyNote: null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("500 m", ex.Message);
        Assert.Contains("vượt quá bán kính cho phép", ex.Message);
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

        public int Count => _apps.Count;
        public IReadOnlyList<KybApplication> All => _apps;

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
