using System.Text.Json;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using AdminService.Domain.ValueObjects;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.ContractTemplates;

// ===== REQUEST (người thao tác lấy từ JWT) =====

/// <summary>T2: mẫu mới. Mode Blank = khung mặc định (form); Duplicate = chép toàn bộ mẫu SourceTemplateId.</summary>
public sealed record CreateContractTemplateRequest(string? Name, string? Mode, int? SourceTemplateId);

/// <summary>T2i (multipart): nhập hợp đồng từ file .docx; UseAi = gửi chữ của file cho AI (Anthropic) phân tích.</summary>
public sealed record ImportContractTemplateRequest(string? Name, UploadedFile? File, bool UseAi);

/// <summary>T3: sửa bản nháp. Kind Structured cần Content; Markup cần Body; Document dùng file gốc. RowVersion lấy từ T1.</summary>
public sealed record UpdateContractTemplateDraftRequest(string? Name, string? ChangeNote, string? Kind, TemplateContentDto? Content,
    string? Body, PlatformPartyDto? Platform, string? RowVersion);

/// <summary>T3n: đổi tên (mọi trạng thái).</summary>
public sealed record RenameContractTemplateRequest(string? Name, string? RowVersion);

/// <summary>T4: xem trước nội dung đang soạn (chưa cần lưu) với hợp đồng giả định.</summary>
public sealed record PreviewContractTemplateRequest(string? Kind, TemplateContentDto? Content, string? Body, PlatformPartyDto? Platform);

/// <summary>T5: áp dụng bản nháp; RetireSource = đồng thời thôi áp dụng mẫu nguồn (bản nháp là phiên bản mới của mẫu đó).</summary>
public sealed record ActivateContractTemplateRequest(string? RowVersion, bool RetireSource = false);

/// <summary>T5r: thôi áp dụng mẫu Active.</summary>
public sealed record RetireContractTemplateRequest(string? RowVersion);

// ===== Dùng chung =====

/// <summary>Audit log thao tác Admin trên mẫu hợp đồng. EntityId = "v{Version}".</summary>
public static class ContractTemplateAudit
{
    public const string EntityName = nameof(ContractTemplate);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static object Snapshot(ContractTemplate t) => new
    {
        version = t.Version,
        name = t.Name,
        kind = t.Kind.ToString(),
        status = t.Status.ToString(),
        platformName = t.PlatformName,
        bodyLength = t.Body.Length,
    };

    public static AuditLog Build(ContractTemplate t, string action, int adminUserId, object? before, string? reason) => new()
    {
        SourceService = "AdminService",
        UserId = adminUserId,
        Action = $"{EntityName}.{action}",
        EntityName = EntityName,
        EntityId = $"v{t.Version}",
        OldValuesJson = before is null ? null : JsonSerializer.Serialize(before, Json),
        NewValuesJson = JsonSerializer.Serialize(Snapshot(t), Json),
        Reason = reason,
    };
}

/// <summary>Chuyển nội dung form giữa domain và DTO.</summary>
public static class ContractTemplateMapping
{
    public static TemplateContentDto ToDto(TemplateContent c) => new(
        new TemplateHeaderDto(c.Header.PlaceOfSigning, c.Header.Title, c.Header.Subtitle),
        c.LegalBases,
        c.Introduction,
        c.Articles.Select(a => new TemplateArticleDto(a.Title, a.Clauses, a.IsSpecialTerms)).ToList(),
        c.Appendices.Select(a => new TemplateAppendixDto(a.Kind.ToString(), a.Title, a.Subtitle, a.Notes)).ToList(),
        c.Signature.SignNote,
        c.Footnote);

    public static PlatformPartyDto ToDto(PlatformParty p)
        => new(p.Name, p.Address, p.TaxCode, p.Phone, p.Email, p.RepresentativeName, p.RepresentativePosition);
}

/// <summary>Kiểm tra dữ liệu mẫu hợp đồng (độ dài, placeholder, Bên A, nội dung form) – thông điệp tiếng Việt cho Admin.</summary>
internal static class ContractTemplateRules
{
    public const int MaxNameLength = 200;
    public const int MaxNoteLength = 1000;
    public const int MaxBodyLength = 100_000;
    public const int MaxPlatformFieldLength = 300;
    public const long MaxImportBytes = 10L * 1024 * 1024;
    public const string SpecialTermsLead =
        "Các thỏa thuận dưới đây chỉ áp dụng cho Bãi đỗ xe tại phụ lục và được ưu tiên áp dụng nếu khác với các điều khoản khác của Hợp đồng:";

