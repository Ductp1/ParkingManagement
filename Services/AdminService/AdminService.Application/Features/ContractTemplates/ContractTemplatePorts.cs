using AdminService.Domain.Entities;
using AdminService.Domain.ValueObjects;

namespace AdminService.Application.Features.ContractTemplates;

// ===== DTO =====

/// <summary>Bên A (nền tảng) trên mẫu hợp đồng. Trường null / rỗng khi gửi lên = để trống.</summary>
public sealed record PlatformPartyDto(string? Name, string? Address, string? TaxCode, string? Phone, string? Email,
    string? RepresentativeName, string? RepresentativePosition);

/// <summary>Nội dung mẫu dạng form (khớp TemplateContent). Chữ nhập có thể chứa placeholder {{Key}} của danh mục.</summary>
public sealed record TemplateContentDto(TemplateHeaderDto? Header, IReadOnlyList<string>? LegalBases, string? Introduction,
    IReadOnlyList<TemplateArticleDto>? Articles, IReadOnlyList<TemplateAppendixDto>? Appendices, string? SignNote, string? Footnote);

public sealed record TemplateHeaderDto(string? PlaceOfSigning, string? Title, string? Subtitle);

public sealed record TemplateArticleDto(string? Title, IReadOnlyList<string>? Clauses, bool IsSpecialTerms);

/// <summary>Kind: LotInfo / PayoutAccount (bảng do hệ thống dựng) / Text.</summary>
public sealed record TemplateAppendixDto(string? Kind, string? Title, string? Subtitle, IReadOnlyList<string>? Notes);

/// <summary>Dòng danh sách mẫu hợp đồng (T0). UsedByVersions = số phiên bản hợp đồng bãi đã sinh từ mẫu này.</summary>
public sealed record ContractTemplateSummaryDto(int Id, int Version, string Name, string Kind, string Status, string? ChangeNote,
    DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, DateTime? ActivatedAtUtc, int? CreatedByAdminId, int UsedByVersions);

/// <summary>
/// Chi tiết mẫu hợp đồng (T1). Content có với mẫu Structured; Body = markup sinh tự động (Structured) hoặc gõ tay (Markup).
/// RowVersion (base64) gửi lại khi sửa / đổi tên / áp dụng / thôi áp dụng để phát hiện Admin khác vừa sửa.
/// </summary>
public sealed record ContractTemplateDetailDto(int Id, int Version, string Name, string Kind, string Status, string? ChangeNote,
    TemplateContentDto? Content, string Body, PlatformPartyDto Platform, string? SourceFileName, string? ImportNotes, int? SourceTemplateId,
    string? SourceTemplateName, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, DateTime? ActivatedAtUtc, int? CreatedByAdminId,
    int UsedByVersions, string RowVersion);

/// <summary>Một placeholder dùng được trong mẫu. Block = phải đứng riêng một dòng (khối nhiều dòng).</summary>
public sealed record ContractPlaceholderDto(string Key, string Group, string Description, string Sample, bool Required, bool Block);

// ===== PORT (ghi) =====

public interface IContractTemplateRepository
{
    /// <summary>Mẫu có tracking.</summary>
    Task<ContractTemplate?> FindTrackedAsync(int id, CancellationToken cancellationToken);

    /// <summary>Mẫu Active mới nhất (lấy Bên A mặc định cho mẫu mới), null nếu không có.</summary>
    Task<ContractTemplate?> FindLatestActiveAsync(CancellationToken cancellationToken);

    /// <summary>Tên đã dùng bởi mẫu Draft / Active khác (không phân biệt hoa thường).</summary>
    Task<bool> NameInUseAsync(string name, int? excludeId, CancellationToken cancellationToken);

    Task<int> MaxVersionAsync(CancellationToken cancellationToken);

    void Add(ContractTemplate template);

    void Remove(ContractTemplate template);

    /// <summary>Ghi thay đổi + audit log trong CÙNG 1 lần SaveChanges. RowVersion lệch / trùng phiên bản → ConflictException.</summary>
    Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken);
}

// ===== PORT: file DOCX (đọc chữ, điền placeholder) =====

/// <summary>Một đoạn của DOCX: chữ (ô bảng nối bằng " | ") + style Word (VD "Heading1") để đoán tiêu đề.</summary>
public sealed record DocxParagraph(string Text, string? Style, bool IsTableRow);

public interface IDocxDocumentService
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>Đọc các đoạn của word/document.xml. Không phải DOCX hợp lệ → ValidationException.</summary>
    IReadOnlyList<DocxParagraph> ReadParagraphs(byte[] docx);

    /// <summary>Bản sao DOCX với {{Key}} được thay bằng giá trị (kể cả placeholder bị Word tách thành nhiều run).</summary>
    byte[] FillPlaceholders(byte[] docx, IReadOnlyDictionary<string, string> values);
}

// ===== PORT: AI phân tích hợp đồng =====

/// <summary>Kết quả phân tích file hợp đồng: nội dung dạng form, Bên A (nếu đọc được) và cảnh báo cho Admin.</summary>
public sealed record ContractAnalysis(TemplateContent Content, PlatformParty? Platform, IReadOnlyList<string> Warnings);

/// <summary>
/// AI đọc chữ của hợp đồng và chuyển sang nội dung dạng form, thay giá trị Bên B / bãi / phí / ngày bằng placeholder.
/// Lỗi gọi AI → ném ContractAnalysisException để use case chuyển sang bộ tách không AI.
/// </summary>
public interface IContractDocumentAnalyzer
{
    /// <summary>Đã cấu hình (ContractAi:Enabled + API key).</summary>
    bool IsEnabled { get; }

    Task<ContractAnalysis> AnalyzeAsync(IReadOnlyList<DocxParagraph> paragraphs, IReadOnlyList<ContractPlaceholderDto> catalog,
        CancellationToken cancellationToken);
}

public sealed class ContractAnalysisException(string message, Exception? inner = null) : Exception(message, inner);

// ===== PORT (đọc) =====

public interface IContractTemplateQueries
{
    /// <summary>Mới nhất trước.</summary>
    Task<IReadOnlyList<ContractTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken);

    Task<ContractTemplateDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken);
}
