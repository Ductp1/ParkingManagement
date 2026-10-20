using System.Reflection;
using System.Text.Json;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using AdminService.Domain.ValueObjects;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-116 (A9 / A10): cấp tài khoản chủ bãi (không ký hợp đồng – hợp đồng theo từng bãi) và gửi lại link kích hoạt
// (fake repository / queries / UserService).
public class PartnerAccountTests
{
    private const int AdminId = 1;
    private const int AppId = 12;
    private static readonly DateTime Now = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);

    private static readonly PartnerBankAccountRequest Bank = new(" 970436 ", "Vietcombank", "0011000000099", " CONG TY CP BAI XE THU DUC ");

    private static PartnerApplication App(PartnerApplicationStatus status, int? ownerProfileId = null)
    {
        var app = PartnerApplication.Submit("PA-2026-0012", Now.AddDays(-5));
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!.SetValue(app, AppId);
        app.FullName = "Nguyễn Thị Lan"; app.Email = "lan.nguyen@thuducparking.vn"; app.Phone = "0903456789";
        app.BusinessName = "Công ty CP Bãi xe Thủ Đức"; app.TaxCode = "0316789012"; app.BusinessAddress = "216 Võ Văn Ngân";
        app.LegalRepresentativeName = "Nguyễn Thị Lan";
        PartnerApplicationStatus[] path = status switch
        {
            PartnerApplicationStatus.UnderReview => [PartnerApplicationStatus.UnderReview],
            PartnerApplicationStatus.NeedsMoreInfo => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.NeedsMoreInfo],
            PartnerApplicationStatus.AccountIssued => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.AccountIssued],
            PartnerApplicationStatus.Activated =>
                [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.AccountIssued, PartnerApplicationStatus.Activated],
            _ => [],
        };
        foreach (var step in path) app.MoveTo(step);
        app.CreatedOwnerProfileId = ownerProfileId;
        return app;
    }

    private static JsonElement Json(string? json) => JsonDocument.Parse(json!).RootElement;

    // ==================== Tài khoản ngân hàng ====================

    [Fact]
    public void Bank_account_is_trimmed()
    {
        var bank = BankAccount.Create(" 970436 ", " Vietcombank ", " 0011000000099 ", " CONG TY ");
        Assert.Equal(new BankAccount("970436", "Vietcombank", "0011000000099", "CONG TY"), bank);
    }

    [Theory]
    [InlineData("97043", "Vietcombank", "0011000000099", "A", "BIN ngân hàng phải gồm 6 chữ số.")]
    [InlineData("970436", " ", "0011000000099", "A", "Tên ngân hàng không được để trống.")]
    [InlineData("970436", "Vietcombank", "12ab5678", "A", "Số tài khoản phải gồm 6–30 chữ số.")]
    [InlineData("970436", "Vietcombank", "12345", "A", "Số tài khoản phải gồm 6–30 chữ số.")]
    [InlineData("970436", "Vietcombank", "0011000000099", null, "Tên chủ tài khoản không được để trống.")]
    public void Invalid_bank_account_is_rejected(string? bin, string? name, string? number, string? holder, string message)
        => Assert.Equal(message, Assert.Throws<ValidationException>(() => BankAccount.Create(bin, name, number, holder)).Message);

    // ==================== A9 issue-account (T6) ====================

    [Fact]
    public async Task Issue_account_calls_user_service_and_stores_payout_bank()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var repo = new FakeRepository(app);
        var users = new FakeOwnerAccounts();

        var dto = await new IssueOwnerAccountUseCase(repo, new FakeQueries(app), users)
            .ExecuteAsync(AppId, new IssueOwnerAccountRequest(Bank), AdminId);

        Assert.Equal("AccountIssued", dto.Status);
        Assert.Equal(new PartnerBankAccountDto("970436", "Vietcombank", "0011000000099", "CONG TY CP BAI XE THU DUC"), dto.PayoutBank);
        var call = Assert.Single(users.Issued);
        Assert.Equal("partner-application-12", call.Key);
        Assert.Equal(("0316789012", "970436", "0011000000099"), (call.Command.TaxCode, call.Command.BankBin, call.Command.BankAccountNumber));
        Assert.Equal((FakeOwnerAccounts.UserId, FakeOwnerAccounts.OwnerProfileId), (app.CreatedUserId, app.CreatedOwnerProfileId));
        Assert.Equal(AdminId, app.ReviewerAdminId);

        var audit = Assert.Single(repo.Audits);
        Assert.Equal("PartnerApplication.AccountIssued", audit.Action);
        Assert.Equal(FakeOwnerAccounts.OwnerProfileId, audit.OwnerProfileId);
        Assert.Equal("UnderReview", Json(audit.OldValuesJson).GetProperty("status").GetString());
        Assert.Equal("0011000000099", Json(audit.NewValuesJson).GetProperty("payoutBankAccount").GetString());
    }

    [Theory]
    [InlineData(PartnerApplicationStatus.Submitted)]
    [InlineData(PartnerApplicationStatus.NeedsMoreInfo)]
    [InlineData(PartnerApplicationStatus.AccountIssued)]
    public async Task Issue_account_in_wrong_status_is_conflict_before_bank_check(PartnerApplicationStatus status)
    {
        var app = App(status);
        var users = new FakeOwnerAccounts();

        await Assert.ThrowsAsync<ConflictException>(() => new IssueOwnerAccountUseCase(new FakeRepository(app), new FakeQueries(app), users)
            .ExecuteAsync(AppId, new IssueOwnerAccountRequest(null), AdminId));
        Assert.Empty(users.Issued);
    }

    [Fact]
    public async Task Issue_account_without_bank_is_bad_request_without_calling_user_service()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var users = new FakeOwnerAccounts();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => new IssueOwnerAccountUseCase(new FakeRepository(app),
            new FakeQueries(app), users).ExecuteAsync(AppId, new IssueOwnerAccountRequest(null), AdminId));
        Assert.Equal("BIN ngân hàng phải gồm 6 chữ số.", ex.Message);
        Assert.Empty(users.Issued);
    }

    [Fact]
    public async Task User_service_unavailable_keeps_application_unchanged()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var repo = new FakeRepository(app);
        var users = new FakeOwnerAccounts { Failure = new DependencyUnavailableException("down") };

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => new IssueOwnerAccountUseCase(repo, new FakeQueries(app), users)
            .ExecuteAsync(AppId, new IssueOwnerAccountRequest(Bank), AdminId));
        Assert.Equal(PartnerApplicationStatus.UnderReview, app.Status);
        Assert.Null(app.PayoutBank);
        Assert.Empty(repo.Audits);
    }

    [Fact]
    public async Task Duplicate_owner_in_user_service_is_conflict()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var users = new FakeOwnerAccounts { Failure = new ConflictException("trùng") };

        await Assert.ThrowsAsync<ConflictException>(() => new IssueOwnerAccountUseCase(new FakeRepository(app), new FakeQueries(app), users)
            .ExecuteAsync(AppId, new IssueOwnerAccountRequest(Bank), AdminId));
        Assert.Equal(PartnerApplicationStatus.UnderReview, app.Status);
    }

    // ==================== A10 resend-activation (T7) ====================

    [Fact]
    public async Task Resend_activation_calls_user_service_and_audits()
    {
        var app = App(PartnerApplicationStatus.AccountIssued, ownerProfileId: 7);
        var repo = new FakeRepository(app);
        var users = new FakeOwnerAccounts();

        var dto = await new ResendOwnerActivationUseCase(repo, new FakeQueries(app), users).ExecuteAsync(AppId, AdminId);

        Assert.Equal("AccountIssued", dto.Status);
        Assert.Equal([7], users.Resent);
        var audit = Assert.Single(repo.Audits);
        Assert.Equal("PartnerApplication.ActivationResent", audit.Action);
        Assert.Equal(7, audit.OwnerProfileId);
    }

    [Fact]
    public async Task Resend_activation_without_owner_account_is_conflict()
    {
        var app = App(PartnerApplicationStatus.AccountIssued);
        var users = new FakeOwnerAccounts();

        await Assert.ThrowsAsync<ConflictException>(() =>
            new ResendOwnerActivationUseCase(new FakeRepository(app), new FakeQueries(app), users).ExecuteAsync(AppId, AdminId));
        Assert.Empty(users.Resent);
    }

    [Theory]
    [InlineData(PartnerApplicationStatus.UnderReview)]
    [InlineData(PartnerApplicationStatus.Activated)]
    public async Task Resend_activation_outside_account_issued_is_conflict(PartnerApplicationStatus status)
        => await Assert.ThrowsAsync<ConflictException>(() => new ResendOwnerActivationUseCase(new FakeRepository(App(status, 7)),
            new FakeQueries(App(status, 7)), new FakeOwnerAccounts()).ExecuteAsync(AppId, AdminId));

    [Fact]
    public async Task Resend_activation_failure_writes_no_audit()
    {
        var app = App(PartnerApplicationStatus.AccountIssued, ownerProfileId: 7);
        var repo = new FakeRepository(app);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => new ResendOwnerActivationUseCase(repo, new FakeQueries(app),
            new FakeOwnerAccounts { Failure = new DependencyUnavailableException("down") }).ExecuteAsync(AppId, AdminId));
        Assert.Empty(repo.Audits);
    }

    // ==================== Fakes ====================

    private sealed class FakeRepository(PartnerApplication? app) : IPartnerApplicationRepository
    {
        public List<AuditLog> Audits { get; } = [];

        public Task<PartnerApplication?> FindTrackedAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult(app?.Id == id ? app : null);

        public Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            Audits.Add(auditLog);
            return Task.CompletedTask;
        }
    }

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
                app.PayoutBank is { } b ? new PartnerBankAccountDto(b.Bin, b.BankName, b.AccountNumber, b.AccountName) : null,
                app.CreatedUserId, app.CreatedOwnerProfileId, app.CreatedAtUtc, app.UpdatedAtUtc,
                PartnerApplication.NextStatuses(app.Status).Select(s => s.ToString()).ToList()));
    }

    private sealed class FakeOwnerAccounts : IOwnerAccountClient
    {
        public const int UserId = 41;
        public const int OwnerProfileId = 9;

        public Exception? Failure { get; init; }
        public List<(IssueOwnerAccountCommand Command, string Key)> Issued { get; } = [];
        public List<int> Resent { get; } = [];

        public Task<IssuedOwnerAccountDto> IssueOwnerAccountAsync(IssueOwnerAccountCommand command, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            if (Failure is not null) throw Failure;
            Issued.Add((command, idempotencyKey));
            return Task.FromResult(new IssuedOwnerAccountDto(UserId, OwnerProfileId));
        }

        public Task ResendActivationAsync(int ownerProfileId, CancellationToken cancellationToken)
        {
            if (Failure is not null) throw Failure;
            Resent.Add(ownerProfileId);
            return Task.CompletedTask;
        }
    }
}
