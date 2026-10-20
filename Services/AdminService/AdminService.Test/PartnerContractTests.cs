using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using AdminService.Domain.ValueObjects;
using AdminService.Infrastructure.Documents;
using AdminService.Infrastructure.Storage;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-116 (A5–A10): hợp đồng tự sinh (HTML + SHA-256) và cấp tài khoản chủ bãi qua cổng UserService
// (fake repository / queries / storage / UserService, renderer HTML thật).
public class PartnerContractTests
{
    private const int AdminId = 1;
    private const int AppId = 12;
    private const string Number = "HD-SP-2026-0012";
    private static readonly DateTime Now = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);   // 09:00 20/10/2026 giờ VN
    private static readonly DateOnly Today = new(2026, 10, 20);

    private static readonly ContractTemplate Template = new()
    {
        Version = 1, Name = "Mẫu v1", IsActive = true,
        Body = "HỢP ĐỒNG SỐ {{ContractNumber}} (phiên bản {{Version}})\nBên B: {{BusinessName}} – MST {{TaxCode}}\n" +
               "Hoa hồng {{CommissionRate}}% từ {{EffectiveFrom}} đến {{EffectiveTo}}\nTài khoản {{BankAccountNumber}} {{Unknown}}",
    };

    private static PartnerContractTermsRequest Terms(decimal? rate = 8.5m, DateOnly? from = null, int? months = 12,
        string? bin = "970436", string? accountNumber = "0011000000099")
        => new(rate, from ?? new DateOnly(2026, 11, 1), months,
            new PartnerBankAccountRequest(bin, "Vietcombank", accountNumber, "CONG TY CP BAI XE THU DUC"));

    private static PartnerReviseContractRequest Revise(string? reason = "Đối tác đề nghị giảm hoa hồng.", decimal rate = 7m)
        => new(rate, new DateOnly(2026, 12, 1), 24, new PartnerBankAccountRequest("970436", "Vietcombank", "0011000000099", "CONG TY"),
            reason);

    private static PartnerApplication App(PartnerApplicationStatus status, int drafts = 0, int? ownerProfileId = null)
    {
        var app = PartnerApplication.Submit("PA-2026-0012", Now.AddDays(-5));
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!.SetValue(app, AppId);
        app.FullName = "Nguyễn Thị Lan"; app.Email = "lan.nguyen@thuducparking.vn"; app.Phone = "0903456789";
        app.BusinessName = "Công ty CP Bãi xe Thủ Đức"; app.TaxCode = "0316789012"; app.BusinessAddress = "216 Võ Văn Ngân";
        app.LegalRepresentativeName = "Nguyễn Thị Lan"; app.LegalRepresentativePosition = "Giám đốc";
        PartnerApplicationStatus[] path = status switch
        {
            PartnerApplicationStatus.UnderReview => [PartnerApplicationStatus.UnderReview],
            PartnerApplicationStatus.ContractPending => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.ContractPending],
            PartnerApplicationStatus.AccountIssued =>
                [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.ContractPending, PartnerApplicationStatus.AccountIssued],
            _ => [],
        };
        foreach (var step in path) app.MoveTo(step);
        for (var v = 1; v <= drafts; v++)
        {
            app.ContractNumber = Number;
            app.ContractDrafts.Add(new ContractDraft
            {
                Version = v, ContractNumber = Number, CommissionRate = 8.5m, EffectiveFrom = new DateOnly(2026, 11, 1),
                EffectiveTo = new DateOnly(2027, 10, 31), TermMonths = 12, BankBin = "970436", BankName = "Vietcombank",
                BankAccountNumber = "0011000000099", BankAccountName = "CONG TY CP BAI XE THU DUC",
                FileKey = $"partner-applications/12/contract-v{v}.html", FileContentType = HtmlContractRenderer.ContentType,
                Status = v == drafts ? ContractDraftStatus.Current : ContractDraftStatus.Superseded,
            });
        }
        app.CreatedOwnerProfileId = ownerProfileId;
        return app;
    }

    private sealed class Fixture(PartnerApplication? app, ContractTemplate? template = null)
    {
        public FakeRepository Repository { get; } = new(app, template ?? Template);
        public FakeStorage Storage { get; } = new();
        public FakeOwnerAccounts OwnerAccounts { get; } = new();
        public FakeQueries Queries { get; } = new(app);
        public FixedTimeProvider Time { get; } = new(new DateTimeOffset(Now));
        public HtmlContractRenderer Renderer { get; } = new();

        public PreviewPartnerContractUseCase Preview() => new(Repository, Renderer, Time);
        public SendPartnerContractUseCase Send() => new(Repository, Queries, Renderer, Storage, Time);
        public RevisePartnerContractUseCase Revise() => new(Repository, Queries, Renderer, Storage, Time);
        public GetPartnerContractFileUseCase File() => new(Queries, Storage);
        public IssueOwnerAccountUseCase Issue() => new(Repository, Queries, OwnerAccounts, Storage);
        public ResendOwnerActivationUseCase Resend() => new(Repository, Queries, OwnerAccounts);
    }

    private static JsonElement Json(string? json) => JsonDocument.Parse(json!).RootElement;
    private static string Text(byte[] content) => Encoding.UTF8.GetString(content);
    private static string Sha(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    // ==================== Điều khoản hợp đồng ====================

    [Fact]
    public void Terms_compute_effective_to_as_last_day_of_term()
    {
        var terms = ContractTerms.Create(8.5m, new DateOnly(2026, 11, 1), 12, " 970436 ", "Vietcombank", "0011000000099", " CONG TY ",
            Today);

        Assert.Equal(new DateOnly(2027, 10, 31), terms.EffectiveTo);
        Assert.Equal("970436", terms.BankBin);
        Assert.Equal("CONG TY", terms.BankAccountName);
    }

    [Fact]
    public void Terms_may_start_today() =>
        Assert.Equal(Today, ContractTerms.Create(10, Today, 36, "970436", "VCB", "123456", "A", Today).EffectiveFrom);

    [Theory]
    [InlineData(null, "2026-11-01", 12, "970436", "0011000000099", "Tỷ lệ hoa hồng")]
    [InlineData(0.0, "2026-11-01", 12, "970436", "0011000000099", "Tỷ lệ hoa hồng")]
    [InlineData(100.01, "2026-11-01", 12, "970436", "0011000000099", "Tỷ lệ hoa hồng")]
    [InlineData(8.555, "2026-11-01", 12, "970436", "0011000000099", "Tỷ lệ hoa hồng")]
    [InlineData(8.5, "2026-10-19", 12, "970436", "0011000000099", "Ngày hiệu lực không được trước hôm nay.")]
    [InlineData(8.5, null, 12, "970436", "0011000000099", "Ngày hiệu lực không được để trống.")]
    [InlineData(8.5, "2026-11-01", 18, "970436", "0011000000099", "Thời hạn hợp đồng")]
    [InlineData(8.5, "2026-11-01", null, "970436", "0011000000099", "Thời hạn hợp đồng")]
    [InlineData(8.5, "2026-11-01", 12, "97043", "0011000000099", "BIN ngân hàng")]
    [InlineData(8.5, "2026-11-01", 12, "970436", "12ab5678", "Số tài khoản")]
    [InlineData(8.5, "2026-11-01", 12, "970436", "12345", "Số tài khoản")]
    public void Invalid_terms_are_rejected(double? rate, string? from, int? months, string bin, string account, string message)
    {
        var ex = Assert.Throws<ValidationException>(() => ContractTerms.Create((decimal?)rate,
            from is null ? null : DateOnly.Parse(from), months, bin, "Vietcombank", account, "CONG TY", Today));
        Assert.StartsWith(message, ex.Message);
    }

    [Theory]
    [InlineData(null, "Tên ngân hàng không được để trống.")]
    [InlineData("   ", "Tên ngân hàng không được để trống.")]
    public void Bank_name_is_required(string? bankName, string message)
        => Assert.Equal(message, Assert.Throws<ValidationException>(
            () => ContractTerms.Create(8, Today, 12, "970436", bankName, "123456", "A", Today)).Message);

    // ==================== A5 contract-preview ====================

    [Fact]
    public async Task Preview_renders_next_version_without_saving_or_changing_state()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var fx = new Fixture(app);

        var file = await fx.Preview().ExecuteAsync(AppId, Terms());

        var html = Text(file.Content);
        Assert.Equal("text/html; charset=utf-8", file.ContentType);
        Assert.Equal("HD-SP-2026-0012-v1-preview.html", file.FileName);
        Assert.Contains("HỢP ĐỒNG SỐ HD-SP-2026-0012 (phiên bản 1)", html);
        Assert.Contains("Hoa hồng 8.5% từ 01/11/2026 đến 31/10/2027", html);
        Assert.Contains("{{Unknown}}", html);                 // placeholder lạ giữ nguyên để phát hiện mẫu sai
        Assert.Equal(PartnerApplicationStatus.UnderReview, app.Status);
        Assert.Null(app.ContractNumber);
        Assert.Empty(app.ContractDrafts);
        Assert.Empty(fx.Storage.Files);
        Assert.Empty(fx.Repository.Audits);
    }

    [Fact]
    public async Task Preview_of_pending_contract_uses_existing_number_and_next_version()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.ContractPending, drafts: 2));

        var file = await fx.Preview().ExecuteAsync(AppId, Terms());

        Assert.Equal("HD-SP-2026-0012-v3-preview.html", file.FileName);
        Assert.Contains("(phiên bản 3)", Text(file.Content));
    }

    [Fact]
    public async Task Preview_html_encodes_partner_data()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        app.BusinessName = "<script>alert(1)</script> & Co";

        var html = Text((await new Fixture(app).Preview().ExecuteAsync(AppId, Terms())).Content);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; &amp; Co", html);
    }

    [Theory]
    [InlineData(PartnerApplicationStatus.Submitted)]
    [InlineData(PartnerApplicationStatus.AccountIssued)]
    public async Task Preview_in_wrong_status_is_conflict(PartnerApplicationStatus status)
        => await Assert.ThrowsAsync<ConflictException>(() => new Fixture(App(status)).Preview().ExecuteAsync(AppId, Terms()));

    [Fact]
    public async Task Preview_without_active_template_is_conflict()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.UnderReview));
        fx.Repository.Template = null;

        var ex = await Assert.ThrowsAsync<ConflictException>(() => fx.Preview().ExecuteAsync(AppId, Terms()));
        Assert.Equal("Chưa có mẫu hợp đồng đang áp dụng, chưa thể sinh hợp đồng.", ex.Message);
    }

    [Fact]
    public async Task Preview_of_missing_application_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => new Fixture(null).Preview().ExecuteAsync(AppId, Terms()));

    // ==================== A6 send-contract (T5) ====================

    [Fact]
    public async Task Send_contract_assigns_number_stores_v1_with_hash_and_audits_once()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var fx = new Fixture(app);

        var dto = await fx.Send().ExecuteAsync(AppId, Terms(), AdminId);

        Assert.Equal("ContractPending", dto.Status);
        Assert.Equal(Number, app.ContractNumber);
        var draft = Assert.Single(app.ContractDrafts);
        Assert.Equal(1, draft.Version);
        Assert.Equal(ContractDraftStatus.Current, draft.Status);
        Assert.Equal(1, draft.TemplateVersion);
        Assert.Equal(8.5m, draft.CommissionRate);
        Assert.Equal(new DateOnly(2027, 10, 31), draft.EffectiveTo);
        Assert.Equal("lan.nguyen@thuducparking.vn", draft.SentToEmail);
        Assert.Equal(Now, draft.SentAtUtc);
        Assert.Equal(AdminId, draft.CreatedByAdminId);
        Assert.Equal("partner-applications/12/contract-v1.html", draft.FileKey);
        Assert.Equal(Sha(fx.Storage.Files[draft.FileKey]), draft.FileSha256);
        Assert.Contains("HỢP ĐỒNG SỐ HD-SP-2026-0012 (phiên bản 1)", Text(fx.Storage.Files[draft.FileKey]));

        var audit = Assert.Single(fx.Repository.Audits);
        Assert.Equal("PartnerApplication.ContractSent", audit.Action);
        Assert.Equal("UnderReview", Json(audit.OldValuesJson).GetProperty("status").GetString());
        Assert.Equal(Number, Json(audit.NewValuesJson).GetProperty("contractNumber").GetString());
        Assert.Equal(1, Json(audit.NewValuesJson).GetProperty("currentDraftVersion").GetInt32());
    }

    [Fact]
    public async Task Send_contract_twice_is_conflict_and_writes_no_file()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.ContractPending, drafts: 1));

        await Assert.ThrowsAsync<ConflictException>(() => fx.Send().ExecuteAsync(AppId, Terms(), AdminId));
        Assert.Empty(fx.Storage.Files);
        Assert.Empty(fx.Repository.Audits);
    }

    [Fact]
    public async Task Send_contract_checks_status_before_terms()
        => await Assert.ThrowsAsync<ConflictException>(() => new Fixture(App(PartnerApplicationStatus.Submitted)).Send()
            .ExecuteAsync(AppId, Terms(rate: 0), AdminId));

    [Fact]
    public async Task Send_contract_with_invalid_terms_is_bad_request_and_writes_nothing()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        var fx = new Fixture(app);

        await Assert.ThrowsAsync<ValidationException>(() => fx.Send().ExecuteAsync(AppId, Terms(months: 6), AdminId));
        Assert.Equal(PartnerApplicationStatus.UnderReview, app.Status);
        Assert.Empty(fx.Storage.Files);
    }

    [Fact]
    public async Task Send_contract_without_legal_representative_is_conflict()
    {
        var app = App(PartnerApplicationStatus.UnderReview);
        app.LegalRepresentativeName = " ";

        await Assert.ThrowsAsync<ConflictException>(() => new Fixture(app).Send().ExecuteAsync(AppId, Terms(), AdminId));
    }

    [Fact]
    public async Task Send_contract_deletes_file_when_saving_fails()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.UnderReview));
        fx.Repository.Concurrency = true;

        await Assert.ThrowsAsync<ConflictException>(() => fx.Send().ExecuteAsync(AppId, Terms(), AdminId));
        Assert.Empty(fx.Storage.Files);
        Assert.Equal(["partner-applications/12/contract-v1.html"], fx.Storage.Deleted);
    }

    [Theory]
    [InlineData(0, AdminId)]
    [InlineData(AppId, 0)]
    public async Task Send_contract_with_invalid_ids_is_bad_request(int id, int adminId)
        => await Assert.ThrowsAsync<ValidationException>(() => new Fixture(App(PartnerApplicationStatus.UnderReview)).Send()
            .ExecuteAsync(id, Terms(), adminId));

    // ==================== A7 revise-contract (T5b) ====================

    [Fact]
    public async Task Revise_contract_creates_next_version_and_supersedes_current()
    {
        var app = App(PartnerApplicationStatus.ContractPending, drafts: 2);
        var fx = new Fixture(app);

        var dto = await fx.Revise().ExecuteAsync(AppId, Revise(), AdminId);

        Assert.Equal("ContractPending", dto.Status);
        Assert.Equal(Number, app.ContractNumber);
        Assert.Equal([ContractDraftStatus.Superseded, ContractDraftStatus.Superseded, ContractDraftStatus.Current],
            app.ContractDrafts.OrderBy(d => d.Version).Select(d => d.Status));
        var current = app.CurrentDraft!;
        Assert.Equal(3, current.Version);
        Assert.Equal(Number, current.ContractNumber);
        Assert.Equal(7m, current.CommissionRate);
        Assert.Equal(24, current.TermMonths);
        Assert.Equal("Đối tác đề nghị giảm hoa hồng.", current.RevisionReason);
        Assert.True(fx.Storage.Files.ContainsKey("partner-applications/12/contract-v3.html"));

        var audit = Assert.Single(fx.Repository.Audits);
        Assert.Equal("PartnerApplication.ContractRevised", audit.Action);
        Assert.Equal("Đối tác đề nghị giảm hoa hồng.", audit.Reason);
        Assert.Equal(2, Json(audit.OldValuesJson).GetProperty("currentDraftVersion").GetInt32());
        Assert.Equal(3, Json(audit.NewValuesJson).GetProperty("currentDraftVersion").GetInt32());
    }

    [Fact]
    public async Task Revise_pending_contract_without_versions_starts_at_v1()
    {
        var app = App(PartnerApplicationStatus.ContractPending);
        app.ContractNumber = "HD-SP-2026-0004";

        await new Fixture(app).Revise().ExecuteAsync(AppId, Revise(), AdminId);

        Assert.Equal(1, app.CurrentDraft!.Version);
        Assert.Equal("HD-SP-2026-0004", app.CurrentDraft.ContractNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Revise_contract_requires_reason(string? reason)
    {
        var fx = new Fixture(App(PartnerApplicationStatus.ContractPending, drafts: 1));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => fx.Revise().ExecuteAsync(AppId, Revise(reason), AdminId));
        Assert.Equal("Lý do sửa hợp đồng không được để trống.", ex.Message);
        Assert.Empty(fx.Storage.Files);
    }

    [Fact]
    public async Task Revise_contract_before_sending_is_conflict()
        => await Assert.ThrowsAsync<ConflictException>(() => new Fixture(App(PartnerApplicationStatus.UnderReview)).Revise()
            .ExecuteAsync(AppId, Revise(), AdminId));

    // ==================== A8 contract file ====================

    [Fact]
    public async Task Contract_file_is_returned_when_hash_matches()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.UnderReview));
        await fx.Send().ExecuteAsync(AppId, Terms(), AdminId);

        var file = await fx.File().ExecuteAsync(AppId, 1);

        Assert.Equal("HD-SP-2026-0012-v1.html", file.FileName);
        Assert.Equal(HtmlContractRenderer.ContentType, file.ContentType);
        Assert.Equal(fx.Storage.Files["partner-applications/12/contract-v1.html"], file.Content);
    }

    [Fact]
    public async Task Tampered_contract_file_is_conflict()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.UnderReview));
        await fx.Send().ExecuteAsync(AppId, Terms(), AdminId);
        fx.Storage.Files["partner-applications/12/contract-v1.html"] = "<p>đã bị sửa</p>"u8.ToArray();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => fx.File().ExecuteAsync(AppId, 1));
        Assert.Contains("SHA-256 không khớp", ex.Message);
    }

    [Fact]
    public async Task Missing_contract_version_or_file_is_not_found()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.ContractPending, drafts: 1));   // có phiên bản nhưng chưa có file

        await Assert.ThrowsAsync<NotFoundException>(() => fx.File().ExecuteAsync(AppId, 2));
        await Assert.ThrowsAsync<NotFoundException>(() => fx.File().ExecuteAsync(AppId, 1));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(AppId, 0)]
    public async Task Contract_file_with_invalid_ids_is_bad_request(int id, int version)
        => await Assert.ThrowsAsync<ValidationException>(() => new Fixture(null).File().ExecuteAsync(id, version));

    // ==================== A9 issue-account (T6) ====================

    private static readonly byte[] Pdf = "%PDF-1.7 signed"u8.ToArray();

    private static IssueOwnerAccountRequest Issue(int? version = 2, byte[]? content = null, long? length = null)
    {
        var bytes = content ?? Pdf;
        return new IssueOwnerAccountRequest(version, new UploadedFile("hop-dong-da-ky.pdf", length ?? bytes.Length, bytes));
    }

    [Fact]
    public async Task Issue_account_calls_user_service_then_signs_current_version_and_creates_owner_contract()
    {
        var app = App(PartnerApplicationStatus.ContractPending, drafts: 2);
        var fx = new Fixture(app);

        var dto = await fx.Issue().ExecuteAsync(AppId, Issue(), AdminId);

        Assert.Equal("AccountIssued", dto.Status);
        var call = Assert.Single(fx.OwnerAccounts.Issued);
        Assert.Equal("partner-application-12", call.Key);
        Assert.Equal(0.085m, call.Command.CommissionRateOverride);
        Assert.Equal(Number, call.Command.ContractNumber);
        Assert.Equal("0316789012", call.Command.TaxCode);
        Assert.Equal("0011000000099", call.Command.BankAccountNumber);

        Assert.Equal(FakeOwnerAccounts.UserId, app.CreatedUserId);
        Assert.Equal(FakeOwnerAccounts.OwnerProfileId, app.CreatedOwnerProfileId);
        Assert.Equal(ContractDraftStatus.Signed, app.ContractDrafts.Single(d => d.Version == 2).Status);
        Assert.Null(app.CurrentDraft);

        var contract = Assert.Single(fx.Repository.OwnerContracts);
        Assert.Equal(FakeOwnerAccounts.OwnerProfileId, contract.OwnerProfileId);
        Assert.Equal(Number, contract.ContractNumber);
        Assert.Equal(2, contract.DraftVersion);
        Assert.Equal(8.5m, contract.CommissionRate);
        Assert.Equal(OwnerContractStatus.Active, contract.Status);
        Assert.Equal("partner-applications/12/signed-v2.pdf", contract.SignedFileKey);
        Assert.Equal("application/pdf", contract.SignedFileContentType);
        Assert.Equal(Sha(Pdf), contract.SignedFileSha256);
        Assert.Equal(Pdf, fx.Storage.Files[contract.SignedFileKey]);

        var audit = Assert.Single(fx.Repository.Audits);
        Assert.Equal("PartnerApplication.AccountIssued", audit.Action);
        Assert.Equal(FakeOwnerAccounts.OwnerProfileId, audit.OwnerProfileId);
        Assert.Equal("ContractPending", Json(audit.OldValuesJson).GetProperty("status").GetString());
        Assert.Equal("AccountIssued", Json(audit.NewValuesJson).GetProperty("status").GetString());
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1 }, "image/png", ".png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1 }, "image/jpeg", ".jpg")]
    public async Task Issue_account_accepts_image_scans(byte[] content, string contentType, string extension)
    {
        var fx = new Fixture(App(PartnerApplicationStatus.ContractPending, drafts: 2));

        await fx.Issue().ExecuteAsync(AppId, Issue(content: content), AdminId);

        var contract = Assert.Single(fx.Repository.OwnerContracts);
        Assert.Equal(contentType, contract.SignedFileContentType);
        Assert.Equal($"partner-applications/12/signed-v2{extension}", contract.SignedFileKey);
    }

    [Fact]
    public async Task Issue_account_for_old_version_is_conflict_without_calling_user_service()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.ContractPending, drafts: 2));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => fx.Issue().ExecuteAsync(AppId, Issue(version: 1), AdminId));
        Assert.Equal("Phiên bản hợp đồng hiện tại là v2, không phải v1. Vui lòng kiểm tra lại bản đã ký.", ex.Message);
        Assert.Empty(fx.OwnerAccounts.Issued);
    }

    [Fact]
    public async Task Issue_account_without_current_version_is_conflict()
        => await Assert.ThrowsAsync<ConflictException>(() => new Fixture(App(PartnerApplicationStatus.ContractPending)).Issue()
            .ExecuteAsync(AppId, Issue(version: 1), AdminId));

    [Theory]
    [InlineData(PartnerApplicationStatus.UnderReview)]
    [InlineData(PartnerApplicationStatus.AccountIssued)]
    public async Task Issue_account_in_wrong_status_is_conflict(PartnerApplicationStatus status)
    {
        var fx = new Fixture(App(status, drafts: 2));

        await Assert.ThrowsAsync<ConflictException>(() => fx.Issue().ExecuteAsync(AppId, Issue(), AdminId));
        Assert.Empty(fx.OwnerAccounts.Issued);
    }

    [Fact]
    public async Task Issue_account_requires_confirmed_version()
        => Assert.Equal("Vui lòng xác nhận phiên bản hợp đồng đã ký (confirmedDraftVersion).",
            (await Assert.ThrowsAsync<ValidationException>(() => new Fixture(App(PartnerApplicationStatus.ContractPending, drafts: 2))
                .Issue().ExecuteAsync(AppId, Issue(version: null), AdminId))).Message);

    [Fact]
    public async Task Issue_account_requires_signed_scan()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.ContractPending, drafts: 2));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => fx.Issue().ExecuteAsync(AppId, new IssueOwnerAccountRequest(2, null), AdminId));
        Assert.Equal("Vui lòng tải lên bản scan hợp đồng đã ký.", ex.Message);
        Assert.Empty(fx.OwnerAccounts.Issued);
    }

    [Fact]
    public async Task Issue_account_rejects_unknown_file_type()
        => Assert.Equal("Bản scan hợp đồng phải là file PDF, JPG hoặc PNG.",
            (await Assert.ThrowsAsync<ValidationException>(() => new Fixture(App(PartnerApplicationStatus.ContractPending, drafts: 2))
                .Issue().ExecuteAsync(AppId, Issue(content: "MZ fake exe"u8.ToArray()), AdminId))).Message);

    [Fact]
    public async Task Issue_account_rejects_scan_over_10_mb()
        => Assert.Equal("Bản scan hợp đồng tối đa 10 MB.",
            (await Assert.ThrowsAsync<ValidationException>(() => new Fixture(App(PartnerApplicationStatus.ContractPending, drafts: 2))
                .Issue().ExecuteAsync(AppId, Issue(content: [], length: SignedScanRules.MaxBytes + 1L), AdminId))).Message);

    [Fact]
    public async Task User_service_unavailable_keeps_application_unchanged()
    {
        var app = App(PartnerApplicationStatus.ContractPending, drafts: 2);
        var fx = new Fixture(app);
        fx.OwnerAccounts.Failure = new DependencyUnavailableException("UserService hiện không phản hồi, vui lòng thử lại sau.");

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => fx.Issue().ExecuteAsync(AppId, Issue(), AdminId));
        Assert.Equal(PartnerApplicationStatus.ContractPending, app.Status);
        Assert.Equal(ContractDraftStatus.Current, app.CurrentDraft!.Status);
        Assert.Null(app.CreatedOwnerProfileId);
        Assert.Empty(fx.Storage.Files);
        Assert.Empty(fx.Repository.Audits);
        Assert.Empty(fx.Repository.OwnerContracts);
    }

    [Fact]
    public async Task Duplicate_owner_in_user_service_is_conflict()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.ContractPending, drafts: 2));
        fx.OwnerAccounts.Failure = new ConflictException("trùng");

        await Assert.ThrowsAsync<ConflictException>(() => fx.Issue().ExecuteAsync(AppId, Issue(), AdminId));
        Assert.Empty(fx.Repository.Audits);
    }

    [Fact]
    public async Task Issue_account_deletes_scan_when_saving_fails()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.ContractPending, drafts: 2));
        fx.Repository.Concurrency = true;

        await Assert.ThrowsAsync<ConflictException>(() => fx.Issue().ExecuteAsync(AppId, Issue(), AdminId));
        Assert.Equal(["partner-applications/12/signed-v2.pdf"], fx.Storage.Deleted);
        Assert.Empty(fx.Storage.Files);
    }

    // ==================== A10 resend-activation (T7) ====================

    [Fact]
    public async Task Resend_activation_calls_user_service_and_audits()
    {
        var app = App(PartnerApplicationStatus.AccountIssued, ownerProfileId: 7);
        var fx = new Fixture(app);

        var dto = await fx.Resend().ExecuteAsync(AppId, AdminId);

        Assert.Equal("AccountIssued", dto.Status);
        Assert.Equal([7], fx.OwnerAccounts.Resent);
        var audit = Assert.Single(fx.Repository.Audits);
        Assert.Equal("PartnerApplication.ActivationResent", audit.Action);
        Assert.Equal(7, audit.OwnerProfileId);
    }

    [Fact]
    public async Task Resend_activation_without_owner_account_is_conflict()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.AccountIssued));

        await Assert.ThrowsAsync<ConflictException>(() => fx.Resend().ExecuteAsync(AppId, AdminId));
        Assert.Empty(fx.OwnerAccounts.Resent);
    }

    [Fact]
    public async Task Resend_activation_before_account_issued_is_conflict()
        => await Assert.ThrowsAsync<ConflictException>(() => new Fixture(App(PartnerApplicationStatus.ContractPending, ownerProfileId: 7))
            .Resend().ExecuteAsync(AppId, AdminId));

    [Fact]
    public async Task Resend_activation_failure_writes_no_audit()
    {
        var fx = new Fixture(App(PartnerApplicationStatus.AccountIssued, ownerProfileId: 7));
        fx.OwnerAccounts.Failure = new DependencyUnavailableException("down");

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => fx.Resend().ExecuteAsync(AppId, AdminId));
        Assert.Empty(fx.Repository.Audits);
    }

    // ==================== Infrastructure: renderer HTML + lưu file local ====================

    [Fact]
    public void Renderer_wraps_lines_in_utf8_html_document()
    {
        var file = new HtmlContractRenderer().Render("Dòng 1 {{A}}\n\nDòng 3", 2, new Dictionary<string, string> { ["A"] = "x<y" });

        var html = Text(file.Content);
        Assert.Equal(".html", file.FileExtension);
        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("<meta charset=\"utf-8\">", html);
        Assert.Contains("<p>Dòng 1 x&lt;y</p>", html);
        Assert.Contains("<div class=\"gap\"></div>", html);
        Assert.Contains("<p>Dòng 3</p>", html);
        Assert.Contains("Mẫu hợp đồng v2", html);
        Assert.Contains("@page { size: A4", html);
    }

    [Fact]
    public void Renderer_turns_markup_lines_into_headings_lists_tables_and_page_breaks()
    {
        const string body = "^ Độc lập\n# Hợp đồng {{N}}\n## Điều 1\n### 1.1\n- Ý 1\n- Ý {{N}}\nĐoạn\n|| Tên: || {{N}}\n|| ||\n||Chỉ một ô\n!! BÊN A !! BÊN B\n!! !!\n---\nPhụ lục";

        var html = Text(new HtmlContractRenderer().Render(body, 2, new Dictionary<string, string> { ["N"] = "<b>" }).Content);

        Assert.Contains("<p class=\"center\">Độc lập</p>", html);
        Assert.Contains("<h1>Hợp đồng &lt;b&gt;</h1>", html);
        Assert.Contains("<h2>Điều 1</h2>", html);
        Assert.Contains("<h3>1.1</h3>", html);
        Assert.Contains("<ul>\n<li>Ý 1</li>\n<li>Ý &lt;b&gt;</li>\n</ul>\n<p>Đoạn</p>", html);
        Assert.Contains("<table class=\"cols\">\n<tr><td>Tên:</td><td>&lt;b&gt;</td></tr>\n<tr><td></td><td></td></tr>\n<tr><td>Chỉ một ô</td><td></td></tr>\n</table>", html);
        Assert.Contains("<table class=\"sign\">\n<tr><td>BÊN A</td><td>BÊN B</td></tr>\n<tr><td></td><td></td></tr>\n</table>", html);
        Assert.Contains("<div class=\"page-break\"></div>\n<p>Phụ lục</p>", html);
        Assert.DoesNotContain("<b>", html);
    }

    [Fact]
    public async Task Seeded_template_v2_renders_a_full_contract_without_missing_placeholders()
    {
        var template = new ContractTemplate
        {
            Version = AdminService.Infrastructure.Persistence.Seeding.PartnerContractTemplates.V2Version,
            Name = AdminService.Infrastructure.Persistence.Seeding.PartnerContractTemplates.V2Name,
            Body = AdminService.Infrastructure.Persistence.Seeding.PartnerContractTemplates.V2Body,
            IsActive = true,
        };
        var fx = new Fixture(App(PartnerApplicationStatus.UnderReview), template);

        var html = Text((await fx.Preview().ExecuteAsync(AppId, Terms())).Content);

        Assert.DoesNotContain("{{", html);
        Assert.Contains("<h1>Hợp đồng hợp tác kinh doanh dịch vụ đỗ xe</h1>", html);
        Assert.Contains("Số: HD-SP-2026-0012 · Phiên bản: 1", html);
        Assert.Contains("<h2>Điều 15. Điều khoản chung</h2>", html);
        Assert.Contains("8.5% Doanh thu giao dịch", html);
        Assert.Contains("từ 01/11/2026 đến 31/10/2027", html);
        Assert.Contains("<td>Công ty CP Bãi xe Thủ Đức</td>", html);
        Assert.Contains("<h1>Phụ lục 01 – Thông tin hợp tác</h1>", html);
        Assert.Contains("Mẫu hợp đồng v2", html);
    }

    [Fact]
    public async Task Local_storage_saves_overwrites_reads_and_deletes()
    {
        var root = Path.Combine(Path.GetTempPath(), "pm-admin-storage-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalFileStorage(root);
            const string key = "partner-applications/12/contract-v1.html";

            await storage.SaveAsync(key, [1, 2], CancellationToken.None);
            await storage.SaveAsync(key, [3], CancellationToken.None);
            Assert.Equal([3], await storage.ReadAsync(key, CancellationToken.None));
            Assert.Single(Directory.GetFiles(Path.Combine(root, "partner-applications", "12")));   // không còn file tạm

            await storage.DeleteAsync(key, CancellationToken.None);
            Assert.Null(await storage.ReadAsync(key, CancellationToken.None));
            await storage.DeleteAsync(key, CancellationToken.None);   // xóa lần 2 không lỗi
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("partner-applications/../../x")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("Partner/Upper.html")]
    [InlineData("a//b")]
    public async Task Local_storage_rejects_keys_outside_root(string key)
        => await Assert.ThrowsAsync<ArgumentException>(
            () => new LocalFileStorage(Path.GetTempPath()).ReadAsync(key, CancellationToken.None));

    // ==================== Fakes ====================

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRepository(PartnerApplication? app, ContractTemplate? template) : IPartnerApplicationRepository
    {
        public ContractTemplate? Template { get; set; } = template;
        public bool Concurrency { get; set; }
        public List<AuditLog> Audits { get; } = [];
        public List<OwnerContract> OwnerContracts { get; } = [];
        private readonly List<OwnerContract> _pending = [];

        public Task<PartnerApplication?> FindTrackedAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult(app?.Id == id ? app : null);

        public Task<ContractTemplate?> GetActiveTemplateAsync(CancellationToken cancellationToken) => Task.FromResult(Template);

        public void AddOwnerContract(OwnerContract contract) => _pending.Add(contract);

        public Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            if (Concurrency) throw new ConflictException("Hồ sơ đối tác vừa được Admin khác cập nhật, vui lòng tải lại và thử lại.");
            Audits.Add(auditLog);
            OwnerContracts.AddRange(_pending);
            _pending.Clear();
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
                app.ContractNumber, app.CreatedUserId, app.CreatedOwnerProfileId, app.CreatedAtUtc, app.UpdatedAtUtc,
                PartnerApplication.NextStatuses(app.Status).Select(s => s.ToString()).ToList(), []));

        public Task<ContractDraftFileInfo?> GetContractFileAsync(int id, int version, CancellationToken cancellationToken)
            => Task.FromResult(app?.Id != id
                ? null
                : app.ContractDrafts.Where(d => d.Version == version)
                    .Select(d => new ContractDraftFileInfo(d.ContractNumber, d.Version, d.FileKey, d.FileContentType, d.FileSha256))
                    .FirstOrDefault());
    }

    private sealed class FakeStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public List<string> Deleted { get; } = [];

        public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken)
        {
            Files[key] = content;
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken)
            => Task.FromResult(Files.TryGetValue(key, out var content) ? content : null);

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            Files.Remove(key);
            Deleted.Add(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOwnerAccounts : IOwnerAccountClient
    {
        public const int UserId = 41;
        public const int OwnerProfileId = 9;

        public Exception? Failure { get; set; }
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