    private static readonly (string Label, Func<PlatformParty, string> Get)[] PlatformFields =
    [
        ("tên công ty", p => p.Name),
        ("địa chỉ trụ sở", p => p.Address),
        ("mã số doanh nghiệp", p => p.TaxCode),
        ("điện thoại", p => p.Phone),
        ("email", p => p.Email),
        ("người đại diện", p => p.RepresentativeName),
        ("chức vụ người đại diện", p => p.RepresentativePosition),
    ];

    public static readonly PlatformParty EmptyPlatform = new("", "", "", "", "", "", "");

    public static void EnsureIds(int id, int adminUserId)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        if (adminUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
    }

    /// <summary>Tên bắt buộc, ≤ 200, không trùng mẫu Draft / Active khác.</summary>
    public static async Task<string> NameAsync(IContractTemplateRepository repository, string? name, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ValidationException("Tên mẫu không được để trống.");
        var n = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (n.Length > MaxNameLength) throw new ValidationException($"Tên mẫu tối đa {MaxNameLength} ký tự.");
        if (await repository.NameInUseAsync(n, excludeId, ct)) throw new ConflictException($"Tên mẫu \"{n}\" đã được dùng cho mẫu khác.");
        return n;
    }

    public static string? Note(string? note)
    {
        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text?.Length > MaxNoteLength) throw new ValidationException($"Ghi chú thay đổi tối đa {MaxNoteLength} ký tự.");
        return text;
    }

    public static ContractTemplateKind Kind(string? kind, ContractTemplateKind fallback)
    {
        if (string.IsNullOrWhiteSpace(kind)) return fallback;
        return Enum.TryParse<ContractTemplateKind>(kind.Trim(), ignoreCase: true, out var k) && Enum.IsDefined(k)
            ? k
            : throw new ValidationException("Loại mẫu không hợp lệ (Structured, Markup, Document).");
    }

    /// <summary>Markup gõ tay (mẫu cũ): bắt buộc, ≤ 100 000, chỉ placeholder trong danh mục.</summary>
    public static string Body(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) throw new ValidationException("Nội dung mẫu không được để trống.");
        var text = body.Replace("\r\n", "\n").TrimEnd();
        if (text.Length > MaxBodyLength) throw new ValidationException($"Nội dung mẫu tối đa {MaxBodyLength} ký tự.");
        EnsureKnownPlaceholders(text);
        return text;
    }

    public static PlatformParty Platform(PlatformPartyDto? dto)
    {
        var platform = new PlatformParty(Trim(dto?.Name), Trim(dto?.Address), Trim(dto?.TaxCode), Trim(dto?.Phone), Trim(dto?.Email),
            Trim(dto?.RepresentativeName), Trim(dto?.RepresentativePosition));
        foreach (var (label, get) in PlatformFields)
            if (get(platform).Length > MaxPlatformFieldLength)
                throw new ValidationException($"Thông tin Bên A ({label}) tối đa {MaxPlatformFieldLength} ký tự.");
        return platform;
    }

    /// <summary>Nội dung form: chuẩn hóa (trim, bỏ dòng trống) + giới hạn số lượng / độ dài + placeholder hợp lệ.</summary>
    public static TemplateContent Content(TemplateContentDto? dto)
    {
        if (dto is null) throw new ValidationException("Thiếu nội dung mẫu (content).");
        var header = dto.Header;
        var title = Text(header?.Title, "Tên hợp đồng (tiêu đề)", 300, required: true)!;
        var place = Text(header?.PlaceOfSigning, "Nơi ký", 100) ?? "TP. Hồ Chí Minh";
        var legalBases = List(dto.LegalBases, "Căn cứ pháp lý", 30, 1000);
        var articles = new List<TemplateArticle>();
        foreach (var (a, i) in (dto.Articles ?? []).Select((a, i) => (a, i)))
        {
            var label = $"Điều {i + 1}";
            articles.Add(new TemplateArticle(Text(a.Title, $"Tiêu đề {label}", 300, required: true)!,
                List(a.Clauses, $"Khoản của {label}", 80, 5000), a.IsSpecialTerms));
        }
        if (articles.Count > 60) throw new ValidationException("Tối đa 60 điều.");
        if (articles.Count(a => a.IsSpecialTerms) > 1) throw new ValidationException("Chỉ được một điều Thỏa thuận riêng.");
        var appendices = new List<TemplateAppendix>();
        foreach (var (a, i) in (dto.Appendices ?? []).Select((a, i) => (a, i)))
        {
            var label = $"Phụ lục {i + 1}";
            var kind = Enum.TryParse<TemplateAppendixKind>(a.Kind?.Trim(), ignoreCase: true, out var k) && Enum.IsDefined(k)
                ? k
                : throw new ValidationException($"Loại {label} không hợp lệ (LotInfo, PayoutAccount, Text).");
            appendices.Add(new TemplateAppendix(kind, Text(a.Title, $"Tiêu đề {label}", 300, required: true)!,
                Text(a.Subtitle, $"Phụ đề {label}", 300), List(a.Notes, $"Ghi chú {label}", 30, 2000)));
        }
        if (appendices.Count > 10) throw new ValidationException("Tối đa 10 phụ lục.");

        var content = new TemplateContent(new TemplateHeader(place, title, Text(header?.Subtitle, "Phụ đề", 300)), legalBases,
            Text(dto.Introduction, "Lời dẫn", 5000, multiline: true), articles, appendices,
            new TemplateSignature(Text(dto.SignNote, "Ghi chú chữ ký", 200)), Text(dto.Footnote, "Ghi chú cuối", 2000, multiline: true));
        var markup = ContractTemplateComposer.ToMarkup(content);
        if (markup.Length > MaxBodyLength) throw new ValidationException($"Nội dung mẫu tối đa {MaxBodyLength} ký tự.");
        var special = $"{{{{{ContractPlaceholders.SpecialTermsKey}}}}}";
        if (articles.Any(a => a.Clauses.Any(c => c.Contains(special, StringComparison.Ordinal)))
            || content.Introduction?.Contains(special, StringComparison.Ordinal) == true)
            throw new ValidationException($"Không dùng {special} trong khoản – hãy đánh dấu một điều là Thỏa thuận riêng.");
        EnsureKnownPlaceholders(markup);
        return content;
    }

    /// <summary>Khung mặc định của mẫu mới: thời hạn, phí nền tảng, thỏa thuận riêng, phụ lục bãi + tài khoản.</summary>
    public static TemplateContent BlankContent() => new(
        new TemplateHeader("TP. Hồ Chí Minh", "Hợp đồng hợp tác khai thác bãi đỗ xe", "TRÊN NỀN TẢNG SMART PARKING MARKETPLACE"),
        ["Căn cứ Bộ luật Dân sự số 91/2015/QH13 ngày 24/11/2015;", "Căn cứ nhu cầu và khả năng của hai bên."],
        "Hai bên thống nhất ký kết Hợp đồng số {{ContractNumber}} để khai thác bãi đỗ xe **{{LotName}}** trên Nền tảng, với các điều khoản sau:",
        [
            new TemplateArticle("Thời hạn hợp đồng",
                ["Hợp đồng có hiệu lực **{{TermMonthsText}} tháng**, kể từ ngày **{{EffectiveFrom}}** đến hết ngày **{{EffectiveTo}}**."]),
            new TemplateArticle("Phí nền tảng", ["Phí nền tảng: **{{CommissionRate}}%** trên GMV của mỗi kỳ đối soát."]),
            new TemplateArticle(PlainTextContractParser.SpecialTermsTitle, [SpecialTermsLead], IsSpecialTerms: true),
        ],
        [
            new TemplateAppendix(TemplateAppendixKind.LotInfo, "Phụ lục 01", "BÃI ĐỖ XE HỢP TÁC", []),
            new TemplateAppendix(TemplateAppendixKind.PayoutAccount, "Phụ lục 02", "THÔNG TIN TÀI KHOẢN NHẬN THANH TOÁN", []),
        ],
        new TemplateSignature(null),
        null);

    /// <summary>Trước khi áp dụng: kiểm tra theo loại mẫu.</summary>
    public static void EnsureReadyToActivate(ContractTemplate t)
    {
        switch (t.Kind)
        {
            case ContractTemplateKind.Document:
                if (t.SourceFile is null) throw new ValidationException("Mẫu loại Document chưa có file DOCX gốc.");
                return;
            case ContractTemplateKind.Structured:
                var content = TemplateContentJson.Deserialize(t.ContentJson)
                              ?? throw new ValidationException("Mẫu chưa có nội dung form.");
                if (content.Articles.Count == 0) throw new ValidationException("Mẫu cần ít nhất một điều.");
                if (content.Articles.Count(a => a.IsSpecialTerms) != 1)
                    throw new ValidationException("Mẫu cần đúng một điều Thỏa thuận riêng (in thỏa thuận riêng của từng bãi).");
                break;
            default:
                Body(t.Body);
                break;
        }
        var missing = ContractPlaceholders.MissingRequired(t.Body);
        if (missing.Count > 0)
            throw new ValidationException(
                $"Mẫu thiếu trường bắt buộc: {string.Join(", ", missing.Select(k => $"{{{{{k}}}}}"))} "
                + $"({{{{{ContractPlaceholders.SpecialTermsKey}}}}} là điều Thỏa thuận riêng).");
        var empty = PlatformFields.Where(f => f.Get(t.Platform).Length == 0).Select(f => f.Label).ToList();
        if (empty.Count > 0) throw new ValidationException($"Chưa nhập thông tin Bên A: {string.Join(", ", empty)}.");
    }

    /// <summary>RowVersion (base64) Admin gửi lên phải khớp bản đang lưu – lệch nghĩa là Admin khác vừa sửa.</summary>
    public static void EnsureSameVersion(ContractTemplate t, string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion)) throw new ValidationException("Thiếu rowVersion – vui lòng tải lại mẫu hợp đồng.");
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new ValidationException("rowVersion không hợp lệ.");
        }
        if (!bytes.AsSpan().SequenceEqual(t.RowVersion))
            throw new ConflictException($"Mẫu hợp đồng \"{t.Name}\" vừa được Admin khác cập nhật, vui lòng tải lại và thử lại.");
    }

    public static async Task<ContractTemplate> LoadAsync(IContractTemplateRepository repository, int id, CancellationToken ct)
        => await repository.FindTrackedAsync(id, ct) ?? throw new NotFoundException("Mẫu hợp đồng", id);

    public static async Task<PlatformParty> DefaultPlatformAsync(IContractTemplateRepository repository, CancellationToken ct)
        => (await repository.FindLatestActiveAsync(ct))?.Platform ?? EmptyPlatform;

    private static void EnsureKnownPlaceholders(string text)
    {
        var unknown = ContractPlaceholders.Unknown(text);
        if (unknown.Count > 0)
            throw new ValidationException($"Placeholder không hợp lệ: {string.Join(", ", unknown.Select(k => $"{{{{{k}}}}}"))}.");
    }

    private static string? Text(string? value, string field, int max, bool required = false, bool multiline = false)
    {
        var text = multiline
            ? string.Join('\n', ContractTemplateComposer.SplitLines(value))
            : string.Join(' ', ContractTemplateComposer.SplitLines(value));
        if (text.Length == 0)
        {
            if (required) throw new ValidationException($"{field} không được để trống.");
            return null;
        }
        if (text.Length > max) throw new ValidationException($"{field} tối đa {max} ký tự.");
        return text;
    }

    private static List<string> List(IReadOnlyList<string>? items, string field, int maxItems, int maxLength)
    {
        var list = (items ?? []).Select(i => string.Join('\n', ContractTemplateComposer.SplitLines(i))).Where(i => i.Length > 0).ToList();
        if (list.Count > maxItems) throw new ValidationException($"{field}: tối đa {maxItems} mục.");
        if (list.Any(i => i.Length > maxLength)) throw new ValidationException($"{field}: mỗi mục tối đa {maxLength} ký tự.");
        return list;
    }

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;
}

