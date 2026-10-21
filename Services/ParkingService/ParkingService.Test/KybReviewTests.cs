using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.API.Controllers;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.KybReview;
using ParkingService.Domain.Entities;

namespace ParkingService.Test;

/// <summary>US-055: Admin duyệt / từ chối / yêu cầu bổ sung hồ sơ KYB, khảo sát thực địa, kích hoạt bãi, audit + outbox.</summary>
public class KybReviewTests
{
    private const int AdminId = 1;
    private static readonly DateTime Now = new(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc);

    // ==================== Helpers ====================

    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!.SetValue(entity, id);
        return entity;
    }

    private static ParkingLot Lot(int totalSlots = 30, ParkingLotStatus status = ParkingLotStatus.PendingApproval) => new(
        id: 7, name: "Bãi xe Thủ Đức", address: "12 Võ Văn Ngân", latitude: 10.85, longitude: 106.77, totalSlots: totalSlots,
        availableSlots: 0, maxHeightCm: 210, openTime: new TimeOnly(6, 0), closeTime: new TimeOnly(22, 0), status: status,
        ownerProfileId: 57);

    private static KybApplication Kyb(KybStatus status, ParkingLot? lot = null)
    {
        lot ??= Lot();
        return WithId(new KybApplication
        {
            ParkingLotId = lot.Id, ParkingLot = lot, Status = status,
            BusinessLicenseUrl = "/uploads/kyb/gpkd.pdf", FireSafetyCertificateUrl = "/uploads/kyb/pccc.pdf",
            SitePhotoUrlsJson = "[\"/uploads/kyb/1.jpg\"]", PhotoLatitude = 10.85, PhotoLongitude = 106.77,
            SubmittedAtUtc = Now.AddDays(-1),
        }, 31);
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class FakeReviewRepo(KybApplication? app, int activeSlots = 10) : IKybReviewRepository
    {
        public List<IntegrationEvent> Saved { get; } = [];
        public int SaveCount { get; private set; }
        public bool ThrowConcurrency { get; init; }

        public Task<KybApplication?> GetForReviewAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(app?.Id == id ? app : null);

        public Task<int> CountActiveSlotsAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(activeSlots);

        public Task SaveWithEventsAsync(IReadOnlyList<IntegrationEvent> events, CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrency) throw new ConflictException("Hồ sơ KYB vừa được Admin khác cập nhật, vui lòng tải lại và thử lại.");
            Saved.AddRange(events);
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>Cổng đọc giả: dựng dòng chi tiết từ entity hiện tại (phản ánh trạng thái sau khi lưu).</summary>
    private sealed class FakeQueries(KybApplication? app, int activeSlots = 10) : IAdminKybQueries
    {
        public List<AdminKybListRow> Rows { get; } = [];
        public (KybStatus? Status, string? Search, int Page, int PageSize)? LastList { get; private set; }

        public Task<PagedResult<AdminKybListRow>> ListAsync(KybStatus? status, string? search, int page, int pageSize,
            CancellationToken cancellationToken = default)
        {
            LastList = (status, search, page, pageSize);
            return Task.FromResult(new PagedResult<AdminKybListRow>(Rows, page, pageSize, Rows.Count));
        }

        public Task<AdminKybDetailRow?> GetDetailAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(app?.Id == id ? Row(app, activeSlots) : null);
    }

    private static AdminKybDetailRow Row(KybApplication k, int activeSlots) => new(
        k.Id, k.Status, k.BusinessLicenseUrl, k.SitePhotoUrlsJson, k.PhotoLatitude, k.PhotoLongitude, k.FireSafetyCertificateUrl,
        k.FieldSurveyRequired, k.FieldSurveyAtUtc, k.FieldSurveyCompletedAtUtc, k.FieldSurveyNote, k.CreatedAtUtc, k.SubmittedAtUtc,
        k.ReviewedByUserId, k.ReviewedAtUtc, k.RejectReason, k.ReviewNote, k.ParkingLotId, k.ParkingLot.Name, k.ParkingLot.Address,
        k.ParkingLot.City, k.ParkingLot.District, k.ParkingLot.Latitude, k.ParkingLot.Longitude, k.ParkingLot.TotalSlots,
        k.ParkingLot.MaxHeightCm, k.ParkingLot.OpenTime, k.ParkingLot.CloseTime, k.ParkingLot.Status, k.ParkingLot.OwnerProfileId,
        activeSlots);

    private static AuditRecorded SingleAudit(FakeReviewRepo repo) => Assert.Single(repo.Saved.OfType<AuditRecorded>());

    // ==================== Domain: bảng chuyển trạng thái ====================

    [Theory]
    [InlineData(KybStatus.Submitted, KybStatus.UnderReview, true)]
    [InlineData(KybStatus.UnderReview, KybStatus.Approved, true)]
    [InlineData(KybStatus.UnderReview, KybStatus.Rejected, true)]
    [InlineData(KybStatus.UnderReview, KybStatus.NeedsMoreInfo, true)]
    [InlineData(KybStatus.Rejected, KybStatus.Submitted, true)]
    [InlineData(KybStatus.NeedsMoreInfo, KybStatus.Submitted, true)]
    [InlineData(KybStatus.Draft, KybStatus.UnderReview, false)]
    [InlineData(KybStatus.Submitted, KybStatus.Approved, false)]
    [InlineData(KybStatus.Approved, KybStatus.Rejected, false)]
    [InlineData(KybStatus.Rejected, KybStatus.Approved, false)]
    public void CanMove_FollowsTransitionTable(KybStatus from, KybStatus to, bool expected)
        => Assert.Equal(expected, KybApplication.CanMove(from, to));

    [Theory]
    [InlineData(KybStatus.Draft)]
    [InlineData(KybStatus.UnderReview)]
    [InlineData(KybStatus.Approved)]
    [InlineData(KybStatus.Rejected)]
    [InlineData(KybStatus.NeedsMoreInfo)]
    public void StartReview_WhenNotSubmitted_ThrowsConflict(KybStatus status)
        => Assert.Throws<ConflictException>(() => Kyb(status).StartReview(AdminId));

    [Theory]
    [InlineData(KybStatus.Submitted)]
    [InlineData(KybStatus.Approved)]
    [InlineData(KybStatus.Rejected)]
    public void Approve_Reject_RequestInfo_WhenNotUnderReview_ThrowConflict(KybStatus status)
    {
        var app = Kyb(status);
        Assert.Throws<ConflictException>(() => app.Approve(AdminId, app.ParkingLot, 10, null, Now));
        Assert.Throws<ConflictException>(() => app.Reject(AdminId, "Giấy phép hết hạn", Now));
        Assert.Throws<ConflictException>(() => app.RequestMoreInfo(AdminId, "Bổ sung ảnh", Now));
    }

    [Fact]
    public void Approve_ActivatesPendingLot_AndStampsReview()
    {
        var app = Kyb(KybStatus.UnderReview);

        app.Approve(AdminId, app.ParkingLot, activeSlotCount: 5, note: "  Đạt  ", Now);

        Assert.Equal(KybStatus.Approved, app.Status);
        Assert.Equal(ParkingLotStatus.Active, app.ParkingLot.Status);
        Assert.Equal(AdminId, app.ReviewedByUserId);
        Assert.Equal(Now, app.ReviewedAtUtc);
        Assert.Equal("Đạt", app.ReviewNote);
    }

    [Fact]
    public void Approve_Tier1LotWithoutCompletedSurvey_ThrowsConflict_AndKeepsLotPending()
    {
        var app = Kyb(KybStatus.UnderReview, Lot(totalSlots: 51));

        var ex = Assert.Throws<ConflictException>(() => app.Approve(AdminId, app.ParkingLot, 10, null, Now));

        Assert.Contains("khảo sát thực địa", ex.Message);
        Assert.Equal(KybStatus.UnderReview, app.Status);
        Assert.Equal(ParkingLotStatus.PendingApproval, app.ParkingLot.Status);
    }

    [Fact]
    public void Approve_Exactly50Slots_DoesNotRequireSurvey()
    {
        var app = Kyb(KybStatus.UnderReview, Lot(totalSlots: 50));
        app.Approve(AdminId, app.ParkingLot, 10, null, Now);
        Assert.Equal(KybStatus.Approved, app.Status);
    }

    [Fact]
    public void Approve_WithoutActiveSlots_ThrowsConflict()
        => Assert.Throws<ConflictException>(() =>
        {
            var app = Kyb(KybStatus.UnderReview);
            app.Approve(AdminId, app.ParkingLot, activeSlotCount: 0, null, Now);
        });

    [Theory]
    [InlineData(ParkingLotStatus.Active)]
    [InlineData(ParkingLotStatus.Suspended)]
    public void Approve_WhenLotNotPending_ThrowsConflict_AndKybStaysUnderReview(ParkingLotStatus lotStatus)
    {
        var app = Kyb(KybStatus.UnderReview, Lot(status: lotStatus));
        Assert.Throws<ConflictException>(() => app.Approve(AdminId, app.ParkingLot, 10, null, Now));
        Assert.Equal(KybStatus.UnderReview, app.Status);
    }

    [Fact]
    public void ParkingLotActivate_OnlyFromPendingApproval()
    {
        var lot = Lot();
        lot.Activate();
        Assert.Equal(ParkingLotStatus.Active, lot.Status);
        Assert.Throws<ConflictException>(() => lot.Activate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_And_RequestInfo_RequireText(string text)
    {
        Assert.Throws<ValidationException>(() => Kyb(KybStatus.UnderReview).Reject(AdminId, text, Now));
        Assert.Throws<ValidationException>(() => Kyb(KybStatus.UnderReview).RequestMoreInfo(AdminId, text, Now));
    }

    [Fact]
    public void Reject_TooLongReason_ThrowsValidation()
        => Assert.Throws<ValidationException>(() => Kyb(KybStatus.UnderReview).Reject(AdminId, new string('x', 1001), Now));

    [Fact]
    public void Reject_KeepsLotPending_SoOwnerCanResubmit()
    {
        var app = Kyb(KybStatus.UnderReview);
        app.Reject(AdminId, "Giấy phép hết hạn", Now);
        Assert.Equal(KybStatus.Rejected, app.Status);
        Assert.Equal("Giấy phép hết hạn", app.RejectReason);
        Assert.Equal(ParkingLotStatus.PendingApproval, app.ParkingLot.Status);
        Assert.True(KybApplication.CanMove(app.Status, KybStatus.Submitted));
    }

    [Fact]
    public void ScheduleFieldSurvey_InPast_ThrowsValidation_InFuture_ResetsCompletion()
    {
        var app = Kyb(KybStatus.UnderReview);
        Assert.Throws<ValidationException>(() => app.ScheduleFieldSurvey(Now, null, Now));

        app.FieldSurveyCompletedAtUtc = Now.AddDays(-1);
        app.ScheduleFieldSurvey(Now.AddDays(2), "  Kiểm tra barie ", Now);

        Assert.True(app.FieldSurveyRequired);
        Assert.Equal(Now.AddDays(2), app.FieldSurveyAtUtc);
        Assert.Equal("Kiểm tra barie", app.FieldSurveyNote);
        Assert.Null(app.FieldSurveyCompletedAtUtc);
    }

    [Fact]
    public void RecordFieldSurveyResult_BeforeScheduleOrBeforeTime_ThrowsConflict()
    {
        var app = Kyb(KybStatus.UnderReview);
        Assert.Throws<ConflictException>(() => app.RecordFieldSurveyResult("Đạt", Now));

        app.ScheduleFieldSurvey(Now.AddHours(1), null, Now);
        Assert.Throws<ConflictException>(() => app.RecordFieldSurveyResult("Đạt", Now));
        Assert.Throws<ValidationException>(() => app.RecordFieldSurveyResult(" ", Now.AddHours(2)));

        app.RecordFieldSurveyResult("Đạt yêu cầu", Now.AddHours(2));
        Assert.Equal(Now.AddHours(2), app.FieldSurveyCompletedAtUtc);
    }

    [Fact]
    public void Tier1Lot_AfterSurveyCompleted_CanBeApproved()
    {
        var app = Kyb(KybStatus.UnderReview, Lot(totalSlots: 120));
        app.ScheduleFieldSurvey(Now.AddHours(1), null, Now);
        app.RecordFieldSurveyResult("Đạt", Now.AddHours(2));

        app.Approve(AdminId, app.ParkingLot, 10, null, Now.AddHours(3));

        Assert.Equal(KybStatus.Approved, app.Status);
    }

    // ==================== Use cases: audit + outbox trong 1 SaveChanges ====================

    [Fact]
    public async Task StartReview_SetsReviewer_AndWritesOneAudit()
    {
        var app = Kyb(KybStatus.Submitted);
        var repo = new FakeReviewRepo(app);
        var useCase = new StartKybReviewUseCase(repo, new FakeQueries(app), new FixedClock(Now));

        var dto = await useCase.ExecuteAsync(app.Id, AdminId);

        Assert.Equal("UnderReview", dto.Status);
        Assert.Equal(AdminId, dto.ReviewedByUserId);
        Assert.Equal(1, repo.SaveCount);
        var audit = SingleAudit(repo);
        Assert.Equal("ParkingService", audit.SourceService);
        Assert.Equal("KybApplication.ReviewStarted", audit.Action);
        Assert.Equal("KybApplication", audit.EntityName);
        Assert.Equal(app.Id.ToString(), audit.EntityId);
        Assert.Equal(AdminId, audit.UserId);
        Assert.Equal(Now, audit.OccurredAtUtc);
        Assert.Equal("Submitted", JsonDocument.Parse(audit.OldValuesJson!).RootElement.GetProperty("status").GetString());
        Assert.Equal("UnderReview", JsonDocument.Parse(audit.NewValuesJson!).RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Approve_WithSignedContract_ActivatesLot_WritesAuditAndParkingLotApproved_InSameSave()
    {
        var app = Kyb(KybStatus.UnderReview);
        var repo = new FakeReviewRepo(app, activeSlots: 3);
        var useCase = new ApproveKybApplicationUseCase(repo, new FakeQueries(app, 3), new FixedClock(Now));

        var dto = await useCase.ExecuteAsync(app.Id, new ApproveKybApplicationRequest(" Đạt ", " HD-SP-2026-0007 "), AdminId);

        Assert.Equal("Approved", dto.Status);
        Assert.Equal("Active", dto.ParkingLot.Status);
        Assert.Equal(1, repo.SaveCount);
        var audit = SingleAudit(repo);
        Assert.Equal("KybApplication.Approved", audit.Action);
        Assert.Equal("HD-SP-2026-0007 – Đạt", audit.Reason);
        var approved = Assert.Single(repo.Saved.OfType<ParkingLotApproved>());
        Assert.Equal((7, 57, "Bãi xe Thủ Đức"), (approved.ParkingLotId, approved.OwnerProfileId, approved.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("HD-SP-2026-0007-0000000000000000000")]
    public async Task Approve_WithoutValidContractNumber_Throws400_AndSavesNothing(string? contractNumber)
    {
        var app = Kyb(KybStatus.UnderReview);
        var repo = new FakeReviewRepo(app);
        var useCase = new ApproveKybApplicationUseCase(repo, new FakeQueries(app), new FixedClock(Now));

        await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(app.Id, new ApproveKybApplicationRequest(null, contractNumber), AdminId));
        Assert.Equal(KybStatus.UnderReview, app.Status);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Approve_Again_WhenAlreadyApprovedAndActive_IsIdempotent()
    {
        var app = Kyb(KybStatus.Approved, Lot(status: ParkingLotStatus.Active));
        var repo = new FakeReviewRepo(app);
        var useCase = new ApproveKybApplicationUseCase(repo, new FakeQueries(app), new FixedClock(Now));

        var dto = await useCase.ExecuteAsync(app.Id, new ApproveKybApplicationRequest(null, "HD-SP-2026-0007"), AdminId);

        Assert.Equal("Active", dto.ParkingLot.Status);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Approve_LegacyApprovedPendingLot_ActivatesLot()
    {
        var app = Kyb(KybStatus.Approved);
        var repo = new FakeReviewRepo(app, activeSlots: 3);
        var useCase = new ApproveKybApplicationUseCase(repo, new FakeQueries(app, 3), new FixedClock(Now));

        var dto = await useCase.ExecuteAsync(app.Id, new ApproveKybApplicationRequest(null, "HD-SP-2026-0007"), AdminId);

        Assert.Equal("Active", dto.ParkingLot.Status);
        Assert.Equal(("KybApplication.LotActivated", "HD-SP-2026-0007"), (SingleAudit(repo).Action, SingleAudit(repo).Reason));
        Assert.Single(repo.Saved.OfType<ParkingLotApproved>());
    }

    [Fact]
    public async Task Approve_UnknownKyb_Throws404()
        => await Assert.ThrowsAsync<NotFoundException>(() => new ApproveKybApplicationUseCase(new FakeReviewRepo(null), new FakeQueries(null),
            new FixedClock(Now)).ExecuteAsync(31, new ApproveKybApplicationRequest(null, "HD-SP-2026-0007"), AdminId));

    // ==================== ActivateLot (dữ liệu cũ design v8: KYB Approved, bãi còn chờ) ====================

    [Fact]
    public void ActivateLot_AfterApproval_ActivatesPendingLot()
    {
        var app = Kyb(KybStatus.Approved);

        Assert.True(app.ActivateLot(app.ParkingLot, activeSlotCount: 5));
        Assert.Equal(ParkingLotStatus.Active, app.ParkingLot.Status);
    }

    [Fact]
    public void ActivateLot_WhenLotAlreadyActive_IsNoOp()
    {
        var app = Kyb(KybStatus.Approved, Lot(status: ParkingLotStatus.Active));

        Assert.False(app.ActivateLot(app.ParkingLot, activeSlotCount: 5));
        Assert.Equal(ParkingLotStatus.Active, app.ParkingLot.Status);
    }

    [Theory]
    [InlineData(KybStatus.Submitted)]
    [InlineData(KybStatus.UnderReview)]
    [InlineData(KybStatus.Rejected)]
    [InlineData(KybStatus.NeedsMoreInfo)]
    public void ActivateLot_BeforeApproval_ThrowsConflict(KybStatus status)
    {
        var app = Kyb(status);

        var ex = Assert.Throws<ConflictException>(() => app.ActivateLot(app.ParkingLot, 5));
        Assert.Contains("chưa được duyệt", ex.Message);
        Assert.Equal(ParkingLotStatus.PendingApproval, app.ParkingLot.Status);
    }

    [Fact]
    public void ActivateLot_SuspendedLot_ThrowsConflict()
    {
        var app = Kyb(KybStatus.Approved, Lot(status: ParkingLotStatus.Suspended));

        Assert.Throws<ConflictException>(() => app.ActivateLot(app.ParkingLot, 5));
    }

    [Fact]
    public async Task Approve_WhenGuardFails_SavesNothing()
    {
        var app = Kyb(KybStatus.UnderReview, Lot(totalSlots: 80));
        var repo = new FakeReviewRepo(app);
        var useCase = new ApproveKybApplicationUseCase(repo, new FakeQueries(app), new FixedClock(Now));

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(app.Id, new ApproveKybApplicationRequest(null, "HD-SP-2026-0007"), AdminId));

        Assert.Equal(0, repo.SaveCount);
        Assert.Empty(repo.Saved);
    }

    [Fact]
    public async Task Reject_WritesAuditWithReason_NoLotEvent()
    {
        var app = Kyb(KybStatus.UnderReview);
        var repo = new FakeReviewRepo(app);
        var useCase = new RejectKybApplicationUseCase(repo, new FakeQueries(app), new FixedClock(Now));

        var dto = await useCase.ExecuteAsync(app.Id, new RejectKybApplicationRequest("  Ảnh giả  "), AdminId);

        Assert.Equal("Rejected", dto.Status);
        Assert.Equal("Ảnh giả", dto.RejectReason);
        Assert.Equal("PendingApproval", dto.ParkingLot.Status);
        var audit = SingleAudit(repo);
        Assert.Equal("KybApplication.Rejected", audit.Action);
        Assert.Equal("Ảnh giả", audit.Reason);
        Assert.Empty(repo.Saved.OfType<ParkingLotApproved>());
    }

    [Fact]
    public async Task Reject_MissingReason_Throws400_BeforeSaving()
    {
        var app = Kyb(KybStatus.UnderReview);
        var repo = new FakeReviewRepo(app);
        var useCase = new RejectKybApplicationUseCase(repo, new FakeQueries(app), new FixedClock(Now));

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(app.Id, new RejectKybApplicationRequest(null), AdminId));
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task RequestMoreInfo_StoresNote_AndAudits()
    {
        var app = Kyb(KybStatus.UnderReview);
        var repo = new FakeReviewRepo(app);
        var useCase = new RequestKybMoreInfoUseCase(repo, new FakeQueries(app), new FixedClock(Now));

        var dto = await useCase.ExecuteAsync(app.Id, new RequestKybMoreInfoRequest("Bổ sung ảnh có GPS"), AdminId);

        Assert.Equal("NeedsMoreInfo", dto.Status);
        Assert.Equal("Bổ sung ảnh có GPS", dto.ReviewNote);
        Assert.Equal("KybApplication.InfoRequested", SingleAudit(repo).Action);
    }

    [Fact]
    public async Task ScheduleSurvey_UnspecifiedKindTreatedAsUtc_AndAudits()
    {
        var app = Kyb(KybStatus.UnderReview);
        var repo = new FakeReviewRepo(app);
        var useCase = new ScheduleKybFieldSurveyUseCase(repo, new FakeQueries(app), new FixedClock(Now));
        var at = DateTime.SpecifyKind(Now.AddDays(1), DateTimeKind.Unspecified);

        var dto = await useCase.ExecuteAsync(app.Id, new ScheduleKybFieldSurveyRequest(at, "Barie, ANPR"), AdminId);

        Assert.Equal(Now.AddDays(1), dto.FieldSurveyAtUtc);
        Assert.Equal(DateTimeKind.Utc, app.FieldSurveyAtUtc!.Value.Kind);
        Assert.Equal("KybApplication.FieldSurveyScheduled", SingleAudit(repo).Action);
    }

    [Fact]
    public async Task ScheduleSurvey_MissingTime_Throws400()
    {
        var app = Kyb(KybStatus.UnderReview);
        var useCase = new ScheduleKybFieldSurveyUseCase(new FakeReviewRepo(app), new FakeQueries(app), new FixedClock(Now));
        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(app.Id, new ScheduleKybFieldSurveyRequest(null, null), AdminId));
    }

    [Fact]
    public async Task RecordSurveyResult_AfterScheduledTime_MarksCompleted()
    {
        var app = Kyb(KybStatus.UnderReview);
        app.ScheduleFieldSurvey(Now.AddHours(-2).AddDays(1), null, Now.AddHours(-3));
        var later = Now.AddDays(1);
        var repo = new FakeReviewRepo(app);
        var useCase = new RecordKybFieldSurveyResultUseCase(repo, new FakeQueries(app), new FixedClock(later));

        var dto = await useCase.ExecuteAsync(app.Id, new RecordKybFieldSurveyResultRequest("Đạt"), AdminId);

        Assert.Equal(later, dto.FieldSurveyCompletedAtUtc);
        Assert.Equal("KybApplication.FieldSurveyCompleted", SingleAudit(repo).Action);
    }

    [Fact]
    public async Task UseCases_UnknownId_Throws404_AndInvalidIds_Throw400()
    {
        var repo = new FakeReviewRepo(null);
        var start = new StartKybReviewUseCase(repo, new FakeQueries(null), new FixedClock(Now));
        await Assert.ThrowsAsync<NotFoundException>(() => start.ExecuteAsync(99, AdminId));
        await Assert.ThrowsAsync<ValidationException>(() => start.ExecuteAsync(0, AdminId));
        await Assert.ThrowsAsync<ValidationException>(() => start.ExecuteAsync(1, 0));
        await Assert.ThrowsAsync<NotFoundException>(() => new GetAdminKybApplicationUseCase(new FakeQueries(null)).ExecuteAsync(99));
    }

    [Fact]
    public async Task ConcurrentUpdate_SurfacesAs409()
    {
        var app = Kyb(KybStatus.Submitted);
        var repo = new FakeReviewRepo(app) { ThrowConcurrency = true };
        var useCase = new StartKybReviewUseCase(repo, new FakeQueries(app), new FixedClock(Now));
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(app.Id, AdminId));
    }

    // ==================== Danh sách + chi tiết ====================

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_InvalidPaging_Throws400(int page, int pageSize)
    {
        var useCase = new ListAdminKybApplicationsUseCase(new FakeQueries(null));
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(null, null, page, pageSize));
    }

    [Fact]
    public async Task List_TrimsSearch_PassesFilters_AndMapsStatusText()
    {
        var queries = new FakeQueries(null);
        queries.Rows.Add(new AdminKybListRow(31, 7, "Bãi xe Thủ Đức", "12 Võ Văn Ngân", "TP.HCM", "Thủ Đức", 57, 30,
            KybStatus.Submitted, ParkingLotStatus.PendingApproval, Now, false, null, null, null, null));
        var useCase = new ListAdminKybApplicationsUseCase(queries);

        var result = await useCase.ExecuteAsync(KybStatus.Submitted, "  thủ đức ", 2, 10);

        Assert.Equal((KybStatus.Submitted, "thủ đức", 2, 10), queries.LastList!.Value);
        var item = Assert.Single(result.Items);
        Assert.Equal(("Submitted", "PendingApproval"), (item.Status, item.LotStatus));
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public void Detail_ComputesGpsDistanceWarningAndBlockers()
    {
        var app = Kyb(KybStatus.UnderReview, Lot(totalSlots: 80));
        app.PhotoLatitude = 10.85 + 0.0018;   // ~200 m về phía bắc

        var dto = AdminKybMapper.ToDto(Row(app, activeSlots: 0));

        Assert.InRange(dto.GpsDistanceMeters!.Value, 190, 210);
        Assert.True(dto.GpsWarning);
        Assert.True(dto.FieldSurveyMandatory);
        Assert.Equal(["/uploads/kyb/1.jpg"], dto.SitePhotoUrls);
        Assert.Equal(2, dto.ApprovalBlockers.Count);    // chưa khảo sát + chưa có ô đỗ
        Assert.Equal(0, dto.ParkingLot.ActiveSlotCount);
    }

    [Fact]
    public void Detail_PhotoAtLot_NoWarning_NoBlockersWhenReady()
    {
        var dto = AdminKybMapper.ToDto(Row(Kyb(KybStatus.UnderReview), activeSlots: 4));
        Assert.Equal(0, dto.GpsDistanceMeters);
        Assert.False(dto.GpsWarning);
        Assert.Empty(dto.ApprovalBlockers);
    }

    [Fact]
    public void Detail_WithoutPhotoGps_WarnsAndHasNoDistance()
    {
        var app = Kyb(KybStatus.Submitted);
        app.PhotoLatitude = null;
        var dto = AdminKybMapper.ToDto(Row(app, 4));
        Assert.Null(dto.GpsDistanceMeters);
        Assert.True(dto.GpsWarning);
    }

    // ==================== Phân quyền ====================

    [Fact]
    public void AdminController_RequiresAdminPolicy()
    {
        var attribute = typeof(AdminKybApplicationsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("RequireAdmin", attribute.Policy);
    }
}
