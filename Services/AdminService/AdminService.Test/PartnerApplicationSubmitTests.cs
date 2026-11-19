using System.Reflection;
using System.Text.Json;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-116 T1: hồ sơ đối tác mới – form public (không đăng nhập) và Admin nhập thay (nhận qua điện thoại).
public class PartnerApplicationSubmitTests
{
    private const int AdminId = 1;
    private static readonly DateTime Now = new(2026, 11, 13, 2, 0, 0, DateTimeKind.Utc);

    internal static SubmitPartnerApplicationRequest Valid() => new(
        FullName: " Trần Minh ", Email: " Minh@Example.VN ", Phone: "+84 901.234.567", Position: "Giám đốc",
        BusinessType: PartnerBusinessType.Company, BusinessName: "Công ty TNHH Bãi xe Thủ Đức", TaxCode: "0312 345 678",
        BusinessAddress: "12 Võ Văn Ngân, TP. Thủ Đức", LegalRepresentativeName: "Trần Minh", LegalRepresentativePosition: "Giám đốc",
        LotCount: 2, PrimaryLotAddress: "45 Kha Vạn Cân, TP. Thủ Đức", EstimatedCarSlots: 40, EstimatedMotorbikeSlots: 200,
        OperatingHours: "06:00–22:00", ConsentAccepted: true);