// ===== USE CASE (đọc) =====

public interface IListContractTemplatesUseCase
{
    Task<IReadOnlyList<ContractTemplateSummaryDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>T0: danh sách mẫu hợp đồng, mới nhất trước.</summary>
public sealed class ListContractTemplatesUseCase(IContractTemplateQueries queries) : IListContractTemplatesUseCase
{
    public Task<IReadOnlyList<ContractTemplateSummaryDto>> ExecuteAsync(CancellationToken cancellationToken = default)
        => queries.ListAsync(cancellationToken);
}

public interface IGetContractTemplateUseCase
{
    Task<ContractTemplateDetailDto> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>T1: chi tiết mẫu (nội dung form / markup + Bên A).</summary>
public sealed class GetContractTemplateUseCase(IContractTemplateQueries queries) : IGetContractTemplateUseCase
{
    public async Task<ContractTemplateDetailDto> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        return await queries.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException("Mẫu hợp đồng", id);
    }
}

public interface IGetContractPlaceholdersUseCase
{
    IReadOnlyList<ContractPlaceholderDto> Execute();
}

/// <summary>T1p: danh mục trường (placeholder) – menu "Insert field" trên form.</summary>
public sealed class GetContractPlaceholdersUseCase(TimeProvider timeProvider) : IGetContractPlaceholdersUseCase
{
    public IReadOnlyList<ContractPlaceholderDto> Execute() => ContractPlaceholders.Catalog(timeProvider.GetUtcNow().UtcDateTime);
}

public interface IGetContractTemplateSourceFileUseCase
{
    Task<ContractFileResult> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>T1f: tải file DOCX gốc của mẫu (SHA-256 kiểm tra lại).</summary>
public sealed class GetContractTemplateSourceFileUseCase(IContractTemplateRepository repository, IFileStorage storage)
    : IGetContractTemplateSourceFileUseCase
{
    public async Task<ContractFileResult> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        var template = await ContractTemplateRules.LoadAsync(repository, id, cancellationToken);
        var source = template.SourceFile ?? throw new NotFoundException("File DOCX gốc của mẫu", id);
        var content = await storage.ReadAsync(source.Key, cancellationToken) ?? throw new NotFoundException("File DOCX gốc của mẫu", id);
        if (!string.Equals(ContractSupport.Sha256Hex(content), source.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException($"File DOCX gốc của mẫu \"{template.Name}\" không còn toàn vẹn (SHA-256 không khớp).");
        return new ContractFileResult(content, IDocxDocumentService.ContentType, source.FileName);
    }
}

public interface IPreviewContractTemplateUseCase
{
    ContractFileResult Execute(PreviewContractTemplateRequest request);
}

/// <summary>T4: xem trước nội dung đang soạn (form hoặc markup) với hợp đồng giả định – có watermark, không lưu gì.</summary>
public sealed class PreviewContractTemplateUseCase(IContractRenderer renderer, TimeProvider timeProvider) : IPreviewContractTemplateUseCase
{
    public ContractFileResult Execute(PreviewContractTemplateRequest request)
    {
        var kind = ContractTemplateRules.Kind(request.Kind, ContractTemplateKind.Structured);
        var body = kind switch
        {
            ContractTemplateKind.Structured => ContractTemplateComposer.ToMarkup(ContractTemplateRules.Content(request.Content)),
            ContractTemplateKind.Markup => ContractTemplateRules.Body(request.Body),
            _ => throw new ValidationException("Mẫu loại Document không xem trước được – hãy tải file DOCX gốc."),
        };
        var platform = ContractTemplateRules.Platform(request.Platform);
        var values = ContractPlaceholders.SampleValues(platform, timeProvider.GetUtcNow().UtcDateTime);
        var file = renderer.Render(body, 0, values, previewWatermark: true);
        return new ContractFileResult(file.Content, file.ContentType, $"contract-template-preview{file.FileExtension}");
    }
}

// ===== USE CASE (ghi) =====

public interface ICreateContractTemplateUseCase
{
    Task<ContractTemplateDetailDto> ExecuteAsync(CreateContractTemplateRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>T2: mẫu mới (Draft) có tên do Admin đặt – khung trống dạng form hoặc bản sao một mẫu có sẵn.</summary>
public sealed class CreateContractTemplateUseCase(IContractTemplateRepository repository, IContractTemplateQueries queries)
    : ICreateContractTemplateUseCase
{
    public async Task<ContractTemplateDetailDto> ExecuteAsync(CreateContractTemplateRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (performedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
        var mode = string.IsNullOrWhiteSpace(request.Mode) ? "Blank" : request.Mode.Trim();
        var name = await ContractTemplateRules.NameAsync(repository, request.Name, null, cancellationToken);
        var version = await repository.MaxVersionAsync(cancellationToken) + 1;
        ContractTemplate draft;
        string reason;
        if (string.Equals(mode, "Duplicate", StringComparison.OrdinalIgnoreCase))
        {
            if (request.SourceTemplateId is not { } sourceId || sourceId <= 0)
                throw new ValidationException("Chọn mẫu nguồn (sourceTemplateId) để sao chép.");
            var source = await ContractTemplateRules.LoadAsync(repository, sourceId, cancellationToken);
            draft = ContractTemplate.CreateDraft(version, name, source, performedByUserId);
            reason = $"Sao chép từ mẫu \"{source.Name}\" (v{source.Version})";
        }
        else if (string.Equals(mode, "Blank", StringComparison.OrdinalIgnoreCase))
        {
            var content = ContractTemplateRules.BlankContent();
            draft = ContractTemplate.CreateDraft(version, name, ContractTemplateKind.Structured, performedByUserId);
            draft.UpdateDraft(name, null, ContractTemplateKind.Structured, ContractTemplateComposer.ToMarkup(content),
                TemplateContentJson.Serialize(content), await ContractTemplateRules.DefaultPlatformAsync(repository, cancellationToken));
            reason = "Mẫu trống (form)";
        }
        else throw new ValidationException("mode phải là Blank hoặc Duplicate.");

        repository.Add(draft);
        await repository.SaveWithAuditAsync(ContractTemplateAudit.Build(draft, "Created", performedByUserId, null, reason), cancellationToken);
        return await queries.GetDetailAsync(draft.Id, cancellationToken) ?? throw new NotFoundException("Mẫu hợp đồng", draft.Id);
    }
}

public interface IImportContractTemplateUseCase
{
    Task<ContractTemplateDetailDto> ExecuteAsync(ImportContractTemplateRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// T2i: nhập hợp đồng từ file .docx → lưu file gốc → đọc chữ → AI phân tích (nếu Admin bật và đã cấu hình; lỗi thì dùng bộ tách thường)
/// → tách được điều khoản: bản nháp dạng form (Structured), không: bản nháp dùng file gốc (Document). Cảnh báo lưu ở ImportNotes.
/// </summary>
public sealed class ImportContractTemplateUseCase(IContractTemplateRepository repository, IContractTemplateQueries queries,
    IDocxDocumentService docx, IContractDocumentAnalyzer analyzer, IFileStorage storage, TimeProvider timeProvider)
    : IImportContractTemplateUseCase
{
    public async Task<ContractTemplateDetailDto> ExecuteAsync(ImportContractTemplateRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (performedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
        var file = request.File ?? throw new ValidationException("Vui lòng chọn file hợp đồng (.docx).");
        if (file.Length <= 0 || file.Content.Length == 0) throw new ValidationException("File hợp đồng rỗng.");
        if (file.Length > ContractTemplateRules.MaxImportBytes || file.Content.Length > ContractTemplateRules.MaxImportBytes)
            throw new ValidationException("File hợp đồng tối đa 10 MB.");
        if (!file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Chỉ nhận file .docx. File .doc hoặc Google Docs hãy xuất sang .docx trước.");
        var fileName = Path.GetFileName(file.FileName);
        var name = await ContractTemplateRules.NameAsync(repository,
            string.IsNullOrWhiteSpace(request.Name) ? Path.GetFileNameWithoutExtension(fileName) : request.Name, null, cancellationToken);
        var paragraphs = docx.ReadParagraphs(file.Content);

        var warnings = new List<string>();
        ContractAnalysis analysis;
        var usedAi = false;
        if (request.UseAi && analyzer.IsEnabled)
        {
            try
            {
                analysis = await analyzer.AnalyzeAsync(paragraphs, ContractPlaceholders.Catalog(timeProvider.GetUtcNow().UtcDateTime),
                    cancellationToken);
                usedAi = true;
            }
            catch (ContractAnalysisException ex)
            {
                warnings.Add($"AI: {ex.Message} Đã dùng bộ tách thường.");
                analysis = PlainTextContractParser.Parse(paragraphs);
            }
        }
        else
        {
            if (request.UseAi) warnings.Add("AI chưa được cấu hình (ANTHROPIC_API_KEY) – đã dùng bộ tách thường.");
            analysis = PlainTextContractParser.Parse(paragraphs);
        }
        warnings.AddRange(analysis.Warnings);

        var structured = analysis.Content.Articles.Count > 0;
        string? markup = null;
        if (structured)
        {
            markup = ContractTemplateComposer.ToMarkup(analysis.Content);
            if (markup.Length > ContractTemplateRules.MaxBodyLength || ContractPlaceholders.Unknown(markup).Count > 0)
            {
                structured = false;
                warnings.Add("Nội dung tách được không hợp lệ cho form – giữ file DOCX gốc.");
            }
        }
        if (!structured) warnings.Add("Mẫu dùng file DOCX gốc: hợp đồng bãi là bản sao file, chỉ cần nhập phí nền tảng.");

        var version = await repository.MaxVersionAsync(cancellationToken) + 1;
        var key = $"contract-templates/source-{Guid.NewGuid():N}.docx";
        await storage.SaveAsync(key, file.Content, cancellationToken);
        var kind = structured ? ContractTemplateKind.Structured : ContractTemplateKind.Document;
        var draft = ContractTemplate.CreateDraft(version, name, kind, performedByUserId);
        draft.SetSourceFile(new TemplateSourceFile(key, fileName, ContractSupport.Sha256Hex(file.Content)));
        draft.UpdateDraft(name, $"Nhập từ file {fileName}", kind, markup ?? string.Empty,
            structured ? TemplateContentJson.Serialize(analysis.Content) : null,
            analysis.Platform ?? await ContractTemplateRules.DefaultPlatformAsync(repository, cancellationToken));
        draft.ImportNotes = warnings.Count == 0 ? null : string.Join('\n', warnings.Distinct());

        repository.Add(draft);
        try
        {
            await repository.SaveWithAuditAsync(ContractTemplateAudit.Build(draft, "Imported", performedByUserId, null,
                $"{fileName} – {(usedAi ? "AI" : "bộ tách thường")} – {kind}"), cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }
        return await queries.GetDetailAsync(draft.Id, cancellationToken) ?? throw new NotFoundException("Mẫu hợp đồng", draft.Id);
    }
}

public interface IUpdateContractTemplateDraftUseCase
{
    Task<ContractTemplateDetailDto> ExecuteAsync(int id, UpdateContractTemplateDraftRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>T3: sửa bản nháp – tên, ghi chú, loại (form ↔ file gốc), nội dung form / markup, Bên A.</summary>
public sealed class UpdateContractTemplateDraftUseCase(IContractTemplateRepository repository, IContractTemplateQueries queries)
    : IUpdateContractTemplateDraftUseCase
{
    public async Task<ContractTemplateDetailDto> ExecuteAsync(int id, UpdateContractTemplateDraftRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        ContractTemplateRules.EnsureIds(id, performedByUserId);
        var template = await ContractTemplateRules.LoadAsync(repository, id, cancellationToken);
        template.EnsureDraft("sửa");
        ContractTemplateRules.EnsureSameVersion(template, request.RowVersion);
        var name = await ContractTemplateRules.NameAsync(repository, request.Name, id, cancellationToken);
        var note = ContractTemplateRules.Note(request.ChangeNote);
        var kind = ContractTemplateRules.Kind(request.Kind, template.Kind);
        if (kind == ContractTemplateKind.Markup && template.Kind != ContractTemplateKind.Markup)
            throw new ValidationException("Không chuyển mẫu sang dạng markup – hãy soạn bằng form.");
        if (template.Kind == ContractTemplateKind.Markup && kind != ContractTemplateKind.Markup)
            throw new ValidationException("Mẫu markup cũ: dùng \"Convert to form\" để chuyển sang form.");
        var platform = ContractTemplateRules.Platform(request.Platform);

        string body;
        var contentJson = template.ContentJson;
        switch (kind)
        {
            case ContractTemplateKind.Structured:
                var content = ContractTemplateRules.Content(request.Content);
                body = ContractTemplateComposer.ToMarkup(content);
                contentJson = TemplateContentJson.Serialize(content);
                break;
            case ContractTemplateKind.Markup:
                body = ContractTemplateRules.Body(request.Body);
                break;
            default:
                if (request.Content is not null)
                {
                    var kept = ContractTemplateRules.Content(request.Content);
                    contentJson = TemplateContentJson.Serialize(kept);
                }
                body = template.Body;
                break;
        }

        var before = ContractTemplateAudit.Snapshot(template);
        template.UpdateDraft(name, note, kind, body, contentJson, platform);
        await repository.SaveWithAuditAsync(ContractTemplateAudit.Build(template, "DraftUpdated", performedByUserId, before, note),
            cancellationToken);
        return await queries.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException("Mẫu hợp đồng", id);
    }
}

public interface IRenameContractTemplateUseCase
{
    Task<ContractTemplateDetailDto> ExecuteAsync(int id, RenameContractTemplateRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>T3n: đổi tên mẫu ở mọi trạng thái (chỉ tên); tên không trùng mẫu Draft / Active khác.</summary>
public sealed class RenameContractTemplateUseCase(IContractTemplateRepository repository, IContractTemplateQueries queries)
    : IRenameContractTemplateUseCase
{
    public async Task<ContractTemplateDetailDto> ExecuteAsync(int id, RenameContractTemplateRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        ContractTemplateRules.EnsureIds(id, performedByUserId);
        var template = await ContractTemplateRules.LoadAsync(repository, id, cancellationToken);
        ContractTemplateRules.EnsureSameVersion(template, request.RowVersion);
        var name = await ContractTemplateRules.NameAsync(repository, request.Name, id, cancellationToken);
        var before = ContractTemplateAudit.Snapshot(template);
        var old = template.Name;
        template.Rename(name);
        await repository.SaveWithAuditAsync(
            ContractTemplateAudit.Build(template, "Renamed", performedByUserId, before, $"\"{old}\" → \"{name}\""), cancellationToken);
        return await queries.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException("Mẫu hợp đồng", id);
    }
}

public interface IActivateContractTemplateUseCase
{
    Task<ContractTemplateDetailDto> ExecuteAsync(int id, ActivateContractTemplateRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// T5: bản nháp → Active (chọn được khi lập hợp đồng bãi; nhiều mẫu Active cùng lúc). RetireSource = mẫu nguồn đang Active → Retired
/// trong cùng lần lưu. Kiểm tra theo loại mẫu (trường bắt buộc, điều Thỏa thuận riêng, Bên A, file gốc) → 400.
/// </summary>
public sealed class ActivateContractTemplateUseCase(IContractTemplateRepository repository, IContractTemplateQueries queries,
    TimeProvider timeProvider) : IActivateContractTemplateUseCase
{
    public async Task<ContractTemplateDetailDto> ExecuteAsync(int id, ActivateContractTemplateRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        ContractTemplateRules.EnsureIds(id, performedByUserId);
        var draft = await ContractTemplateRules.LoadAsync(repository, id, cancellationToken);
        draft.EnsureDraft("áp dụng");
        ContractTemplateRules.EnsureSameVersion(draft, request.RowVersion);
        ContractTemplateRules.EnsureReadyToActivate(draft);

        var before = ContractTemplateAudit.Snapshot(draft);
        string? reason = draft.ChangeNote;
        if (request.RetireSource && draft.SourceTemplateId is { } sourceId
            && await repository.FindTrackedAsync(sourceId, cancellationToken) is { Status: ContractTemplateStatus.Active } source)
        {
            source.Retire();
            reason = $"Thay mẫu \"{source.Name}\". {draft.ChangeNote}".Trim();
        }
        draft.Activate(timeProvider.GetUtcNow().UtcDateTime);
        await repository.SaveWithAuditAsync(ContractTemplateAudit.Build(draft, "Activated", performedByUserId, before, reason),
            cancellationToken);
        return await queries.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException("Mẫu hợp đồng", id);
    }
}

public interface IRetireContractTemplateUseCase
{
    Task<ContractTemplateDetailDto> ExecuteAsync(int id, RetireContractTemplateRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>T5r: thôi áp dụng – không chọn được khi lập hợp đồng mới; hợp đồng đã sinh giữ nguyên file.</summary>
public sealed class RetireContractTemplateUseCase(IContractTemplateRepository repository, IContractTemplateQueries queries)
    : IRetireContractTemplateUseCase
{
    public async Task<ContractTemplateDetailDto> ExecuteAsync(int id, RetireContractTemplateRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        ContractTemplateRules.EnsureIds(id, performedByUserId);
        var template = await ContractTemplateRules.LoadAsync(repository, id, cancellationToken);
        ContractTemplateRules.EnsureSameVersion(template, request.RowVersion);
        var before = ContractTemplateAudit.Snapshot(template);
        template.Retire();
        await repository.SaveWithAuditAsync(ContractTemplateAudit.Build(template, "Retired", performedByUserId, before, null), cancellationToken);
        return await queries.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException("Mẫu hợp đồng", id);
    }
}

public interface IConvertContractTemplateUseCase
{
    Task<ContractTemplateDetailDto> ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>T6c: mẫu markup cũ → bản nháp mới dạng form (đọc markup thành đầu trang, căn cứ, điều / khoản, phụ lục).</summary>
public sealed class ConvertContractTemplateUseCase(IContractTemplateRepository repository, IContractTemplateQueries queries)
    : IConvertContractTemplateUseCase
{
    public async Task<ContractTemplateDetailDto> ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default)
    {
        ContractTemplateRules.EnsureIds(id, performedByUserId);
        var source = await ContractTemplateRules.LoadAsync(repository, id, cancellationToken);
        if (source.Kind != ContractTemplateKind.Markup)
            throw new ConflictException($"Mẫu \"{source.Name}\" đã soạn bằng form hoặc là file DOCX – không cần chuyển.");
        var content = ContractMarkupParser.Parse(source.Body);
        var name = $"{source.Name} (form)";
        for (var n = 2; await repository.NameInUseAsync(name, null, cancellationToken); n++) name = $"{source.Name} (form {n})";
        if (name.Length > ContractTemplateRules.MaxNameLength) name = name[..ContractTemplateRules.MaxNameLength];

        var draft = ContractTemplate.CreateDraft(await repository.MaxVersionAsync(cancellationToken) + 1, name, ContractTemplateKind.Structured,
            performedByUserId, source.Id);
        draft.UpdateDraft(name, $"Chuyển sang form từ mẫu \"{source.Name}\"", ContractTemplateKind.Structured,
            ContractTemplateComposer.ToMarkup(content), TemplateContentJson.Serialize(content), source.Platform);
        repository.Add(draft);
        await repository.SaveWithAuditAsync(ContractTemplateAudit.Build(draft, "ConvertedToForm", performedByUserId, null,
            $"Từ mẫu \"{source.Name}\" (v{source.Version})"), cancellationToken);
        return await queries.GetDetailAsync(draft.Id, cancellationToken) ?? throw new NotFoundException("Mẫu hợp đồng", draft.Id);
    }
}

public interface IDiscardContractTemplateDraftUseCase
{
    Task ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>T6: hủy (xóa) bản nháp. Mẫu Active / Retired không xóa được (409).</summary>
public sealed class DiscardContractTemplateDraftUseCase(IContractTemplateRepository repository) : IDiscardContractTemplateDraftUseCase
{
    public async Task ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default)
    {
        ContractTemplateRules.EnsureIds(id, performedByUserId);
        var draft = await ContractTemplateRules.LoadAsync(repository, id, cancellationToken);
        draft.EnsureDraft("hủy");
        var audit = ContractTemplateAudit.Build(draft, "DraftDiscarded", performedByUserId, ContractTemplateAudit.Snapshot(draft), reason: null);
        repository.Remove(draft);
        await repository.SaveWithAuditAsync(audit, cancellationToken);
    }
}
