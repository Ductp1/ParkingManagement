using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AdminService.Application.Features.LotContracts;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using AdminService.Domain.ValueObjects;
using AdminService.Infrastructure.Documents;
using AdminService.Infrastructure.Persistence.Seeding;
using AdminService.Infrastructure.Storage;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-116 (L0–L4, mục 14): hợp đồng theo từng bãi – lập sau KYB, xem trước, sửa, tải file, ký (kích hoạt bãi bên ParkingService), hủy
// (fake repository / queries / storage / ParkingService, renderer HTML thật, mẫu v3 thật).
public class LotContractTests
{
    private const int AdminId = 1;
    private const int KybId = 31;
    private const int LotId = 7;
    private const int OwnerProfileId = 9;
    private const string Number = "HD-SP-2026-0007";
    private static readonly DateTime Now = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);   // 09:00 20/10/2026 giờ VN
    private static readonly DateOnly Today = new(2026, 10, 20);

    private static readonly ContractTemplate Template = new()
    {
        Version = PartnerContractTemplates.V3Version, Name = PartnerContractTemplates.V3Name, Body = PartnerContractTemplates.V3Body,
        IsActive = true,
    };

    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!.SetValue(entity, id);
        return entity;
    }

    private static PartnerApplication Partner(bool withBank = true)
    {
        var app = WithId(PartnerApplication.Submit("PA-2026-0012", Now.AddDays(-30)), 12);
        app.FullName = "Lê Văn Phúc"; app.Email = "phuc.le@baixetanbinh.vn"; app.Phone = "0903456789";
        app.BusinessName = "Công ty TNHH Bãi xe Tân Bình"; app.TaxCode = "0987654321"; app.BusinessAddress = "125 Cộng Hòa, Tân Bình";
        app.LegalRepresentativeName = "Lê Văn Phúc"; app.LegalRepresentativePosition = "Giám đốc";
        app.MoveTo(PartnerApplicationStatus.UnderReview);
        if (withBank)
            app.IssueAccount(AdminId, new BankAccount("970436", "Vietcombank", "000000004521", "CONG TY TNHH BAI XE TAN BINH"), 41,
                OwnerProfileId);
        else
        {
            app.MoveTo(PartnerApplicationStatus.AccountIssued);
            app.CreatedOwnerProfileId = OwnerProfileId;
        }
        return app;
    }

    private static KybLotInfo Kyb(string kybStatus = "UnderReview", string lotStatus = "PendingApproval") => new(KybId, kybStatus, LotId,
        "Bãi xe Tân Bình 1", "125 Cộng Hòa", "TP. Hồ Chí Minh", "Tân Bình", 240, new TimeOnly(5, 0), new TimeOnly(23, 0), lotStatus,
        OwnerProfileId);

    private static CreateLotContractRequest Create(decimal? rate = 7.5m, int? kybId = KybId, int? months = 12)
        => new(kybId, rate, new DateOnly(2026, 11, 1), months);

    /// <summary>Hợp đồng đã lập (Id 5) với các phiên bản v1..vN, phiên bản cuối là Current.</summary>
    private static LotContract Contract(int versions = 1)
    {
        var lot = new LotSnapshot(LotId, KybId, OwnerProfileId, "Bãi xe Tân Bình 1", "125 Cộng Hòa", "TP. Hồ Chí Minh", "Tân Bình", 240,
            "05:00 – 23:00");
        var c = WithId(LotContract.Open(Number, lot, Partner(), AdminId), 5);
        for (var v = 1; v <= versions; v++)
        {
            var terms = new ContractTerms(7.5m, new DateOnly(2026, 11, 1), 12);
            var file = new StoredFile($"lot-contracts/hd-sp-2026-0007/contract-v{v}.html", HtmlContractRenderer.ContentType, "x");
            if (v == 1) c.AddFirstVersion(AdminId, terms, 3, file);
            else c.Revise(AdminId, terms, 3, file, "Đổi hoa hồng");
        }
        return c;
    }

    private sealed class Fixture
    {
        public FakeRepository Repository { get; }
        public FakeQueries Queries { get; }
        public FakeParking Parking { get; } = new();
        public FakeStorage Storage { get; } = new();
        public FixedTimeProvider Time { get; } = new(new DateTimeOffset(Now));
        public HtmlContractRenderer Renderer { get; } = new();

        public Fixture(LotContract? contract = null, PartnerApplication? partner = null)
        {
            Repository = new FakeRepository(contract, partner ?? Partner());
            Queries = new FakeQueries(Repository);
        }

        public CreateLotContractUseCase Create() => new(Repository, Queries, Parking, Renderer, Storage, Time);
        public PreviewNewLotContractUseCase PreviewNew() => new(Repository, Parking, Renderer, Time);
        public PreviewLotContractRevisionUseCase PreviewRevision() => new(Repository, Renderer, Time);
        public ReviseLotContractUseCase Revise() => new(Repository, Queries, Renderer, Storage, Time);
        public GetLotContractFileUseCase File() => new(Queries, Storage);
        public SignLotContractUseCase Sign() => new(Repository, Queries, Parking, Storage, Time);
        public CancelLotContractUseCase Cancel() => new(Repository, Queries);
    }

    private static JsonElement Json(string? json) => JsonDocument.Parse(json!).RootElement;
    private static string Text(byte[] content) => Encoding.UTF8.GetString(content);
    private static string Sha(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    // ==================== Điều khoản ====================

    [Fact]
    public void Terms_compute_effective_to_as_last_day_of_term()
        => Assert.Equal(new DateOnly(2027, 10, 31), ContractTerms.Create(7.5m, new DateOnly(2026, 11, 1), 12, Today).EffectiveTo);

    [Theory]
    [InlineData(null, "2026-11-01", 12, "Tỷ lệ hoa hồng")]
    [InlineData(0.0, "2026-11-01", 12, "Tỷ lệ hoa hồng")]
    [InlineData(100.01, "2026-11-01", 12, "Tỷ lệ hoa hồng")]
    [InlineData(7.555, "2026-11-01", 12, "Tỷ lệ hoa hồng")]
    [InlineData(7.5, "2026-10-19", 12, "Ngày hiệu lực không được trước hôm nay.")]
    [InlineData(7.5, null, 12, "Ngày hiệu lực không được để trống.")]
    [InlineData(7.5, "2026-11-01", 18, "Thời hạn hợp đồng")]
    public void Invalid_terms_are_rejected(double? rate, string? from, int? months, string message)
        => Assert.StartsWith(message, Assert.Throws<ValidationException>(() => ContractTerms.Create((decimal?)rate,
            from is null ? null : DateOnly.Parse(from), months, Today)).Message);

    // ==================== L1 tạo hợp đồng ====================

    [Fact]
    public async Task Create_snapshots_lot_and_partner_stores_v1_and_audits()
    {
        var fx = new Fixture();

        var dto = await fx.Create().ExecuteAsync(Create(), AdminId);

        Assert.Equal(Number, dto.ContractNumber);
        Assert.Equal("Pending", dto.Status);
        var c = Assert.Single(fx.Repository.Added);
        Assert.Equal((LotId, KybId, OwnerProfileId, 12), (c.ParkingLotId, c.KybApplicationId, c.OwnerProfileId, c.PartnerApplicationId));
        Assert.Equal(("Bãi xe Tân Bình 1", 240, "05:00 – 23:00"), (c.LotName, c.LotTotalSlots, c.LotOpeningHours));
        Assert.Equal(("Công ty TNHH Bãi xe Tân Bình", "0987654321", "000000004521"), (c.BusinessName, c.TaxCode, c.BankAccountNumber));
        var v1 = Assert.Single(c.Versions);
        Assert.Equal((1, 3, 7.5m, LotContractVersionStatus.Current), (v1.Version, v1.TemplateVersion, v1.CommissionRate, v1.Status));
        Assert.Equal("lot-contracts/hd-sp-2026-0007/contract-v1.html", v1.FileKey);
        Assert.Equal(Sha(fx.Storage.Files[v1.FileKey]), v1.FileSha256);
        var html = Text(fx.Storage.Files[v1.FileKey]);
        Assert.Contains("Số: HD-SP-2026-0007", html);
        Assert.DoesNotContain("BẢN XEM TRƯỚC", html);

        var audit = Assert.Single(fx.Repository.Audits);
        Assert.Equal(("LotContract.Created", "LotContract", Number, OwnerProfileId),
            (audit.Action, audit.EntityName, audit.EntityId, audit.OwnerProfileId));
        Assert.Null(audit.OldValuesJson);
        Assert.Equal(1, Json(audit.NewValuesJson).GetProperty("currentVersion").GetInt32());
    }

    [Fact]
    public async Task Recreating_after_cancel_gets_number_suffix()
    {
        var fx = new Fixture();
        fx.Repository.ExistingCount = 1;

        var dto = await fx.Create().ExecuteAsync(Create(), AdminId);

        Assert.Equal("HD-SP-2026-0007-2", dto.ContractNumber);
    }

    [Fact]
    public async Task Create_for_unknown_kyb_is_not_found()
    {
        var fx = new Fixture();
        fx.Parking.Kyb = null;

        await Assert.ThrowsAsync<NotFoundException>(() => fx.Create().ExecuteAsync(Create(), AdminId));
    }

    [Theory]
    [InlineData("Submitted", "PendingApproval", "đang thẩm định")]
    [InlineData("Approved", "Active", "đang thẩm định")]
    [InlineData("UnderReview", "Active", "không phải chờ duyệt")]
    public async Task Create_requires_kyb_under_review_and_pending_lot(string kybStatus, string lotStatus, string message)
    {
        var fx = new Fixture();
        fx.Parking.Kyb = Kyb(kybStatus, lotStatus);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => fx.Create().ExecuteAsync(Create(), AdminId));
        Assert.Contains(message, ex.Message);
        Assert.Empty(fx.Storage.Files);
    }

    [Fact]
    public async Task Create_when_lot_already_has_open_contract_is_conflict()
    {
        var fx = new Fixture();
        fx.Repository.HasOpen = true;

        await Assert.ThrowsAsync<ConflictException>(() => fx.Create().ExecuteAsync(Create(), AdminId));
    }

    [Fact]
    public async Task Create_when_owner_has_no_partner_application_is_conflict()
    {
        var fx = new Fixture();
        fx.Repository.Partner = null;

        var ex = await Assert.ThrowsAsync<ConflictException>(() => fx.Create().ExecuteAsync(Create(), AdminId));
        Assert.Contains("chưa có hồ sơ đối tác", ex.Message);
    }

    [Fact]
    public async Task Create_when_partner_has_no_payout_bank_is_conflict()
    {
        var fx = new Fixture(partner: Partner(withBank: false));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => fx.Create().ExecuteAsync(Create(), AdminId));
        Assert.Contains("tài khoản nhận thanh toán", ex.Message);
    }

    [Fact]
    public async Task Create_checks_state_before_terms()
    {
        var fx = new Fixture();
        fx.Parking.Kyb = Kyb("Submitted");

        await Assert.ThrowsAsync<ConflictException>(() => fx.Create().ExecuteAsync(Create(rate: 0), AdminId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task Create_without_kyb_id_is_bad_request(int? kybId)
        => await Assert.ThrowsAsync<ValidationException>(() => new Fixture().Create().ExecuteAsync(Create(kybId: kybId), AdminId));

    [Fact]
    public async Task Create_with_invalid_terms_is_bad_request_and_writes_nothing()
    {
        var fx = new Fixture();

        await Assert.ThrowsAsync<ValidationException>(() => fx.Create().ExecuteAsync(Create(months: 6), AdminId));
        Assert.Empty(fx.Storage.Files);
        Assert.Empty(fx.Repository.Added);
    }

    [Fact]
    public async Task Create_without_active_template_is_conflict()
    {
        var fx = new Fixture();
        fx.Repository.Template = null;

        Assert.Equal("Chưa có mẫu hợp đồng đang áp dụng, chưa thể sinh hợp đồng.",
            (await Assert.ThrowsAsync<ConflictException>(() => fx.Create().ExecuteAsync(Create(), AdminId))).Message);
    }

    [Fact]
    public async Task Create_deletes_file_when_saving_fails()
    {
        var fx = new Fixture();
        fx.Repository.Concurrency = true;

        await Assert.ThrowsAsync<ConflictException>(() => fx.Create().ExecuteAsync(Create(), AdminId));
        Assert.Empty(fx.Storage.Files);
        Assert.Equal(["lot-contracts/hd-sp-2026-0007/contract-v1.html"], fx.Storage.Deleted);
    }

    [Fact]
    public async Task Parking_service_unavailable_is_reported()
    {
        var fx = new Fixture();
        fx.Parking.Failure = new DependencyUnavailableException("down");

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => fx.Create().ExecuteAsync(Create(), AdminId));
    }

    // ==================== L1p / L2p xem trước ====================

    [Fact]
    public async Task Preview_new_renders_full_v3_contract_with_watermark_and_saves_nothing()
    {
        var fx = new Fixture();

        var file = await fx.PreviewNew().ExecuteAsync(Create(), AdminId);

        var html = Text(file.Content);
        Assert.Equal("HD-SP-2026-0007-v1-preview.html", file.FileName);
        Assert.DoesNotContain("{{", html);
        Assert.Contains("BẢN XEM TRƯỚC", html);
        Assert.Contains("<h1>Hợp đồng hợp tác khai thác bãi đỗ xe</h1>", html);
        Assert.Contains("TP. Hồ Chí Minh, ngày 20 tháng 10 năm 2026", html);
        Assert.Contains("(Phiên bản 1)", html);
        Assert.Contains("<h2>BÊN B: Công ty TNHH Bãi xe Tân Bình</h2>", html);
        Assert.Contains("Phí nền tảng: <strong>7,5%</strong>", html);
        Assert.Contains("<strong>12 (mười hai) tháng</strong>", html);
        Assert.Contains("đến hết ngày <strong>31/10/2027</strong>", html);
        Assert.Contains("<h2>ĐIỀU 12. ĐIỀU KHOẢN CHUNG</h2>", html);
        Assert.Contains("<td>Bãi xe Tân Bình 1</td><td>125 Cộng Hòa, Tân Bình, TP. Hồ Chí Minh</td><td>240 chỗ</td><td>05:00 – 23:00</td>", html);
        Assert.Contains("<td>000000004521</td>", html);
        Assert.Empty(fx.Storage.Files);
        Assert.Empty(fx.Repository.Added);
        Assert.Empty(fx.Repository.Audits);
    }

    [Fact]
    public async Task Preview_revision_shows_next_version_replacing_current()
    {
        var fx = new Fixture(Contract(versions: 1));

        var file = await fx.PreviewRevision().ExecuteAsync(5, new LotContractTermsRequest(6m, new DateOnly(2026, 12, 1), 24));

        var html = Text(file.Content);
        Assert.Equal("HD-SP-2026-0007-v2-preview.html", file.FileName);
        Assert.Contains("(Phiên bản 2 – thay thế phiên bản 1 ngày", html);
        Assert.Contains("<strong>6%</strong>", html);
        Assert.Contains("<strong>24 (hai mươi bốn) tháng</strong>", html);
    }

    // ==================== L2 sửa ====================

    [Fact]
    public async Task Revise_creates_next_version_and_supersedes_current()
    {
        var c = Contract(versions: 2);
        var fx = new Fixture(c);

        await fx.Revise().ExecuteAsync(5, new ReviseLotContractRequest(6m, new DateOnly(2026, 12, 1), 24, "Đối tác đề nghị giảm phí."), AdminId);

        Assert.Equal([LotContractVersionStatus.Superseded, LotContractVersionStatus.Superseded, LotContractVersionStatus.Current],
            c.Versions.OrderBy(v => v.Version).Select(v => v.Status));
        var v3 = c.CurrentVersion!;
        Assert.Equal((3, 6m, 24, "Đối tác đề nghị giảm phí."), (v3.Version, v3.CommissionRate, v3.TermMonths, v3.RevisionReason));
        Assert.True(fx.Storage.Files.ContainsKey("lot-contracts/hd-sp-2026-0007/contract-v3.html"));
        var audit = Assert.Single(fx.Repository.Audits);
        Assert.Equal(("LotContract.Revised", "Đối tác đề nghị giảm phí."), (audit.Action, audit.Reason));
        Assert.Equal(2, Json(audit.OldValuesJson).GetProperty("currentVersion").GetInt32());
        Assert.Equal(3, Json(audit.NewValuesJson).GetProperty("currentVersion").GetInt32());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Revise_requires_reason(string? reason)
    {
        var fx = new Fixture(Contract());

        Assert.Equal("Lý do sửa hợp đồng không được để trống.", (await Assert.ThrowsAsync<ValidationException>(() =>
            fx.Revise().ExecuteAsync(5, new ReviseLotContractRequest(6m, new DateOnly(2026, 12, 1), 12, reason), AdminId))).Message);
        Assert.Empty(fx.Storage.Files);
    }

    [Fact]
    public async Task Revise_signed_contract_is_conflict()
    {
        var c = Contract();
        c.Sign(AdminId, 1, new StoredFile("lot-contracts/hd-sp-2026-0007/signed-v1.pdf", "application/pdf", "x"), Now);

        await Assert.ThrowsAsync<ConflictException>(() => new Fixture(c).Revise()
            .ExecuteAsync(5, new ReviseLotContractRequest(6m, new DateOnly(2026, 12, 1), 12, "Lý do"), AdminId));
    }

    // ==================== L2f tải file ====================

    [Fact]
    public async Task Version_file_is_returned_when_hash_matches_and_refused_when_tampered()
    {
        var fx = new Fixture();
        await fx.Create().ExecuteAsync(Create(), AdminId);

        var file = await fx.File().ExecuteAsync(5, 1);
        Assert.Equal("HD-SP-2026-0007-v1.html", file.FileName);

        fx.Storage.Files["lot-contracts/hd-sp-2026-0007/contract-v1.html"] = "<p>đã sửa</p>"u8.ToArray();
        Assert.Contains("SHA-256 không khớp", (await Assert.ThrowsAsync<ConflictException>(() => fx.File().ExecuteAsync(5, 1))).Message);
    }

    [Fact]
    public async Task Missing_version_or_file_is_not_found()
    {
        var fx = new Fixture(Contract());
        await Assert.ThrowsAsync<NotFoundException>(() => fx.File().ExecuteAsync(5, 2));
        await Assert.ThrowsAsync<NotFoundException>(() => fx.File().ExecuteAsync(5, 1));
    }

    // ==================== L3 ký ====================

    private static readonly byte[] Pdf = "%PDF-1.7 signed"u8.ToArray();

    private static SignLotContractRequest SignRequest(int? version = 2, byte[]? content = null, long? length = null, string? note = null)
    {
        var bytes = content ?? Pdf;
        return new SignLotContractRequest(version, new UploadedFile("da-ky.pdf", length ?? bytes.Length, bytes), note);
    }

    [Fact]
    public async Task Sign_approves_kyb_then_signs_current_version()
    {
        var c = Contract(versions: 2);
        var fx = new Fixture(c);

        var dto = await fx.Sign().ExecuteAsync(5, SignRequest(note: "  Đạt khảo sát  "), AdminId);

        Assert.Equal("Signed", dto.Status);
        Assert.Equal([(KybId, Number, (string?)"Đạt khảo sát")], fx.Parking.Approvals);
        Assert.Equal("Đạt khảo sát", fx.Repository.Audits.Single().Reason);
        Assert.Equal(LotContractStatus.Signed, c.Status);
        Assert.Equal(LotContractVersionStatus.Signed, c.Versions.Single(v => v.Version == 2).Status);
        Assert.Equal(("lot-contracts/hd-sp-2026-0007/signed-v2.pdf", "application/pdf", Sha(Pdf)),
            (c.SignedFileKey, c.SignedFileContentType, c.SignedFileSha256));
        Assert.Equal(Now, c.SignedAtUtc);
        Assert.Equal(Pdf, fx.Storage.Files[c.SignedFileKey!]);
        Assert.Equal("LotContract.Signed", Assert.Single(fx.Repository.Audits).Action);
    }

    [Fact]
    public async Task Sign_with_too_long_note_is_bad_request_before_approving()
    {
        var fx = new Fixture(Contract(versions: 2));

        await Assert.ThrowsAsync<ValidationException>(() => fx.Sign().ExecuteAsync(5, SignRequest(note: new string('x', 1001)), AdminId));
        Assert.Empty(fx.Parking.Approvals);
        Assert.Empty(fx.Storage.Files);
    }

    [Fact]
    public async Task Kyb_approval_blocker_keeps_contract_pending_and_deletes_scan()
    {
        var c = Contract(versions: 2);
        var fx = new Fixture(c);
        fx.Parking.Failure = new ConflictException("Bãi trên 50 chỗ phải được khảo sát thực địa trước khi duyệt.");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => fx.Sign().ExecuteAsync(5, SignRequest(), AdminId));
        Assert.Contains("khảo sát thực địa", ex.Message);
        Assert.Equal(LotContractStatus.Pending, c.Status);
        Assert.Empty(fx.Storage.Files);
    }

    [Fact]
    public async Task Sign_old_version_is_conflict_without_activating()
    {
        var fx = new Fixture(Contract(versions: 2));

        Assert.Equal("Phiên bản hợp đồng hiện tại là v2, không phải v1. Vui lòng kiểm tra lại bản đã ký.",
            (await Assert.ThrowsAsync<ConflictException>(() => fx.Sign().ExecuteAsync(5, SignRequest(version: 1), AdminId))).Message);
        Assert.Empty(fx.Parking.Approvals);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task Sign_requires_confirmed_version(int? version)
        => await Assert.ThrowsAsync<ValidationException>(() => new Fixture(Contract()).Sign().ExecuteAsync(5, SignRequest(version), AdminId));

    [Fact]
    public async Task Sign_rejects_missing_unknown_or_large_scan()
    {
        var fx = new Fixture(Contract(versions: 2));
        await Assert.ThrowsAsync<ValidationException>(() => fx.Sign().ExecuteAsync(5, new SignLotContractRequest(2, null), AdminId));
        await Assert.ThrowsAsync<ValidationException>(() => fx.Sign().ExecuteAsync(5, SignRequest(content: "MZ exe"u8.ToArray()), AdminId));
        await Assert.ThrowsAsync<ValidationException>(() =>
            fx.Sign().ExecuteAsync(5, SignRequest(content: [], length: SignedScanRules.MaxBytes + 1L), AdminId));
        Assert.Empty(fx.Parking.Approvals);
    }

    [Fact]
    public async Task Parking_service_failure_deletes_scan_and_keeps_contract_pending()
    {
        var c = Contract(versions: 2);
        var fx = new Fixture(c);
        fx.Parking.Failure = new DependencyUnavailableException("down");

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => fx.Sign().ExecuteAsync(5, SignRequest(), AdminId));
        Assert.Equal(LotContractStatus.Pending, c.Status);
        Assert.Equal(["lot-contracts/hd-sp-2026-0007/signed-v2.pdf"], fx.Storage.Deleted);
        Assert.Empty(fx.Repository.Audits);
    }

    [Fact]
    public async Task Sign_save_failure_deletes_scan()
    {
        var fx = new Fixture(Contract(versions: 2));
        fx.Repository.Concurrency = true;

        await Assert.ThrowsAsync<ConflictException>(() => fx.Sign().ExecuteAsync(5, SignRequest(), AdminId));
        Assert.Empty(fx.Storage.Files);
    }

    // ==================== L4 hủy ====================

    [Fact]
    public async Task Cancel_pending_contract_supersedes_current_version()
    {
        var c = Contract();
        var fx = new Fixture(c);

        var dto = await fx.Cancel().ExecuteAsync(5, new LotContractReasonRequest("Chủ bãi rút bãi"), AdminId);

        Assert.Equal("Cancelled", dto.Status);
        Assert.Null(c.CurrentVersion);
        Assert.Equal(("LotContract.Cancelled", "Chủ bãi rút bãi"), (Assert.Single(fx.Repository.Audits).Action, fx.Repository.Audits[0].Reason));
    }

    [Fact]
    public async Task Cancel_requires_reason_and_pending_status()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            new Fixture(Contract()).Cancel().ExecuteAsync(5, new LotContractReasonRequest(" "), AdminId));

        var signed = Contract();
        signed.Sign(AdminId, 1, new StoredFile("lot-contracts/hd-sp-2026-0007/signed-v1.pdf", "application/pdf", "x"), Now);
        await Assert.ThrowsAsync<ConflictException>(() =>
            new Fixture(signed).Cancel().ExecuteAsync(5, new LotContractReasonRequest("Lý do"), AdminId));
    }

    [Fact]
    public async Task Unknown_contract_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => new Fixture().Cancel().ExecuteAsync(99, new LotContractReasonRequest("x"), AdminId));

    // ==================== L0 danh sách ====================

    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(1, 101, null)]
    [InlineData(1, 20, 0)]
    public async Task List_validates_paging_and_filters(int page, int pageSize, int? lotId)
        => await Assert.ThrowsAsync<ValidationException>(() =>
            new ListLotContractsUseCase(new FakeQueries(new FakeRepository(null, null))).ExecuteAsync(null, lotId, null, null, page, pageSize));

    // ==================== Renderer HTML + mẫu v3 + lưu file ====================

    [Fact]
    public void Renderer_turns_markup_into_html_blocks()
    {
        const string body = "^ Độc lập\n# Hợp đồng {{N}}\n## Điều 1\n### 1.a\n~ – Căn cứ\n- Ý {{N}}\n1.1. **Đậm** {{N}}\nĐoạn\n" +
                            "|| Tên: || {{N}}\n!! BÊN A !! BÊN B\n!! !!\n#| Cấp | Nội dung\n| 1 | Cảnh cáo\n---\nPhụ lục";

        var html = Text(new HtmlContractRenderer().Render(body, 3, new Dictionary<string, string> { ["N"] = "<b>**x**</b>" }, false).Content);

        Assert.Contains("<p class=\"center\">Độc lập</p>", html);
        Assert.Contains("<h1>Hợp đồng &lt;b&gt;**x**&lt;/b&gt;</h1>", html);   // giá trị có ** không thành chữ đậm
        Assert.Contains("<h2>Điều 1</h2>", html);
        Assert.Contains("<h3>1.a</h3>", html);
        Assert.Contains("<p class=\"italic\">– Căn cứ</p>", html);
        Assert.Contains("<ul>\n<li>Ý &lt;b&gt;**x**&lt;/b&gt;</li>\n</ul>", html);
        Assert.Contains("<p class=\"clause\">1.1. <strong>Đậm</strong> &lt;b&gt;**x**&lt;/b&gt;</p>", html);
        Assert.Contains("<table class=\"cols\">\n<tr><td>Tên:</td><td>&lt;b&gt;**x**&lt;/b&gt;</td></tr>\n</table>", html);
        Assert.Contains("<table class=\"sign\">\n<tr><td>BÊN A</td><td>BÊN B</td></tr>\n<tr><td></td><td></td></tr>\n</table>", html);
        Assert.Contains("<table class=\"grid\">\n<tr><th>Cấp</th><th>Nội dung</th></tr>\n<tr><td>1</td><td>Cảnh cáo</td></tr>\n</table>", html);
        Assert.Contains("<div class=\"page-break\"></div>\n<p>Phụ lục</p>", html);
        Assert.DoesNotContain("<b>", html);
        Assert.DoesNotContain("watermark\" aria-hidden", html);
        Assert.Contains("Ký nháy: Bên A", html);
        Assert.Contains("@page { size: A4", html);
    }

    [Fact]
    public void Renderer_adds_watermark_for_preview()
        => Assert.Contains("BẢN XEM TRƯỚC<br>KHÔNG CÓ GIÁ TRỊ PHÁP LÝ",
            Text(new HtmlContractRenderer().Render("Nội dung", 3, new Dictionary<string, string>(), true).Content));

    [Fact]
    public async Task Local_storage_saves_overwrites_reads_and_deletes()
    {
        var root = Path.Combine(Path.GetTempPath(), "pm-admin-storage-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalFileStorage(root);
            const string key = "lot-contracts/hd-sp-2026-0007/contract-v1.html";

            await storage.SaveAsync(key, [1, 2], CancellationToken.None);
            await storage.SaveAsync(key, [3], CancellationToken.None);
            Assert.Equal([3], await storage.ReadAsync(key, CancellationToken.None));
            Assert.Single(Directory.GetFiles(Path.Combine(root, "lot-contracts", "hd-sp-2026-0007")));

            await storage.DeleteAsync(key, CancellationToken.None);
            Assert.Null(await storage.ReadAsync(key, CancellationToken.None));
            await storage.DeleteAsync(key, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("lot-contracts/../../x")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("Lot/Upper.html")]
    [InlineData("a//b")]
    public async Task Local_storage_rejects_keys_outside_root(string key)
        => await Assert.ThrowsAsync<ArgumentException>(() => new LocalFileStorage(Path.GetTempPath()).ReadAsync(key, CancellationToken.None));

    // ==================== Fakes ====================

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRepository(LotContract? contract, PartnerApplication? partner) : ILotContractRepository
    {
        public PartnerApplication? Partner { get; set; } = partner;
        public ContractTemplate? Template { get; set; } = LotContractTests.Template;
        public bool HasOpen { get; set; }
        public int ExistingCount { get; set; }
        public bool Concurrency { get; set; }
        public LotContract? Contract { get; private set; } = contract;
        public List<LotContract> Added { get; } = [];
        public List<AuditLog> Audits { get; } = [];

        public Task<LotContract?> FindTrackedAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult(Contract?.Id == id ? Contract : null);

        public Task<PartnerApplication?> FindPartnerByOwnerProfileAsync(int ownerProfileId, CancellationToken cancellationToken)
            => Task.FromResult(Partner?.CreatedOwnerProfileId == ownerProfileId ? Partner : null);

        public Task<bool> HasOpenContractAsync(int parkingLotId, CancellationToken cancellationToken) => Task.FromResult(HasOpen);
        public Task<int> CountForLotAsync(int parkingLotId, CancellationToken cancellationToken) => Task.FromResult(ExistingCount);
        public Task<ContractTemplate?> GetActiveTemplateAsync(CancellationToken cancellationToken) => Task.FromResult(Template);

        public void Add(LotContract contract) => Added.Add(contract);

        public Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            if (Concurrency) throw new ConflictException("Hợp đồng bãi vừa được Admin khác cập nhật, vui lòng tải lại và thử lại.");
            Audits.Add(auditLog);
            foreach (var added in Added.Where(a => a.Id == 0)) Contract = WithId(added, 5);
            return Task.CompletedTask;
        }
    }

    /// <summary>Cổng đọc giả: dựng DTO từ hợp đồng đang có trong fake repository.</summary>
    private sealed class FakeQueries(FakeRepository repository) : ILotContractQueries
    {
        public Task<PagedResult<LotContractSummaryDto>> ListAsync(LotContractStatus? status, int? parkingLotId, int? ownerProfileId,
            string? search, int page, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult(new PagedResult<LotContractSummaryDto>([], page, pageSize, 0));

        public Task<LotContractDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken)
        {
            var c = repository.Contract;
            return Task.FromResult(c?.Id != id ? null : new LotContractDetailDto(c.Id, c.ContractNumber, c.Status.ToString(),
                c.ParkingLotId, c.KybApplicationId, c.LotName, c.LotAddress, c.LotCity, c.LotDistrict, c.LotTotalSlots, c.LotOpeningHours,
                c.OwnerProfileId, c.PartnerApplicationId, c.ApplicationCode, c.BusinessName, c.TaxCode, c.BusinessAddress,
                c.LegalRepresentativeName, c.LegalRepresentativePosition, c.Email, c.Phone,
                new PartnerBankAccountDto(c.BankBin, c.BankName, c.BankAccountNumber, c.BankAccountName), c.SignedFileContentType,
                c.SignedFileSha256, c.SignedAtUtc, c.CancelReason, c.CreatedByAdminId, c.CreatedAtUtc, c.UpdatedAtUtc, []));
        }

        public Task<LotContractVersionFileInfo?> GetVersionFileAsync(int id, int version, CancellationToken cancellationToken)
        {
            var c = repository.Contract;
            return Task.FromResult(c?.Id != id
                ? null
                : c.Versions.Where(v => v.Version == version)
                    .Select(v => new LotContractVersionFileInfo(c.ContractNumber, v.Version, v.FileKey, v.FileContentType, v.FileSha256))
                    .FirstOrDefault());
        }
    }

    private sealed class FakeParking : IParkingServiceClient
    {
        public KybLotInfo? Kyb { get; set; } = LotContractTests.Kyb();
        public Exception? Failure { get; set; }
        public List<(int KybId, string Number, string? Note)> Approvals { get; } = [];

        public Task<KybLotInfo?> GetKybAsync(int kybApplicationId, CancellationToken cancellationToken)
        {
            if (Failure is not null) throw Failure;
            return Task.FromResult(Kyb?.KybApplicationId == kybApplicationId ? Kyb : null);
        }

        public Task ApproveKybAsync(int kybApplicationId, string contractNumber, string? note, CancellationToken cancellationToken)
        {
            if (Failure is not null) throw Failure;
            Approvals.Add((kybApplicationId, contractNumber, note));
            return Task.CompletedTask;
        }
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
}
