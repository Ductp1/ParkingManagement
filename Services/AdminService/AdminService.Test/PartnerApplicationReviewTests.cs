using System.Reflection;
using System.Text.Json;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-116 (A3 / A4 / A11 / A12): thẩm định hồ sơ đối tác – hồ sơ + AuditLog cùng 1 SaveChanges (fake repository / queries).
public class PartnerApplicationReviewTests
{
    private const int AdminId = 1;
    private const int AppId = 12;
    private static readonly DateTime Now = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);

    private static PartnerApplication App(PartnerApplicationStatus status, bool withCurrentDraft = false)
    {
        var app = PartnerApplication.Submit("PA-2026-0012", Now);
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!.SetValue(app, AppId);
        app.BusinessName = "Công ty CP Bãi xe Thủ Đức";
        PartnerApplicationStatus[] path = status switch
        {
            PartnerApplicationStatus.UnderReview => [PartnerApplicationStatus.UnderReview],
            PartnerApplicationStatus.NeedsMoreInfo => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.NeedsMoreInfo],
            PartnerApplicationStatus.ContractPending => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.ContractPending],
            PartnerApplicationStatus.AccountIssued =>
                [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.ContractPending, PartnerApplicationStatus.AccountIssued],
            PartnerApplicationStatus.Rejected => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.Rejected],
            PartnerApplicationStatus.Cancelled => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.Cancelled],
            _ => [],
        };
        foreach (var step in path) app.MoveTo(step);
        if (withCurrentDraft)
        {
            app.ContractNumber = "HD-SP-2026-0012";
            app.ContractDrafts.Add(new ContractDraft { Version = 1, ContractNumber = "HD-SP-2026-0012", Status = ContractDraftStatus.Superseded });
            app.ContractDrafts.Add(new ContractDraft { Version = 2, ContractNumber = "HD-SP-2026-0012", Status = ContractDraftStatus.Current });
        }
        return app;
    }

    private sealed class FakeRepository(PartnerApplication? app) : IPartnerApplicationRepository
    {
        public List<AuditLog> Audits { get; } = [];
        public bool Concurrency { get; init; }

        public Task<PartnerApplication?> FindTrackedAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult(app?.Id == id ? app : null);

        public Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            if (Concurrency) throw new ConflictException("Hồ sơ đối tác vừa được Admin khác cập nhật, vui lòng tải lại và thử lại.");
            Audits.Add(auditLog);
            return Task.CompletedTask;
        }
    }

    /// <summary>Cổng đọc giả phản ánh trạng thái entity sau khi lưu.</summary>
    private sealed class FakeQueries(PartnerApplication? app) : IPartnerApplicationQueries
    {
        public Task<PagedResult<PartnerApplicationSummaryDto>> ListAsync(PartnerApplicationStatus? status, string? search, int page,
            int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PartnerApplicationDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult(app?.Id != id ? null : new PartnerApplicationDetailDto(
                app.Id, app.Code, app.Status.ToString(), app.FullName, app.Email, app.Phone, app.Position, app.BusinessType.ToString(),
                app.BusinessName, app.TaxCode, app.BusinessAddress, app.LegalRepresentativeName, app.LegalRepresentativePosition,
                app.BusinessLicenseUrl, app.LotCount, app.PrimaryLotAddress, app.EstimatedCarSlots, app.EstimatedMotorbikeSlots,
                app.OperatingHours, app.ConsentAcceptedAtUtc, app.ReviewerAdminId, app.InfoRequestNote, app.RejectReason, app.CancelReason,
                app.ContractNumber, app.CreatedUserId, app.CreatedOwnerProfileId, app.CreatedAtUtc, app.UpdatedAtUtc,
                PartnerApplication.NextStatuses(app.Status).Select(s => s.ToString()).ToList(), []));
    }

    private static JsonElement Json(string? json) => JsonDocument.Parse(json!).RootElement;

    // ==================== A3 start-review (T2) ====================

    [Fact]
    public async Task Start_review_moves_to_under_review_sets_reviewer_and_audits()
    {
        var app = App(PartnerApplicationStatus.Submitted);
        var repo = new FakeRepository(app);

        var dto = await new StartPartnerReviewUseCase(repo, new FakeQueries(app)).ExecuteAsync(AppId, AdminId);

        Assert.Equal("UnderReview", dto.Status);
        Assert.Equal(AdminId, dto.ReviewerAdminId);
        var audit = Assert.Single(repo.Audits);
        Assert.Equal(("AdminService", "PartnerApplication.ReviewStarted", "PartnerApplication", "12", AdminId),
            (audit.SourceService, audit.Action, audit.EntityName, audit.EntityId, audit.UserId));
        Assert.Equal("Submitted", Json(audit.OldValuesJson).GetProperty("status").GetString());
        Assert.Equal("UnderReview", Json(audit.NewValuesJson).GetProperty("status").GetString());
        Assert.Null(audit.Reason);
    }

    [Theory]
    [InlineData(PartnerApplicationStatus.UnderReview)]
    [InlineData(PartnerApplicationStatus.ContractPending)]
    [InlineData(PartnerApplicationStatus.Rejected)]
    public async Task Start_review_from_wrong_state_is_409_and_saves_nothing(PartnerApplicationStatus status)
    {
        var app = App(status);
        var repo = new FakeRepository(app);

        await Assert.ThrowsAsync<ConflictException>(() => new StartPartnerReviewUseCase(repo, new FakeQueries(app)).ExecuteAsync(AppId, AdminId));

        Assert.Empty(repo.Audits);
        Assert.Equal(status, app.Status);
    }

    // ==================== A4 request-info (T3) ====================

    [Fact]
    public async Task Request_info_stores_trimmed_note_and_audits_reason()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var repo = new FakeRepository(app);

        var dto = await new RequestPartnerInfoUseCase(repo, new FakeQueries(app))
            .ExecuteAsync(AppId, new PartnerRequestInfoRequest("  Bổ sung giấy phép có dấu đỏ  "), AdminId);

        Assert.Equal("NeedsMoreInfo", dto.Status);
        Assert.Equal("Bổ sung giấy phép có dấu đỏ", dto.InfoRequestNote);
        Assert.Equal(["UnderReview", "Cancelled"], dto.NextStatuses);
        var audit = Assert.Single(repo.Audits);
        Assert.Equal("PartnerApplication.InfoRequested", audit.Action);
        Assert.Equal("Bổ sung giấy phép có dấu đỏ", audit.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Request_info_without_note_is_400_and_keeps_state(string? note)
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var repo = new FakeRepository(app);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            new RequestPartnerInfoUseCase(repo, new FakeQueries(app)).ExecuteAsync(AppId, new PartnerRequestInfoRequest(note), AdminId));

        Assert.Equal("Nội dung cần bổ sung không được để trống.", ex.Message);
        Assert.Equal(PartnerApplicationStatus.UnderReview, app.Status);
        Assert.Empty(repo.Audits);
    }

    [Fact]
    public async Task Request_info_from_submitted_is_409_even_with_blank_note()
    {
        var app = App(PartnerApplicationStatus.Submitted);
        await Assert.ThrowsAsync<ConflictException>(() =>
            new RequestPartnerInfoUseCase(new FakeRepository(app), new FakeQueries(app))
                .ExecuteAsync(AppId, new PartnerRequestInfoRequest(null), AdminId));
    }

    [Fact]
    public void Request_info_note_over_1000_chars_is_400()
        => Assert.Throws<ValidationException>(() => App(PartnerApplicationStatus.UnderReview).RequestInfo(AdminId, new string('x', 1001)));

    // ==================== A11 reject (T9) ====================

    [Fact]
    public async Task Reject_from_contract_pending_supersedes_current_draft()
    {
        var app = App(PartnerApplicationStatus.ContractPending, withCurrentDraft: true);
        var repo = new FakeRepository(app);

        var dto = await new RejectPartnerApplicationUseCase(repo, new FakeQueries(app))
            .ExecuteAsync(AppId, new PartnerReasonRequest("MST không khớp giấy phép"), AdminId);

        Assert.Equal("Rejected", dto.Status);
        Assert.Equal("MST không khớp giấy phép", dto.RejectReason);
        Assert.Empty(dto.NextStatuses);
        Assert.All(app.ContractDrafts, d => Assert.Equal(ContractDraftStatus.Superseded, d.Status));
        var audit = Assert.Single(repo.Audits);
        Assert.Equal("PartnerApplication.Rejected", audit.Action);
        Assert.Equal(2, Json(audit.OldValuesJson).GetProperty("currentDraftVersion").GetInt32());
        Assert.Equal(JsonValueKind.Null, Json(audit.NewValuesJson).GetProperty("currentDraftVersion").ValueKind);
    }

    [Theory]
    [InlineData(PartnerApplicationStatus.Submitted)]
    [InlineData(PartnerApplicationStatus.NeedsMoreInfo)]
    [InlineData(PartnerApplicationStatus.AccountIssued)]
    [InlineData(PartnerApplicationStatus.Cancelled)]
    public void Reject_not_allowed_from(PartnerApplicationStatus status)
        => Assert.Throws<ConflictException>(() => App(status).Reject(AdminId, "Lý do"));

    [Fact]
    public async Task Reject_without_reason_is_400()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            new RejectPartnerApplicationUseCase(new FakeRepository(app), new FakeQueries(app))
                .ExecuteAsync(AppId, new PartnerReasonRequest(" "), AdminId));
        Assert.Equal("Lý do từ chối không được để trống.", ex.Message);
    }

    // ==================== A12 cancel (T10) ====================

    [Theory]
    [InlineData(PartnerApplicationStatus.UnderReview)]
    [InlineData(PartnerApplicationStatus.NeedsMoreInfo)]
    [InlineData(PartnerApplicationStatus.ContractPending)]
    public async Task Cancel_allowed_before_account_issued(PartnerApplicationStatus status)
    {
        var app = App(status, withCurrentDraft: status == PartnerApplicationStatus.ContractPending);
        var repo = new FakeRepository(app);

        var dto = await new CancelPartnerApplicationUseCase(repo, new FakeQueries(app))
            .ExecuteAsync(AppId, new PartnerReasonRequest("Đối tác xin rút hồ sơ"), AdminId);

        Assert.Equal("Cancelled", dto.Status);
        Assert.Equal("Đối tác xin rút hồ sơ", dto.CancelReason);
        Assert.DoesNotContain(app.ContractDrafts, d => d.Status == ContractDraftStatus.Current);
        Assert.Equal("PartnerApplication.Cancelled", Assert.Single(repo.Audits).Action);
    }

    [Theory]
    [InlineData(PartnerApplicationStatus.Submitted)]
    [InlineData(PartnerApplicationStatus.AccountIssued)]
    [InlineData(PartnerApplicationStatus.Rejected)]
    public async Task Cancel_not_allowed_from(PartnerApplicationStatus status)
    {
        var app = App(status);
        await Assert.ThrowsAsync<ConflictException>(() =>
            new CancelPartnerApplicationUseCase(new FakeRepository(app), new FakeQueries(app))
                .ExecuteAsync(AppId, new PartnerReasonRequest("Lý do"), AdminId));
    }

    // ==================== Chung: 400 / 404 / 409 RowVersion ====================

    [Fact]
    public async Task Invalid_ids_are_400_and_unknown_id_is_404()
    {
        var useCase = new StartPartnerReviewUseCase(new FakeRepository(null), new FakeQueries(null));
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(0, AdminId));
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(AppId, 0));
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(99, AdminId));
        Assert.Contains("Hồ sơ đối tác", ex.Message);
    }

    [Fact]
    public async Task Concurrent_update_surfaces_as_409()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var repo = new FakeRepository(app) { Concurrency = true };
        await Assert.ThrowsAsync<ConflictException>(() =>
            new RejectPartnerApplicationUseCase(repo, new FakeQueries(app)).ExecuteAsync(AppId, new PartnerReasonRequest("Lý do"), AdminId));
    }
}
