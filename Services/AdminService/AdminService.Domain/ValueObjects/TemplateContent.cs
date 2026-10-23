namespace AdminService.Domain.ValueObjects;

/// <summary>
/// [AdminService] Nội dung mẫu hợp đồng dạng form (US-116 v11): Admin điền từng phần trên giao diện, không gõ cú pháp markup.
/// Số khoản "n.m." do hệ thống đánh; phần Bên A / Bên B, quốc hiệu và bảng phụ lục do hệ thống dựng. Lưu dạng JSON (ContentJson).
/// </summary>
public sealed record TemplateContent(
    TemplateHeader Header,
    IReadOnlyList<string> LegalBases,
    string? Introduction,
    IReadOnlyList<TemplateArticle> Articles,
    IReadOnlyList<TemplateAppendix> Appendices,
    TemplateSignature Signature,
    string? Footnote);

/// <summary>Đầu hợp đồng: nơi ký ("TP. Hồ Chí Minh"), tên hợp đồng, dòng phụ đề (in đậm, căn giữa).</summary>
public sealed record TemplateHeader(string PlaceOfSigning, string Title, string? Subtitle);

/// <summary>Một điều: tiêu đề ("PHẠM VI HỢP TÁC") + các khoản. IsSpecialTerms = điều in thỏa thuận riêng của từng bãi.</summary>
public sealed record TemplateArticle(string Title, IReadOnlyList<string> Clauses, bool IsSpecialTerms = false);

/// <summary>Phụ lục: LotInfo / PayoutAccount có bảng do hệ thống dựng; Text chỉ gồm các đoạn ghi chú.</summary>
public sealed record TemplateAppendix(TemplateAppendixKind Kind, string Title, string? Subtitle, IReadOnlyList<string> Notes);

public enum TemplateAppendixKind
{
    LotInfo = 0,
    PayoutAccount = 1,
    Text = 2,
}

/// <summary>Khối chữ ký: dòng ghi chú dưới "ĐẠI DIỆN BÊN A / B" (mặc định "(Ký, ghi rõ họ tên, đóng dấu)").</summary>
public sealed record TemplateSignature(string? SignNote);
