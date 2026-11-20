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

    private static PartnerApplication App(PartnerApplicationStatus status)
    {
        var app = PartnerApplication.Submit("PA-2026-0012", Now);
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!.SetValue(app, AppId);
        app.BusinessName = "Công ty CP Bãi xe Thủ Đức";
        PartnerApplicationStatus[] path = status switch
        {
            PartnerApplicationStatus.UnderReview => [PartnerApplicationStatus.UnderReview],
            PartnerApplicationStatus.NeedsMoreInfo => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.NeedsMoreInfo],
            PartnerApplicationStatus.AccountIssued => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.AccountIssued],
            PartnerApplicationStatus.Rejected => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.Rejected],
            PartnerApplicationStatus.Cancelled => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.Cancelled],
            _ => [],
        };
        foreach (var step in path) app.MoveTo(step);
        return app;
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    private static TimeProvider Clock => new FixedClock(Now);

    private sealed class FakeLinks : IPartnerEditLinkBuilder
    {
        public string Build(string applicationCode, string token) => $"https://web.test/partner/apply/edit#code={applicationCode}&token={token}";
    }

    /// <summary>Cổng UserService giả: ghi lại email / yêu cầu gỡ tài khoản, hoặc ném lỗi theo cấu hình.</summary>
    private sealed class FakeOwners : IOwnerAccountClient
    {
        public List<PartnerEmailCommand> Emails { get; } = [];
        public List<(int OwnerProfileId, string Reason)> Discarded { get; } = [];
        public Exception? FailWith { get; set; }

        public Task<IssuedOwnerAccountDto> IssueOwnerAccountAsync(IssueOwnerAccountCommand command, string idempotencyKey, CancellationToken cancellationToken)
            => throw new NotSupportedException();
        public Task ResendActivationAsync(int ownerProfileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DiscardOwnerAccountAsync(int ownerProfileId, string reason, CancellationToken cancellationToken)
        {
            if (FailWith is not null) throw FailWith;
            Discarded.Add((ownerProfileId, reason));
            return Task.CompletedTask;
        }

        public Task SendPartnerEmailAsync(PartnerEmailCommand command, CancellationToken cancellationToken)
        {
            if (FailWith is not null) throw FailWith;
            Emails.Add(command);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSync(PartnerApplication app, bool activates = false) : IOwnerActivationSync
    {
        public Task<bool> SyncAsync(int applicationId, CancellationToken cancellationToken)
        {
            if (!activates) return Task.FromResult(false);
            app.MarkActivated();
            return Task.FromResult(true);
        }
    }

    private static CancelPartnerApplicationUseCase Cancel(PartnerApplication app, FakeRepository repo, FakeOwners? owners = null, bool activates = false)
        => new(repo, new FakeQueries(app), owners ?? new FakeOwners(), new FakeSync(app, activates), Clock);

    private static RequestPartnerInfoUseCase RequestInfo(PartnerApplication app, FakeRepository repo, FakeOwners? owners = null)
        => new(repo, new FakeQueries(app), owners ?? new FakeOwners(), new FakeLinks(), Clock);

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
                null, app.CreatedUserId, app.CreatedOwnerProfileId, app.CreatedAtUtc, app.UpdatedAtUtc,
                PartnerApplication.NextStatuses(app.Status).Select(s => s.ToString()).ToList(),
                Convert.ToBase64String(app.RowVersion), app.CancelledAtUtc, app.CleanupSyncStatus.ToString(), app.CleanupError,
                app.InfoRequestItems, app.InfoRequestedAtUtc, app.InfoRequestDueAtUtc, app.InfoProvidedAtUtc,
                app.InfoRequestEmailStatus.ToString(), app.InfoRequestEmailError));
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
    [InlineData(PartnerApplicationStatus.AccountIssued)]
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

        var dto = await RequestInfo(app, repo)
            .ExecuteAsync(AppId, new PartnerRequestInfoRequest("  Bổ sung giấy phép có dấu đỏ  "), AdminId);

        Assert.Equal("NeedsMoreInfo", dto.Status);
        Assert.Equal("Bổ sung giấy phép có dấu đỏ", dto.InfoRequestNote);
        Assert.Equal(["UnderReview", "Cancelled"], dto.NextStatuses);
        Assert.Equal(["PartnerApplication.InfoRequested", "PartnerApplication.InfoRequestEmailQueued"], repo.Audits.Select(a => a.Action));
        Assert.Equal("Bổ sung giấy phép có dấu đỏ", repo.Audits[0].Reason);
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
            RequestInfo(app, repo).ExecuteAsync(AppId, new PartnerRequestInfoRequest(note), AdminId));

        Assert.Equal("Nội dung cần bổ sung không được để trống.", ex.Message);
        Assert.Equal(PartnerApplicationStatus.UnderReview, app.Status);
        Assert.Empty(repo.Audits);
    }

    [Fact]
    public async Task Request_info_from_submitted_is_409_even_with_blank_note()
    {
        var app = App(PartnerApplicationStatus.Submitted);
        await Assert.ThrowsAsync<ConflictException>(() =>
            RequestInfo(app, new FakeRepository(app))
                .ExecuteAsync(AppId, new PartnerRequestInfoRequest(null), AdminId));
    }

    [Fact]
    public void Request_info_note_over_1000_chars_is_400()
        => Assert.Throws<ValidationException>(() => App(PartnerApplicationStatus.UnderReview).RequestInfo(AdminId, new string('x', 1001)));

    // ==================== A11 reject (T9) ====================

    [Fact]
    public async Task Reject_from_under_review_is_terminal_and_audits_reason()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var repo = new FakeRepository(app);

        var dto = await new RejectPartnerApplicationUseCase(repo, new FakeQueries(app))
            .ExecuteAsync(AppId, new PartnerReasonRequest("MST không khớp giấy phép"), AdminId);

        Assert.Equal("Rejected", dto.Status);
        Assert.Equal("MST không khớp giấy phép", dto.RejectReason);
        Assert.Empty(dto.NextStatuses);
        var audit = Assert.Single(repo.Audits);
        Assert.Equal("PartnerApplication.Rejected", audit.Action);
        Assert.Equal("MST không khớp giấy phép", audit.Reason);
        Assert.Equal("UnderReview", Json(audit.OldValuesJson).GetProperty("status").GetString());
        Assert.Equal("Rejected", Json(audit.NewValuesJson).GetProperty("status").GetString());
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
    [InlineData(PartnerApplicationStatus.Submitted)]
    [InlineData(PartnerApplicationStatus.UnderReview)]
    [InlineData(PartnerApplicationStatus.NeedsMoreInfo)]
    public async Task Cancel_allowed_before_account_issued_without_touching_user_service(PartnerApplicationStatus status)
    {
        var app = App(status);
        var repo = new FakeRepository(app);
        var owners = new FakeOwners();

        var dto = await Cancel(app, repo, owners).ExecuteAsync(AppId, new PartnerReasonRequest("Đối tác xin rút hồ sơ"), AdminId);

        Assert.Equal("Cancelled", dto.Status);
        Assert.Equal("Đối tác xin rút hồ sơ", dto.CancelReason);
        Assert.Equal(("NotRequired", AdminId, Now), (dto.CleanupSyncStatus, app.CancelledByAdminId, app.CancelledAtUtc));
        Assert.Equal("PartnerApplication.Cancelled", Assert.Single(repo.Audits).Action);
        Assert.Empty(owners.Discarded);
    }

    [Fact]
    public async Task Cancel_account_issued_needs_typed_code_then_removes_owner_account_and_audits_both_steps()
    {
        var app = App(PartnerApplicationStatus.AccountIssued);
        app.CreatedOwnerProfileId = 7;
        var repo = new FakeRepository(app);
        var owners = new FakeOwners();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Cancel(app, repo, owners).ExecuteAsync(AppId, new PartnerReasonRequest("Không liên lạc được"), AdminId));
        Assert.Contains("Gõ lại mã hồ sơ", ex.Message);
        Assert.Equal(PartnerApplicationStatus.AccountIssued, app.Status);
        Assert.Empty(repo.Audits);

        var dto = await Cancel(app, repo, owners)
            .ExecuteAsync(AppId, new PartnerReasonRequest("Không liên lạc được", ConfirmCode: "pa-2026-0012"), AdminId);

        Assert.Equal(("Cancelled", "Synced"), (dto.Status, dto.CleanupSyncStatus));
        Assert.Equal([(7, "Không liên lạc được")], owners.Discarded);
        Assert.Equal(["PartnerApplication.Cancelled", "PartnerApplication.OwnerAccountDiscarded"], repo.Audits.Select(a => a.Action));
    }

    [Fact]
    public async Task Cancel_account_issued_keeps_application_cancelled_when_user_service_fails_and_retry_succeeds()
    {
        var app = App(PartnerApplicationStatus.AccountIssued);
        app.CreatedOwnerProfileId = 7;
        var repo = new FakeRepository(app);
        var owners = new FakeOwners { FailWith = new DependencyUnavailableException("UserService hiện không phản hồi, vui lòng thử lại sau.") };

        var dto = await Cancel(app, repo, owners)
            .ExecuteAsync(AppId, new PartnerReasonRequest("Hủy", ConfirmCode: "PA-2026-0012"), AdminId);

        Assert.Equal(("Cancelled", "Failed"), (dto.Status, dto.CleanupSyncStatus));
        Assert.Equal("UserService hiện không phản hồi, vui lòng thử lại sau.", dto.CleanupError);
        Assert.Equal("PartnerApplication.OwnerCleanupFailed", repo.Audits[^1].Action);

        owners.FailWith = null;
        var retried = await new RetryPartnerCleanupUseCase(repo, new FakeQueries(app), owners, Clock).ExecuteAsync(AppId, AdminId);

        Assert.Equal("Synced", retried.CleanupSyncStatus);
        Assert.Equal([(7, "Hủy")], owners.Discarded);
        await Assert.ThrowsAsync<ConflictException>(() => new RetryPartnerCleanupUseCase(repo, new FakeQueries(app), owners, Clock).ExecuteAsync(AppId, AdminId));
    }

    [Fact]
    public async Task Cancel_account_issued_is_409_when_owner_already_activated_in_user_service()
    {
        var app = App(PartnerApplicationStatus.AccountIssued);
        app.CreatedOwnerProfileId = 7;
        var repo = new FakeRepository(app);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Cancel(app, repo, activates: true).ExecuteAsync(AppId, new PartnerReasonRequest("Hủy", ConfirmCode: "PA-2026-0012"), AdminId));

        Assert.Contains("Đình chỉ hoặc Xóa user", ex.Message);
        Assert.Equal(PartnerApplicationStatus.Activated, app.Status);
    }

    [Fact]
    public async Task Cancel_twice_is_idempotent_and_wrong_row_version_is_409()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var repo = new FakeRepository(app);
        var useCase = Cancel(app, repo);

        await useCase.ExecuteAsync(AppId, new PartnerReasonRequest("Lần 1"), AdminId);
        var again = await useCase.ExecuteAsync(AppId, new PartnerReasonRequest("Lần 2"), AdminId);

        Assert.Equal(("Cancelled", "Lần 1"), (again.Status, again.CancelReason));
        Assert.Single(repo.Audits);

        var fresh = App(PartnerApplicationStatus.UnderReview);
        await Assert.ThrowsAsync<ConflictException>(() => Cancel(fresh, new FakeRepository(fresh))
            .ExecuteAsync(AppId, new PartnerReasonRequest("Hủy", RowVersion: Convert.ToBase64String([1, 2, 3])), AdminId));
    }

    [Fact]
    public async Task Cancel_not_allowed_after_rejection()
    {
        var app = App(PartnerApplicationStatus.Rejected);
        await Assert.ThrowsAsync<ConflictException>(() => Cancel(app, new FakeRepository(app)).ExecuteAsync(AppId, new PartnerReasonRequest("Lý do"), AdminId));
    }

    // ==================== A4 request-info: mục, hạn, email ====================

    [Fact]
    public async Task Request_info_stores_items_and_default_due_issues_hashed_link_and_queues_email_without_sensitive_data()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        app.FullName = "Nguyễn Văn An";
        app.Email = "an@example.com";
        var repo = new FakeRepository(app);
        var owners = new FakeOwners();

        var dto = await RequestInfo(app, repo, owners).ExecuteAsync(AppId,
            new PartnerRequestInfoRequest("Chụp lại CCCD rõ nét", ["doc:idCardFront", "taxCode", "doc:idCardFront"]), AdminId);

        Assert.Equal(["doc:idCardFront", "taxCode"], dto.InfoRequestItems);
        Assert.Equal(new DateTime(2026, 10, 27, 16, 59, 59, DateTimeKind.Utc), dto.InfoRequestDueAtUtc);   // 23:59:59 ICT sau 7 ngày
        Assert.Equal(("Synced", Now), (dto.InfoRequestEmailStatus, dto.InfoRequestedAtUtc));
        Assert.Equal(64, app.EditTokenHash!.Length);
        Assert.Equal(dto.InfoRequestDueAtUtc, app.EditTokenExpiresAtUtc);

        var mail = Assert.Single(owners.Emails);
        Assert.Equal(("PartnerInfoRequested", "an@example.com", "PA-2026-0012"), (mail.Template, mail.Destination, mail.ApplicationCode));
        Assert.Equal(["CCCD người đại diện – mặt trước", "Mã số thuế"], mail.Items);
        Assert.StartsWith("https://web.test/partner/apply/edit#code=PA-2026-0012&token=", mail.Link);
        var token = mail.Link![(mail.Link.IndexOf("token=", StringComparison.Ordinal) + 6)..];
        Assert.NotEqual(token, app.EditTokenHash);
        Assert.DoesNotContain(token, string.Join(" ", repo.Audits.Select(a => a.NewValuesJson + a.OldValuesJson + a.Reason)));
    }

    [Fact]
    public async Task Request_info_with_unknown_item_or_past_due_is_400_and_email_failure_is_recorded_for_resend()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        await Assert.ThrowsAsync<ValidationException>(() => RequestInfo(app, new FakeRepository(app))
            .ExecuteAsync(AppId, new PartnerRequestInfoRequest("Ghi chú", ["hack"]), AdminId));
        await Assert.ThrowsAsync<ValidationException>(() => RequestInfo(app, new FakeRepository(app))
            .ExecuteAsync(AppId, new PartnerRequestInfoRequest("Ghi chú", null, Now.AddHours(-1)), AdminId));
        Assert.Equal(PartnerApplicationStatus.UnderReview, app.Status);

        var repo = new FakeRepository(app);
        var owners = new FakeOwners { FailWith = new DependencyUnavailableException("SMTP chưa cấu hình") };
        var dto = await RequestInfo(app, repo, owners).ExecuteAsync(AppId, new PartnerRequestInfoRequest("Ghi chú"), AdminId);
        Assert.Equal(("NeedsMoreInfo", "Failed", "SMTP chưa cấu hình"), (dto.Status, dto.InfoRequestEmailStatus, dto.InfoRequestEmailError));

        owners.FailWith = null;
        var oldHash = app.EditTokenHash;
        var resent = await new ResendPartnerInfoRequestUseCase(repo, new FakeQueries(app), owners, new FakeLinks(), Clock)
            .ExecuteAsync(AppId, new PartnerResendInfoRequest(), AdminId);
        Assert.Equal("Synced", resent.InfoRequestEmailStatus);
        Assert.NotEqual(oldHash, app.EditTokenHash);
        Assert.Single(owners.Emails);
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
