using System.Reflection;
using System.Text;
using AdminService.Application.Features.ContractTemplates;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using AdminService.Domain.ValueObjects;
using AdminService.Infrastructure.Documents;
using AdminService.Infrastructure.Persistence.Seeding;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-116 v11: mẫu hợp đồng = loại hợp đồng có tên (T0–T6) – tạo mới, nhập DOCX (AI giả), sửa form, đổi tên, áp dụng nhiều mẫu, thôi áp dụng,
// chuyển markup → form, hủy bản nháp (fake repository / queries / storage / analyzer; renderer, composer, DOCX thật).
public class ContractTemplateTests
{
    private const int AdminId = 1;
    private static readonly DateTime Now = new(2026, 10, 23, 2, 0, 0, DateTimeKind.Utc);

    private static readonly PlatformPartyDto NewPlatform = new("CÔNG TY CP SMART PARKING VIỆT NAM", "10 Lê Lợi, Quận 1, TP. Hồ Chí Minh",
        "0312345678", "028 1234 5678", "legal@smartparking.vn", "TRẦN VĂN AN", "Giám đốc điều hành");

    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!.SetValue(entity, id);
        return entity;
    }

    /// <summary>Mẫu chuẩn dạng form Active (Id 6, đọc từ v4) + tùy chọn bản nháp Id 7 chép từ mẫu đó.</summary>
    private static Fixture Setup(bool withDraft = false)
    {
        var content = ContractMarkupParser.Parse(PartnerContractTemplates.V4Body);
        var standard = WithId(ContractTemplate.CreateDraft(6, PartnerContractTemplates.StandardName, ContractTemplateKind.Structured, AdminId), 6);
        standard.UpdateDraft(standard.Name, null, ContractTemplateKind.Structured, ContractTemplateComposer.ToMarkup(content),
            TemplateContentJson.Serialize(content), PartnerContractTemplates.V4Platform);
        standard.Activate(Now.AddDays(-10));
        standard.RowVersion = [6];
        var fx = new Fixture(standard);
        if (withDraft)
        {
            var draft = WithId(ContractTemplate.CreateDraft(7, "Hợp đồng chuẩn 2027", standard, AdminId), 7);
            draft.RowVersion = [7];
            fx.Repository.Templates.Add(draft);
        }
        return fx;
    }

    private static TemplateContentDto ContentDto(Action<List<TemplateArticleDto>>? edit = null)
    {
        var dto = ContractTemplateMapping.ToDto(ContractMarkupParser.Parse(PartnerContractTemplates.V4Body));
        var articles = dto.Articles!.ToList();
        edit?.Invoke(articles);
        return dto with { Articles = articles };
    }

    private static UpdateContractTemplateDraftRequest Update(TemplateContentDto? content = null, PlatformPartyDto? platform = null,
        string? rowVersion = "Bw==", string name = "Hợp đồng chuẩn 2027", string kind = "Structured")
        => new(name, "Đổi Bên A", kind, content ?? ContentDto(), null, platform ?? NewPlatform, rowVersion);

    private static string Text(byte[] content) => Encoding.UTF8.GetString(content);

    // ==================== T2 tạo mới ====================

    [Fact]
    public async Task Create_blank_template_is_structured_draft_with_default_articles()
    {
        var fx = Setup();

        var draft = await fx.Create().ExecuteAsync(new CreateContractTemplateRequest("Hợp đồng ngắn hạn", "Blank", null), AdminId);

        Assert.Equal(("Hợp đồng ngắn hạn", "Structured", "Draft", 7), (draft.Name, draft.Kind, draft.Status, draft.Version));
        Assert.Equal(3, draft.Content!.Articles!.Count);
        Assert.Single(draft.Content.Articles, a => a.IsSpecialTerms);
        Assert.Equal(PartnerContractTemplates.V4Platform.Name, draft.Platform.Name);   // Bên A lấy từ mẫu Active mới nhất
        Assert.Contains("{{CommissionRate}}", draft.Body);
        Assert.Equal("ContractTemplate.Created", Assert.Single(fx.Repository.Audits).Action);
    }

    [Fact]
    public async Task Duplicate_copies_content_and_remembers_source()
    {
        var fx = Setup();

        var draft = await fx.Create().ExecuteAsync(new CreateContractTemplateRequest("Bản sao", "Duplicate", 6), AdminId);

        Assert.Equal((6, PartnerContractTemplates.StandardName), (draft.SourceTemplateId!.Value, draft.SourceTemplateName));
        Assert.Equal(13, draft.Content!.Articles!.Count);
    }

    [Theory]
    [InlineData(" ", "Blank", "Tên mẫu không được để trống.")]
    [InlineData("x", "Weird", "mode phải là Blank hoặc Duplicate.")]
    [InlineData("x", "Duplicate", "Chọn mẫu nguồn (sourceTemplateId) để sao chép.")]
    public async Task Create_with_invalid_input_is_rejected(string name, string mode, string message)
        => Assert.Equal(message, (await Assert.ThrowsAsync<ValidationException>(() =>
            Setup().Create().ExecuteAsync(new CreateContractTemplateRequest(name, mode, null), AdminId))).Message);

    [Fact]
    public async Task Create_with_name_of_active_template_is_conflict()
        => Assert.EndsWith("đã được dùng cho mẫu khác.", (await Assert.ThrowsAsync<ConflictException>(() => Setup().Create()
            .ExecuteAsync(new CreateContractTemplateRequest($"  {PartnerContractTemplates.StandardName.ToUpperInvariant()} ", "Blank", null),
                AdminId))).Message);

    // ==================== T2i nhập DOCX ====================

    private static byte[] SampleDocx() => DocxTestFiles.Create(
        new[]
        {
            new[] { "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM" },
            new[] { "HỢP ĐỒNG HỢP TÁC BÃI ĐỖ XE" },
            new[] { "Căn cứ Bộ luật Dân sự 2015;" },
            new[] { "BÊN A: CÔNG TY SMART PARKING" },
            new[] { "Điều 1. Phạm vi" },
            new[] { "1.1. Bên A niêm yết bãi của Bên B." },
            new[] { "a) Đặt chỗ trực tuyến." },
            new[] { "Điều 2. Phí" },
            new[] { "2.1. Phí 8% trên doanh thu." },
            new[] { "Phụ lục 01 – Bãi đỗ xe" },
            new[] { "Ghi chú phụ lục." },
        }, tableRows: []);

    [Fact]
    public async Task Import_without_ai_parses_articles_into_structured_draft()
    {
        var fx = Setup();

        var draft = await fx.Import().ExecuteAsync(
            new ImportContractTemplateRequest(null, new UploadedFile("Hop dong Vincom.docx", 100, SampleDocx()), UseAi: false), AdminId);

        Assert.Equal(("Hop dong Vincom", "Structured", "Hop dong Vincom.docx"), (draft.Name, draft.Kind, draft.SourceFileName));
        var articles = draft.Content!.Articles!;
        Assert.Equal(["Phạm vi", "Phí", PlainTextContractParser.SpecialTermsTitle], articles.Select(a => a.Title));
        Assert.Equal(["Bên A niêm yết bãi của Bên B.\na) Đặt chỗ trực tuyến."], articles[0].Clauses);
        Assert.Equal("HỢP ĐỒNG HỢP TÁC BÃI ĐỖ XE", draft.Content.Header!.Title);
        Assert.Equal(["Căn cứ Bộ luật Dân sự 2015;"], draft.Content.LegalBases);
        Assert.Contains(draft.Content.Appendices!, a => a.Kind == "LotInfo" && a.Title.StartsWith("Phụ lục 01"));
        Assert.Contains(draft.Content.Appendices!, a => a.Kind == "PayoutAccount");
        Assert.Contains("Bộ tách không dùng AI", draft.ImportNotes);
        Assert.Equal(0, fx.Analyzer.Calls);
        Assert.Single(fx.Storage.Files);
        Assert.Equal("ContractTemplate.Imported", Assert.Single(fx.Repository.Audits).Action);
    }

    [Fact]
    public async Task Import_with_ai_uses_analysis_and_party_a()
    {
        var fx = Setup();
        fx.Analyzer.Result = new ContractAnalysis(ContractMarkupParser.Parse(PartnerContractTemplates.V4Body),
            new PlatformParty("CÔNG TY AI", "Địa chỉ AI", "999", "1", "a@b.c", "NGƯỜI AI", "CEO"), ["AI cảnh báo."]);

        var draft = await fx.Import().ExecuteAsync(
            new ImportContractTemplateRequest("Mẫu AI", new UploadedFile("a.docx", 100, SampleDocx()), UseAi: true), AdminId);

        Assert.Equal(1, fx.Analyzer.Calls);
        Assert.Equal(("Structured", "CÔNG TY AI"), (draft.Kind, draft.Platform.Name));
        Assert.Equal("AI cảnh báo.", draft.ImportNotes);
    }

    [Fact]
    public async Task Import_falls_back_to_parser_when_ai_fails()
    {
        var fx = Setup();
        fx.Analyzer.Failure = new ContractAnalysisException("Không gọi được AI (HttpRequestException).");

        var draft = await fx.Import().ExecuteAsync(
            new ImportContractTemplateRequest("Mẫu lỗi AI", new UploadedFile("a.docx", 100, SampleDocx()), UseAi: true), AdminId);

        Assert.Equal("Structured", draft.Kind);
        Assert.StartsWith("AI: Không gọi được AI (HttpRequestException). Đã dùng bộ tách thường.", draft.ImportNotes);
    }

    [Fact]
    public async Task Import_without_articles_keeps_original_docx()
    {
        var fx = Setup();

        var draft = await fx.Import().ExecuteAsync(new ImportContractTemplateRequest("Thư mời",
            new UploadedFile("thu.docx", 100, DocxTestFiles.FromLines("Kính gửi quý đối tác", "Nội dung không có điều khoản")), UseAi: false),
            AdminId);

        Assert.Equal(("Document", (TemplateContentDto?)null), (draft.Kind, draft.Content));
        Assert.Contains("file DOCX gốc", draft.ImportNotes);
    }

    [Theory]
    [InlineData("a.doc", "Chỉ nhận file .docx. File .doc hoặc Google Docs hãy xuất sang .docx trước.")]
    [InlineData("a.docx", "File không phải DOCX hợp lệ. File .doc hoặc Google Docs hãy xuất sang .docx trước.")]
    public async Task Import_rejects_non_docx(string fileName, string message)
    {
        var fx = Setup();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => fx.Import().ExecuteAsync(
            new ImportContractTemplateRequest("x", new UploadedFile(fileName, 3, "abc"u8.ToArray()), UseAi: false), AdminId));

        Assert.Equal(message, ex.Message);
        Assert.Empty(fx.Storage.Files);
    }

    // ==================== T3 sửa bản nháp ====================

    [Fact]
    public async Task Update_draft_composes_body_from_form()
    {
        var fx = Setup(withDraft: true);
        var content = ContentDto(a => a[0] = a[0] with { Title = "Phạm vi mới", Clauses = ["# không phải tiêu đề", "Khoản 2 {{LotName}}"] });

        var detail = await fx.UpdateDraft().ExecuteAsync(7, Update(content), AdminId);

        Assert.Contains("## ĐIỀU 1. PHẠM VI MỚI\n1.1. # không phải tiêu đề\n1.2. Khoản 2 {{LotName}}", detail.Body);
        Assert.Equal("TRẦN VĂN AN", detail.Platform.RepresentativeName);
        Assert.Equal("ContractTemplate.DraftUpdated", Assert.Single(fx.Repository.Audits).Action);
    }

    [Fact]
    public async Task Update_active_template_is_conflict()
        => Assert.Equal($"Chỉ sửa được mẫu ở trạng thái Draft (mẫu \"{PartnerContractTemplates.StandardName}\" đang Active).",
            (await Assert.ThrowsAsync<ConflictException>(() =>
                Setup().UpdateDraft().ExecuteAsync(6, Update(rowVersion: "Bg==", name: "x"), AdminId))).Message);

    [Fact]
    public async Task Update_with_stale_row_version_is_conflict()
        => Assert.Contains("vừa được Admin khác cập nhật", (await Assert.ThrowsAsync<ConflictException>(() =>
            Setup(withDraft: true).UpdateDraft().ExecuteAsync(7, Update(rowVersion: "BA=="), AdminId))).Message);

    [Fact]
    public async Task Update_with_unknown_placeholder_or_two_special_articles_is_rejected()
    {
        var fx = Setup(withDraft: true);

        var unknown = await Assert.ThrowsAsync<ValidationException>(() => fx.UpdateDraft().ExecuteAsync(7,
            Update(ContentDto(a => a[0] = a[0] with { Clauses = ["Xin chào {{LotNmae}}"] })), AdminId));
        var twoSpecial = await Assert.ThrowsAsync<ValidationException>(() => fx.UpdateDraft().ExecuteAsync(7,
            Update(ContentDto(a => a[0] = a[0] with { IsSpecialTerms = true })), AdminId));
        var inline = await Assert.ThrowsAsync<ValidationException>(() => fx.UpdateDraft().ExecuteAsync(7,
            Update(ContentDto(a => a[0] = a[0] with { Clauses = ["Xem {{SpecialTerms}}"] })), AdminId));

        Assert.Equal("Placeholder không hợp lệ: {{LotNmae}}.", unknown.Message);
        Assert.Equal("Chỉ được một điều Thỏa thuận riêng.", twoSpecial.Message);
        Assert.StartsWith("Không dùng {{SpecialTerms}} trong khoản", inline.Message);
    }

    [Fact]
    public async Task Draft_without_source_file_cannot_switch_to_document()
        => Assert.Contains("chưa có file DOCX gốc", (await Assert.ThrowsAsync<ConflictException>(() =>
            Setup(withDraft: true).UpdateDraft().ExecuteAsync(7, Update(kind: "Document"), AdminId))).Message);

    // ==================== T3n đổi tên ====================

    [Fact]
    public async Task Rename_works_for_active_template_and_writes_audit()
    {
        var fx = Setup();

        var detail = await fx.Rename().ExecuteAsync(6, new RenameContractTemplateRequest("Hợp đồng tiêu chuẩn", "Bg=="), AdminId);

        Assert.Equal(("Hợp đồng tiêu chuẩn", "Active"), (detail.Name, detail.Status));
        Assert.Equal($"\"{PartnerContractTemplates.StandardName}\" → \"Hợp đồng tiêu chuẩn\"", Assert.Single(fx.Repository.Audits).Reason);
    }

    [Fact]
    public async Task Rename_to_name_of_other_template_is_conflict()
        => await Assert.ThrowsAsync<ConflictException>(() => Setup(withDraft: true).Rename()
            .ExecuteAsync(7, new RenameContractTemplateRequest(PartnerContractTemplates.StandardName, "Bw=="), AdminId));

    // ==================== T4 xem trước ====================

    [Fact]
    public void Preview_renders_form_content_with_sample_data_and_watermark()
    {
        var file = Setup().Preview().Execute(new PreviewContractTemplateRequest("Structured", ContentDto(), null, NewPlatform));

        var html = Text(file.Content);
        Assert.Contains("BẢN XEM TRƯỚC", html);
        Assert.Contains("<h2>BÊN A: CÔNG TY CP SMART PARKING VIỆT NAM</h2>", html);
        Assert.Contains("<h2>BÊN B: CÔNG TY TNHH BÃI XE MẪU</h2>", html);
        Assert.Contains("<h2>ĐIỀU 12. THỎA THUẬN RIÊNG</h2>", html);
        Assert.Contains("<p class=\"clause\">12.1. Bên B được miễn phí nền tảng", html);
        Assert.DoesNotContain("{{", html);
        Assert.DoesNotContain("\\", html);
    }

    [Fact]
    public void Preview_of_document_kind_is_rejected()
        => Assert.Throws<ValidationException>(() => Setup().Preview().Execute(new PreviewContractTemplateRequest("Document", null, null, null)));

    // ==================== T5 áp dụng / thôi áp dụng ====================

    [Fact]
    public async Task Activate_keeps_other_templates_active_unless_retire_source()
    {
        var fx = Setup(withDraft: true);
        await fx.UpdateDraft().ExecuteAsync(7, Update(), AdminId);
        fx.Repository.Templates.Single(t => t.Id == 7).RowVersion = [7];

        var detail = await fx.Activate().ExecuteAsync(7, new ActivateContractTemplateRequest("Bw=="), AdminId);

        Assert.Equal("Active", detail.Status);
        Assert.Equal(ContractTemplateStatus.Active, fx.Repository.Templates.Single(t => t.Id == 6).Status);
    }

    [Fact]
    public async Task Activate_with_retire_source_retires_the_copied_template()
    {
        var fx = Setup(withDraft: true);

        await fx.Activate().ExecuteAsync(7, new ActivateContractTemplateRequest("Bw==", RetireSource: true), AdminId);

        Assert.Equal(ContractTemplateStatus.Retired, fx.Repository.Templates.Single(t => t.Id == 6).Status);
        Assert.StartsWith($"Thay mẫu \"{PartnerContractTemplates.StandardName}\".", fx.Repository.Audits.Last().Reason);
    }

    [Fact]
    public async Task Activate_requires_required_fields_and_party_a()
    {
        var fx = Setup(withDraft: true);
        var noFee = ContentDto(a =>
        {
            for (var i = 0; i < a.Count; i++)
                a[i] = a[i] with { Clauses = a[i].Clauses!.Select(c => c.Replace("{{CommissionRate}}", "7")).ToList() };
        });
        await fx.UpdateDraft().ExecuteAsync(7, Update(noFee, NewPlatform with { Email = " " }), AdminId);
        fx.Repository.Templates.Single(t => t.Id == 7).RowVersion = [7];

        var missing = await Assert.ThrowsAsync<ValidationException>(() =>
            fx.Activate().ExecuteAsync(7, new ActivateContractTemplateRequest("Bw=="), AdminId));

        Assert.StartsWith("Mẫu thiếu trường bắt buộc: {{CommissionRate}}", missing.Message);
    }

    [Fact]
    public async Task Retire_moves_active_to_retired_and_cannot_retire_twice()
    {
        var fx = Setup();

        var detail = await fx.Retire().ExecuteAsync(6, new RetireContractTemplateRequest("Bg=="), AdminId);
        fx.Repository.Templates.Single(t => t.Id == 6).RowVersion = [6];

        Assert.Equal("Retired", detail.Status);
        await Assert.ThrowsAsync<ConflictException>(() => fx.Retire().ExecuteAsync(6, new RetireContractTemplateRequest("Bg=="), AdminId));
    }

    // ==================== T6c chuyển markup → form ====================

    [Fact]
    public async Task Convert_markup_template_creates_structured_draft()
    {
        var fx = Setup();
        var legacy = WithId(ContractTemplate.CreateDraft(4, PartnerContractTemplates.V4Name, ContractTemplateKind.Markup, AdminId), 4);
        legacy.UpdateDraft(legacy.Name, null, ContractTemplateKind.Markup, PartnerContractTemplates.V4Body, null, PartnerContractTemplates.V4Platform);
        fx.Repository.Templates.Add(legacy);

        var draft = await fx.Convert().ExecuteAsync(4, AdminId);

        Assert.Equal(($"{PartnerContractTemplates.V4Name} (form)", "Structured", "Draft", 4),
            (draft.Name, draft.Kind, draft.Status, draft.SourceTemplateId!.Value));
        Assert.Equal(13, draft.Content!.Articles!.Count);
        await Assert.ThrowsAsync<ConflictException>(() => fx.Convert().ExecuteAsync(6, AdminId));
    }

    // ==================== T6 hủy bản nháp ====================

    [Fact]
    public async Task Discard_removes_draft_only()
    {
        var fx = Setup(withDraft: true);

        await fx.Discard().ExecuteAsync(7, AdminId);

        Assert.DoesNotContain(fx.Repository.Templates, t => t.Id == 7);
        await Assert.ThrowsAsync<ConflictException>(() => fx.Discard().ExecuteAsync(6, AdminId));
    }

    // ==================== Danh mục placeholder ====================

    [Fact]
    public void Catalog_marks_required_and_block_placeholders_with_samples()
    {
        var catalog = new GetContractPlaceholdersUseCase(new FixedTimeProvider(new DateTimeOffset(Now))).Execute();

        var special = Assert.Single(catalog, p => p.Key == ContractPlaceholders.SpecialTermsKey);
        Assert.True(special.Required && special.Block);
        Assert.Equal("ngày 23 tháng 10 năm 2026", Assert.Single(catalog, p => p.Key == "IssuedDateLong").Sample);
    }

    // ==================== Fakes ====================

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Fixture(ContractTemplate active)
    {
        public FakeRepository Repository { get; } = new([active]);
        public FakeStorage Storage { get; } = new();
        public FakeAnalyzer Analyzer { get; } = new();
        public FixedTimeProvider Time { get; } = new(new DateTimeOffset(Now));

        private FakeQueries Queries => new(Repository);

        public CreateContractTemplateUseCase Create() => new(Repository, Queries);
        public ImportContractTemplateUseCase Import() => new(Repository, Queries, new DocxDocumentService(), Analyzer, Storage, Time);
        public UpdateContractTemplateDraftUseCase UpdateDraft() => new(Repository, Queries);
        public RenameContractTemplateUseCase Rename() => new(Repository, Queries);
        public ActivateContractTemplateUseCase Activate() => new(Repository, Queries, Time);
        public RetireContractTemplateUseCase Retire() => new(Repository, Queries);
        public ConvertContractTemplateUseCase Convert() => new(Repository, Queries);
        public DiscardContractTemplateDraftUseCase Discard() => new(Repository);
        public PreviewContractTemplateUseCase Preview() => new(new HtmlContractRenderer(), Time);
    }

    private sealed class FakeAnalyzer : IContractDocumentAnalyzer
    {
        public ContractAnalysis? Result { get; set; }
        public Exception? Failure { get; set; }
        public int Calls { get; private set; }
        public bool IsEnabled => true;

        public Task<ContractAnalysis> AnalyzeAsync(IReadOnlyList<DocxParagraph> paragraphs, IReadOnlyList<ContractPlaceholderDto> catalog,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (Failure is not null) throw Failure;
            return Task.FromResult(Result ?? PlainTextContractParser.Parse(paragraphs));
        }
    }

    private sealed class FakeStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];

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
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRepository(List<ContractTemplate> templates) : IContractTemplateRepository
    {
        public List<ContractTemplate> Templates { get; } = templates;
        public List<AuditLog> Audits { get; } = [];

        public Task<ContractTemplate?> FindTrackedAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult(Templates.FirstOrDefault(t => t.Id == id));

        public Task<ContractTemplate?> FindLatestActiveAsync(CancellationToken cancellationToken)
            => Task.FromResult(Templates.Where(t => t.Status == ContractTemplateStatus.Active).MaxBy(t => t.ActivatedAtUtc));

        public Task<bool> NameInUseAsync(string name, int? excludeId, CancellationToken cancellationToken)
            => Task.FromResult(Templates.Any(t => t.Status != ContractTemplateStatus.Retired && t.Id != excludeId
                                                  && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task<int> MaxVersionAsync(CancellationToken cancellationToken) => Task.FromResult(Templates.Max(t => t.Version));

        public void Add(ContractTemplate template) => Templates.Add(WithId(template, template.Version));

        public void Remove(ContractTemplate template) => Templates.Remove(template);

        public Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            Audits.Add(auditLog);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeQueries(FakeRepository repository) : IContractTemplateQueries
    {
        public Task<IReadOnlyList<ContractTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ContractTemplateSummaryDto>>(repository.Templates.OrderByDescending(t => t.Version)
                .Select(t => new ContractTemplateSummaryDto(t.Id, t.Version, t.Name, t.Kind.ToString(), t.Status.ToString(), t.ChangeNote,
                    t.CreatedAtUtc, t.UpdatedAtUtc, t.ActivatedAtUtc, t.CreatedByAdminId, 0)).ToList());

        public Task<ContractTemplateDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken)
        {
            var t = repository.Templates.FirstOrDefault(x => x.Id == id);
            if (t is null) return Task.FromResult<ContractTemplateDetailDto?>(null);
            var content = TemplateContentJson.Deserialize(t.ContentJson);
            var source = repository.Templates.FirstOrDefault(x => x.Id == t.SourceTemplateId);
            return Task.FromResult<ContractTemplateDetailDto?>(new ContractTemplateDetailDto(t.Id, t.Version, t.Name, t.Kind.ToString(),
                t.Status.ToString(), t.ChangeNote, content is null ? null : ContractTemplateMapping.ToDto(content), t.Body,
                ContractTemplateMapping.ToDto(t.Platform), t.SourceFileName, t.ImportNotes, t.SourceTemplateId, source?.Name, t.CreatedAtUtc,
                t.UpdatedAtUtc, t.ActivatedAtUtc, t.CreatedByAdminId, 0, Convert.ToBase64String(t.RowVersion)));
        }
    }
}
