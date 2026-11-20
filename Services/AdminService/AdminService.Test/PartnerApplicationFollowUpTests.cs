using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Application.Features.Users;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using AdminService.Domain.ValueObjects;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using static AdminService.Test.PartnerApplicationSubmitTests;

namespace AdminService.Test;

// US-116 T3 / T4 (link bổ sung hồ sơ + đối tác gửi bổ sung) và T8 (đồng bộ trạng thái kích hoạt từ UserService).
public class PartnerApplicationFollowUpTests
{
    private const int AdminId = 1;
    private static readonly DateTime Now = new(2026, 11, 13, 2, 0, 0, DateTimeKind.Utc);
    private readonly InMemoryIntake store = new();
    private DateTime clockNow = Now;

    private sealed class Clock(Func<DateTime> now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now());
    }

    private TimeProvider Time => new Clock(() => clockNow);

    private sealed class NoStorage : IFileStorage
    {
        public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);
        public Task DeleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static readonly byte[] Pdf = [.. "%PDF-1.7\n"u8, 1, 2, 3];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9];

    /// <summary>Đối tác tự nộp qua form public (không đăng nhập, đủ 4 giấy tờ bắt buộc).</summary>
    private Task<PartnerApplicationReceiptDto> Seed(SubmitPartnerApplicationRequest request)
        => new SubmitPartnerApplicationUseCase(store, new NoStorage(), Time).ExecuteAsync(null, request,
        [
            new PartnerDocumentUpload(AdminService.Domain.Enums.PartnerDocumentType.BusinessLicense, new UploadedFile("dkkd.pdf", Pdf.Length, Pdf)),
            new PartnerDocumentUpload(AdminService.Domain.Enums.PartnerDocumentType.LotLegalProof, new UploadedFile("mb.pdf", Pdf.Length, Pdf)),
            new PartnerDocumentUpload(AdminService.Domain.Enums.PartnerDocumentType.IdCardFront, new UploadedFile("t.png", Png.Length, Png)),
            new PartnerDocumentUpload(AdminService.Domain.Enums.PartnerDocumentType.IdCardBack, new UploadedFile("s.png", Png.Length, Png)),
        ]);

    /// <summary>Hồ sơ mới qua form public rồi Admin chuyển sang NeedsMoreInfo.</summary>
    private async Task<PartnerApplication> NeedsMoreInfo()
    {
        await Seed(Valid());
        var app = store.Applications[^1];
        app.StartReview(AdminId);
        app.RequestInfo(AdminId, "Bổ sung giấy phép kinh doanh và số chỗ ô tô");
        store.Audits.Clear();
        return app;
    }

    private IssuePartnerEditLinkUseCase EditLink => new(store, PartnerEditLinkOptions.Default, Time);
    private GetPartnerApplicationForEditUseCase ForEdit => new(store, Time);
    private ResubmitPartnerApplicationUseCase Resubmit => new(store, store, new NoStorage(), Time);

    // ==================== T3 ====================

    [Fact]
    public async Task Edit_link_is_issued_once_hashed_and_audited()
    {
        var app = await NeedsMoreInfo();

        var link = await EditLink.ExecuteAsync(app.Id, AdminId);

        Assert.Equal((app.Code, Now.AddDays(7)), (link.Code, link.ExpiresAtUtc));
        Assert.True(link.Token.Length >= 40);
        Assert.NotEqual(link.Token, app.EditTokenHash);
        Assert.Equal(64, app.EditTokenHash!.Length);
        var audit = Assert.Single(store.Audits);
        Assert.Equal(("PartnerApplication.EditLinkIssued", (int?)AdminId), (audit.Action, audit.UserId));
        Assert.DoesNotContain(link.Token, audit.NewValuesJson);
    }

    [Fact]
    public async Task Edit_link_needs_needs_more_info_and_reissue_revokes_the_old_one()
    {
        await Seed(Valid() with
        {
            Email = "submitted@example.vn", Phone = "0911111111", TaxCode = "0311111111",
        });
        await Assert.ThrowsAsync<ConflictException>(() => EditLink.ExecuteAsync(store.Applications[0].Id, AdminId));

        var app = await NeedsMoreInfo();
        var first = await EditLink.ExecuteAsync(app.Id, AdminId);
        var second = await EditLink.ExecuteAsync(app.Id, AdminId);

        await Assert.ThrowsAsync<ValidationException>(() => ForEdit.ExecuteAsync(app.Code, first.Token));
        Assert.Equal(app.Code, (await ForEdit.ExecuteAsync(app.Code, second.Token)).Code);
    }

    // ==================== T4 ====================

    [Fact]
    public async Task Partner_sees_the_form_data_and_the_request_note()
    {
        var app = await NeedsMoreInfo();
        var link = await EditLink.ExecuteAsync(app.Id, AdminId);

        var dto = await ForEdit.ExecuteAsync(app.Code.ToLowerInvariant(), link.Token);

        Assert.Equal(("NeedsMoreInfo", "Bổ sung giấy phép kinh doanh và số chỗ ô tô", "minh@example.vn", "Company"),
            (dto.Status, dto.InfoRequestNote, dto.Email, dto.BusinessType));
    }

    [Theory]
    [InlineData("wrong-token")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Wrong_token_gets_one_generic_message(string? token)
    {
        var app = await NeedsMoreInfo();
        await EditLink.ExecuteAsync(app.Id, AdminId);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => ForEdit.ExecuteAsync(app.Code, token));
        Assert.Contains("Link bổ sung hồ sơ không hợp lệ", ex.Message);
        await Assert.ThrowsAsync<ValidationException>(() => ForEdit.ExecuteAsync("PA-2026-9999", "x"));
    }

    [Fact]
    public async Task Expired_link_is_rejected()
    {
        var app = await NeedsMoreInfo();
        var link = await EditLink.ExecuteAsync(app.Id, AdminId);
        clockNow = Now.AddDays(8);

        await Assert.ThrowsAsync<ValidationException>(() => ForEdit.ExecuteAsync(app.Code, link.Token));
    }

    [Fact]
    public async Task Resubmit_updates_fields_moves_to_under_review_and_burns_the_link()
    {
        var app = await NeedsMoreInfo();
        var link = await EditLink.ExecuteAsync(app.Id, AdminId);
        store.Audits.Clear();

        var receipt = await Resubmit.ExecuteAsync(app.Code, link.Token, Valid() with { EstimatedCarSlots = 55, LotCount = 3 });

        Assert.Equal((app.Code, "UnderReview"), (receipt.Code, receipt.Status));
        Assert.Equal((PartnerApplicationStatus.UnderReview, 55, 3, null), (app.Status, app.EstimatedCarSlots, app.LotCount, app.EditTokenHash));
        var audit = Assert.Single(store.Audits);
        Assert.Equal(("PartnerApplication.InfoProvided", (int?)null), (audit.Action, audit.UserId));
        await Assert.ThrowsAsync<ValidationException>(() => Resubmit.ExecuteAsync(app.Code, link.Token, Valid()));
    }

    [Fact]
    public async Task Resubmit_checks_input_and_duplicates_with_other_open_applications()
    {
        var app = await NeedsMoreInfo();
        await Seed(Valid() with
        {
            Email = "other@example.vn", Phone = "0912345678", TaxCode = "0399999999",
        });
        var link = await EditLink.ExecuteAsync(app.Id, AdminId);

        await Assert.ThrowsAsync<ValidationException>(() => Resubmit.ExecuteAsync(app.Code, link.Token, Valid() with { ConsentAccepted = false }));
        await Assert.ThrowsAsync<ConflictException>(() => Resubmit.ExecuteAsync(app.Code, link.Token, Valid() with { TaxCode = "0399999999" }));
        Assert.Equal(PartnerApplicationStatus.NeedsMoreInfo, app.Status);
    }

    // ==================== T8 ====================

    private sealed class Users(string? status, bool down = false) : IUserServiceClient
    {
        public Task<AdminUserDto?> FindUserByIdAsync(int userId, CancellationToken ct)
            => down
                ? throw new DependencyUnavailableException("down")
                : Task.FromResult(status is null ? null : new AdminUserDto(userId, "Minh", "minh@example.vn", null, status, "NotSubmitted", ["LotOwner"], null));
        public Task<PagedResult<AdminUserSummaryDto>> ListUsersAsync(UserRoleType? role, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<OwnerAccountDto?> LockOwnerAsync(int ownerProfileId, string reason, DateTime? lockedUntilUtc, int performedByUserId, CancellationToken ct) => throw new NotSupportedException();
        public Task<OwnerAccountDto?> UnlockOwnerAsync(int ownerProfileId, string reason, int performedByUserId, CancellationToken ct) => throw new NotSupportedException();
    }

    private async Task<PartnerApplication> AccountIssued()
    {
        await Seed(Valid());
        var app = store.Applications[^1];
        app.StartReview(AdminId);
        app.IssueAccount(AdminId, BankAccount.Create("970436", "Vietcombank", "0071000123456", "CONG TY A"), 41, 7);
        store.Audits.Clear();
        return app;
    }

    [Fact]
    public async Task Activated_owner_moves_the_application_to_activated_on_detail()
    {
        var app = await AccountIssued();

        var dto = await new GetPartnerApplicationUseCase(store, new OwnerActivationSync(store, new Users("Active"))).ExecuteAsync(app.Id);

        Assert.Equal("Activated", dto.Status);
        var audit = Assert.Single(store.Audits);
        Assert.Equal(("PartnerApplication.Activated", (int?)null), (audit.Action, audit.UserId));
    }

    [Theory]
    [InlineData("PendingVerification", false)]
    [InlineData(null, false)]
    [InlineData("Active", true)]
    public async Task Pending_missing_or_unreachable_accounts_leave_the_status(string? status, bool down)
    {
        var app = await AccountIssued();

        var changed = await new OwnerActivationSync(store, new Users(status, down)).SyncAsync(app.Id, default);

        Assert.False(changed);
        Assert.Equal(PartnerApplicationStatus.AccountIssued, app.Status);
        Assert.Empty(store.Audits);
    }

    [Fact]
    public async Task List_syncs_account_issued_rows()
    {
        var app = await AccountIssued();
        var list = new ListPartnerApplicationsUseCase(new ListQueries(store), new OwnerActivationSync(store, new Users("Active")));

        var page = await list.ExecuteAsync(null, null, 1, 20);

        Assert.Equal("Activated", Assert.Single(page.Items).Status);
        Assert.Equal(PartnerApplicationStatus.Activated, app.Status);
    }

    /// <summary>Danh sách đọc từ bộ nhớ (cột tối thiểu cho đồng bộ).</summary>
    private sealed class ListQueries(InMemoryIntake store) : IPartnerApplicationQueries
    {
        public Task<PagedResult<PartnerApplicationSummaryDto>> ListAsync(PartnerApplicationStatus? status, string? search, int page, int pageSize, CancellationToken ct)
            => Task.FromResult(new PagedResult<PartnerApplicationSummaryDto>(store.Applications.Select(a => new PartnerApplicationSummaryDto(
                a.Id, a.Code, a.BusinessName, a.TaxCode, a.FullName, a.Email, a.Phone, a.LotCount, a.Status.ToString(), a.ReviewerAdminId,
                a.CreatedOwnerProfileId, a.CreatedAtUtc, a.UpdatedAtUtc)).ToList(), page, pageSize, store.Applications.Count));
        public Task<PartnerApplicationDetailDto?> GetDetailAsync(int id, CancellationToken ct) => store.GetDetailAsync(id, ct);
    }

    // ==================== Yêu cầu bổ sung theo mục + giấy tờ ====================

    /// <summary>Admin chỉ yêu cầu mã số thuế và CCCD mặt sau; link bổ sung đã cấp.</summary>
    private async Task<(PartnerApplication App, string Token)> NeedsOnlyTaxCodeAndIdBack()
    {
        await Seed(Valid());
        var app = store.Applications[^1];
        app.StartReview(AdminId);
        app.RequestInfo(AdminId, "Cập nhật MST và chụp lại CCCD mặt sau", ["taxCode", "doc:idCardBack"], Now.AddDays(7), new string('a', 64), Now);
        var link = await EditLink.ExecuteAsync(app.Id, AdminId);
        store.Audits.Clear();
        return (app, link.Token);
    }

    [Fact]
    public async Task Resubmit_only_allows_requested_items_and_requires_the_requested_documents()
    {
        var (app, token) = await NeedsOnlyTaxCodeAndIdBack();
        var idBack = new PartnerDocumentUpload(AdminService.Domain.Enums.PartnerDocumentType.IdCardBack, new UploadedFile("sau.png", Png.Length, Png));

        var tax = Valid() with { TaxCode = "0311111111" };
        var lot = await Assert.ThrowsAsync<ValidationException>(() => Resubmit.ExecuteAsync(app.Code, token, tax with { LotCount = 3 }, [idBack]));
        Assert.Contains("Số bãi", lot.Message);
        var email = await Assert.ThrowsAsync<ValidationException>(() => Resubmit.ExecuteAsync(app.Code, token, tax with { Email = "khac@example.vn" }, [idBack]));
        Assert.Contains("Email không được thay đổi", email.Message);
        var missing = await Assert.ThrowsAsync<ValidationException>(() => Resubmit.ExecuteAsync(app.Code, token, tax, []));
        Assert.Contains("ảnh CCCD mặt sau", missing.Message);
        var extra = await Assert.ThrowsAsync<ValidationException>(() => Resubmit.ExecuteAsync(app.Code, token, tax,
            [idBack, new PartnerDocumentUpload(AdminService.Domain.Enums.PartnerDocumentType.BusinessLicense, new UploadedFile("dk.pdf", Pdf.Length, Pdf))]));
        Assert.Contains("không nằm trong yêu cầu", extra.Message);
        Assert.Equal(AdminService.Domain.Enums.PartnerApplicationStatus.NeedsMoreInfo, app.Status);

        var receipt = await Resubmit.ExecuteAsync(app.Code, token, tax, [idBack]);

        Assert.Equal("UnderReview", receipt.Status);
        Assert.Equal(("0311111111", Now), (app.TaxCode, app.InfoProvidedAtUtc));
        Assert.Null(app.EditTokenHash);
        Assert.Contains("1 giấy tờ mới", Assert.Single(store.Audits).Reason);
    }

    [Fact]
    public async Task Edit_link_errors_carry_codes_for_invalid_expired_and_cancelled()
    {
        var (app, token) = await NeedsOnlyTaxCodeAndIdBack();

        var wrong = await Assert.ThrowsAsync<ValidationException>(() => ForEdit.ExecuteAsync(app.Code, "sai-token", default));
        Assert.Equal("edit_link_invalid", wrong.Code);

        var view = await ForEdit.ExecuteAsync(app.Code, token, default);
        Assert.Equal(["taxCode", "doc:idCardBack"], view.InfoRequestItems);

        clockNow = Now.AddDays(8);
        var expired = await Assert.ThrowsAsync<ValidationException>(() => ForEdit.ExecuteAsync(app.Code, token, default));
        Assert.Equal("edit_link_expired", expired.Code);

        clockNow = Now;
        app.Cancel(AdminId, "Đối tác rút", Now);
        var closed = await Assert.ThrowsAsync<ValidationException>(() => ForEdit.ExecuteAsync(app.Code, token, default));
        Assert.Equal("edit_link_invalid", closed.Code);   // hủy hồ sơ đã thu hồi link
    }
}