    private static AdminCreatePartnerApplicationRequest AdminBody(SubmitPartnerApplicationRequest r, string? note) => new(
        r.FullName, r.Email, r.Phone, r.Position, r.BusinessType, r.BusinessName, r.TaxCode, r.BusinessAddress, r.LegalRepresentativeName,
        r.LegalRepresentativePosition, r.LotCount, r.PrimaryLotAddress, r.EstimatedCarSlots, r.EstimatedMotorbikeSlots, r.OperatingHours,
        r.ConsentAccepted, note);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    /// <summary>Repository + cổng đọc trong bộ nhớ: gán Id / mã như DB, giữ audit log.</summary>
    internal sealed class InMemoryIntake : IPartnerApplicationIntakeRepository, IPartnerApplicationQueries, IPartnerApplicationRepository,
        IPartnerRegistrationRepository
    {
        public List<PartnerApplication> Applications { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public List<PartnerApplicationDocument> Documents { get; } = [];

        // ----- Form public (multipart + giấy tờ, không bắt buộc đăng nhập) – cùng kho với Admin nhập hồ sơ -----

        public Task<bool> HasOpenApplicationAsync(int applicantUserId, string taxCode, string email, CancellationToken cancellationToken)
            => Task.FromResult(Applications.Any(a => !a.IsTerminal
                                                     && (a.ApplicantUserId == applicantUserId || a.TaxCode == taxCode || a.Email == email)));

        public Task<IReadOnlyList<MyPartnerApplicationDto>> ListByApplicantAsync(int applicantUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string> AddWithNewCodeAsync(PartnerApplication application, IReadOnlyList<PartnerApplicationDocument> documents, int year,
            CancellationToken cancellationToken, Func<PartnerApplication, AuditLog>? buildAuditLog = null)
        {
            var last = Applications.Select(a => a.Code).Where(c => c.StartsWith(PartnerApplicationCode.Prefix(year))).Max();
            application.Code = PartnerApplicationCode.Next(year, last);
            typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(application, Applications.Count + 100);
            Applications.Add(application);
            Documents.AddRange(documents);
            if (buildAuditLog is not null) Audits.Add(buildAuditLog(application));
            return Task.FromResult(application.Code);
        }

        public Task<OpenApplicationMatch?> FindOpenDuplicateAsync(string email, string phone, string taxCode, CancellationToken ct,
            int? excludeId = null)
        {
            var open = Applications.FirstOrDefault(a => !a.IsTerminal && a.Id != excludeId
                                                        && (a.Email == email || a.Phone == phone || a.TaxCode == taxCode));
            return Task.FromResult(open is null ? null : new OpenApplicationMatch(open.Code, open.Email == email, open.Phone == phone, open.TaxCode == taxCode));
        }

        public Task<PartnerApplication?> FindByCodeTrackedAsync(string code, CancellationToken ct)
            => Task.FromResult(Applications.FirstOrDefault(a => a.Code == code));

        public Task<PartnerApplication?> FindTrackedAsync(int id, CancellationToken ct) => Task.FromResult(Applications.FirstOrDefault(a => a.Id == id));

        public Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken ct)
        {
            Audits.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task AddSubmittedAsync(PartnerApplication application, int year, Func<PartnerApplication, AuditLog> buildAuditLog, CancellationToken ct)
        {
            var last = Applications.Select(a => a.Code).Where(c => c.StartsWith(PartnerApplicationCode.Prefix(year))).Max();
            application.Code = PartnerApplicationCode.Next(year, last);
            typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(application, Applications.Count + 100);
            Applications.Add(application);
            Audits.Add(buildAuditLog(application));
            return Task.CompletedTask;
        }

        public Task<PagedResult<PartnerApplicationSummaryDto>> ListAsync(PartnerApplicationStatus? status, string? search, int page,
            int pageSize, CancellationToken ct) => throw new NotSupportedException();

        public Task<PartnerApplicationDetailDto?> GetDetailAsync(int id, CancellationToken ct)
        {
            var a = Applications.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(a is null ? null : new PartnerApplicationDetailDto(
                a.Id, a.Code, a.Status.ToString(), a.FullName, a.Email, a.Phone, a.Position, a.BusinessType.ToString(), a.BusinessName,
                a.TaxCode, a.BusinessAddress, a.LegalRepresentativeName, a.LegalRepresentativePosition, a.BusinessLicenseUrl, a.LotCount,
                a.PrimaryLotAddress, a.EstimatedCarSlots, a.EstimatedMotorbikeSlots, a.OperatingHours, a.ConsentAcceptedAtUtc,
                a.ReviewerAdminId, a.InfoRequestNote, a.RejectReason, a.CancelReason, null, a.CreatedUserId, a.CreatedOwnerProfileId,
                a.CreatedAtUtc, a.UpdatedAtUtc, PartnerApplication.NextStatuses(a.Status).Select(s => s.ToString()).ToList()));
        }
    }

    private static CreatePartnerApplicationUseCase Create(InMemoryIntake store) => new(store, store, new FixedClock(Now));

    // ==================== Kiểm tra + chuẩn hóa ====================

    [Fact]
    public void Input_is_trimmed_and_normalized()
    {
        var input = PartnerApplicationInput.From(Valid());

        Assert.Equal("Trần Minh", input.FullName);
        Assert.Equal("minh@example.vn", input.Email);
        Assert.Equal("0901234567", input.Phone);
        Assert.Equal("0312345678", input.TaxCode);
        Assert.Equal(PartnerBusinessType.Company, input.BusinessType);
    }

    [Theory]
    [InlineData("0901234567", "0901234567")]
    [InlineData("84901234567", "0901234567")]
    [InlineData("+84 28 3822 1234", "02838221234")]
    [InlineData("090-123-4567", "0901234567")]
    public void Phone_is_normalized(string raw, string expected) => Assert.Equal(expected, PartnerApplicationInput.NormalizePhone(raw));

    [Theory]
    [InlineData("0312345678")]
    [InlineData("0312345678-001")]
    [InlineData("079201001234")]
    public void Valid_tax_codes_are_accepted(string taxCode)
        => Assert.Equal(taxCode, PartnerApplicationInput.From(Valid() with { TaxCode = taxCode }).TaxCode);

    [Fact]
    public void All_problems_are_reported_in_one_400()
    {
        var ex = Assert.Throws<ValidationException>(() => PartnerApplicationInput.From(Valid() with
        {
            Email = "not-an-email", Phone = "12345", TaxCode = "123", LotCount = 0, BusinessType = null, ConsentAccepted = false,
            EstimatedCarSlots = -1,
        }));

        Assert.Contains("Email không hợp lệ", ex.Message);
        Assert.Contains("Số điện thoại không hợp lệ", ex.Message);
        Assert.Contains("Mã số thuế", ex.Message);
        Assert.Contains("Số bãi đỗ phải từ 1 đến 100", ex.Message);
        Assert.Contains("Loại hình kinh doanh", ex.Message);
        Assert.Contains("Nghị định 13/2023", ex.Message);
        Assert.Contains("Số chỗ ô tô", ex.Message);
    }

    [Fact]
    public void Missing_required_text_is_rejected()
    {
        var ex = Assert.Throws<ValidationException>(() => PartnerApplicationInput.From(Valid() with
        {
            FullName = " ", BusinessName = null, BusinessAddress = "abc", LegalRepresentativeName = "", PrimaryLotAddress = null,
        }));

        Assert.Contains("Họ và tên người liên hệ", ex.Message);
        Assert.Contains("Tên doanh nghiệp / hộ kinh doanh", ex.Message);
        Assert.Contains("Địa chỉ kinh doanh", ex.Message);
        Assert.Contains("Người đại diện theo pháp luật", ex.Message);
        Assert.Contains("Địa chỉ bãi chính", ex.Message);
    }

    [Theory]
    [InlineData(null, "PA-2026-0001")]
    [InlineData("PA-2026-0012", "PA-2026-0013")]
    [InlineData("PA-2025-0099", "PA-2026-0001")]
    [InlineData("PA-2026-xx", "PA-2026-0001")]
    public void Code_continues_the_yearly_sequence(string? last, string expected) => Assert.Equal(expected, PartnerApplicationCode.Next(2026, last));

    // ==================== Hồ sơ mới: mã, trùng lặp, kiểm tra (dùng chung cho mọi cách tạo hồ sơ) ====================
    // Form public (đối tác tự nộp, multipart + giấy tờ, không cần đăng nhập) có test riêng ở PartnerRegistrationTests
    // (Anonymous_submit_*, Submit_writes_audit_log_*). Các test dưới chạy qua đường Admin nhập hồ sơ – cùng PartnerApplicationInput.

    [Fact]
    public async Task New_application_is_submitted_with_normalized_data()
    {
        var store = new InMemoryIntake();

        await Create(store).ExecuteAsync(AdminBody(Valid(), null), AdminId);

        var app = Assert.Single(store.Applications);
        Assert.Equal((PartnerApplicationStatus.Submitted, "minh@example.vn", "0901234567", Now),
            (app.Status, app.Email, app.Phone, app.ConsentAcceptedAtUtc));
        Assert.Equal(2, app.LotCount);
        Assert.Null(app.ApplicantUserId);                           // hồ sơ không gắn tài khoản: duyệt xong mới cấp tài khoản mới
        Assert.Equal("PA-2026-0001", JsonDocument.Parse(Assert.Single(store.Audits).NewValuesJson!).RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Second_application_gets_next_code()
    {
        var store = new InMemoryIntake();
        await Create(store).ExecuteAsync(AdminBody(Valid(), null), AdminId);

        var second = await Create(store).ExecuteAsync(
            AdminBody(Valid() with { Email = "b@example.vn", Phone = "0912345678", TaxCode = "0312345679" }, null), AdminId);

        Assert.Equal("PA-2026-0002", second.Code);
    }

    [Fact]
    public async Task Closed_application_does_not_block_a_new_one()
    {
        var store = new InMemoryIntake();
        await Create(store).ExecuteAsync(AdminBody(Valid(), null), AdminId);
        var first = store.Applications[0];
        first.StartReview(AdminId);
        first.Reject(AdminId, "Thiếu giấy phép kinh doanh");

        var dto = await Create(store).ExecuteAsync(AdminBody(Valid(), null), AdminId);

        Assert.Equal("PA-2026-0002", dto.Code);
    }

    [Fact]
    public async Task New_application_is_validated_before_touching_the_store()
    {
        var store = new InMemoryIntake();

        await Assert.ThrowsAsync<ValidationException>(() =>
            Create(store).ExecuteAsync(AdminBody(Valid() with { ConsentAccepted = false }, null), AdminId));

        Assert.Empty(store.Applications);
        Assert.Empty(store.Audits);
    }

    // ==================== Admin nhập thay ====================

    [Fact]
    public async Task Admin_intake_creates_submitted_application_with_admin_and_note_in_audit()
    {
        var store = new InMemoryIntake();

        var dto = await Create(store).ExecuteAsync(AdminBody(Valid(), "  Gọi hotline 13/11, anh Minh  "), AdminId);

        Assert.Equal(("PA-2026-0001", "Submitted"), (dto.Code, dto.Status));
        Assert.Contains("UnderReview", dto.NextStatuses);
        var audit = Assert.Single(store.Audits);
        Assert.Equal((AdminId, "PartnerApplication.CreatedByAdmin"), (audit.UserId, audit.Action));
        Assert.Equal("Admin nhập hồ sơ thay đối tác: Gọi hotline 13/11, anh Minh", audit.Reason);
    }

    [Fact]
    public async Task Admin_intake_duplicate_names_the_open_application()
    {
        var store = new InMemoryIntake();
        await Create(store).ExecuteAsync(AdminBody(Valid(), null), AdminId);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Create(store).ExecuteAsync(AdminBody(Valid() with { Phone = "0987654321", TaxCode = "0399999999" }, null), AdminId));

        Assert.Contains("PA-2026-0001", ex.Message);
        Assert.Contains("email", ex.Message);
    }

    [Fact]
    public async Task Admin_intake_rejects_long_note_and_missing_admin()
    {
        var store = new InMemoryIntake();

        await Assert.ThrowsAsync<ValidationException>(() => Create(store).ExecuteAsync(AdminBody(Valid(), new string('a', 1001)), AdminId));
        await Assert.ThrowsAsync<ValidationException>(() => Create(store).ExecuteAsync(AdminBody(Valid(), null), 0));
        Assert.Empty(store.Applications);
    }
}
