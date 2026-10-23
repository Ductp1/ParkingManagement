using System.Text.RegularExpressions;
using AdminService.Domain.ValueObjects;

namespace AdminService.Application.Features.ContractTemplates;

/// <summary>
/// US-116 v11: tách hợp đồng từ chữ của file DOCX mà không dùng AI – "Điều N." / style Heading = điều, "n.n." = khoản,
/// "Căn cứ …" trước điều đầu = căn cứ pháp lý, "Phụ lục …" sau các điều = phụ lục. Thông tin hai bên (do hệ thống dựng từ
/// placeholder) bị bỏ qua. Không tự thay giá trị Bên B / bãi / phí bằng placeholder – cảnh báo Admin kiểm tra lại.
/// </summary>
public static partial class PlainTextContractParser
{
    public const string SpecialTermsTitle = "Thỏa thuận riêng";

    public static ContractAnalysis Parse(IReadOnlyList<DocxParagraph> paragraphs)
    {
        var warnings = new List<string>
        {
            "Bộ tách không dùng AI: tên đối tác, bãi, phí, ngày… trong file vẫn là chữ cố định – hãy thay bằng trường (Insert field) trước khi áp dụng.",
        };
        string? title = null, subtitle = null;
        var legalBases = new List<string>();
        var intro = new List<string>();
        var articles = new List<(string Title, List<string> Clauses)>();
        var appendices = new List<(string Title, List<string> Notes)>();
        var signNote = (string?)null;

        foreach (var p in paragraphs)
        {
            var text = p.Text.Trim();
            if (text.Length == 0) continue;

            if (appendices.Count == 0 && articles.Count > 0 && AppendixHeading().IsMatch(text))
            {
                appendices.Add((text, []));
                continue;
            }
            if (appendices.Count > 0)
            {
                if (AppendixHeading().IsMatch(text)) appendices.Add((text, []));
                else appendices[^1].Notes.Add(text);
                continue;
            }

            var article = ArticleHeading().Match(text);
            if (article.Success || (p.Style?.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) == true && articles.Count > 0))
            {
                var heading = article.Success ? article.Groups[1].Value.Trim() : text;
                articles.Add((heading.Length == 0 ? $"Điều {articles.Count + 1}" : heading, []));
                continue;
            }

            if (articles.Count > 0)
            {
                if (SignatureLine().IsMatch(text)) { signNote ??= text.Contains('(') ? text[text.IndexOf('(')..] : null; continue; }
                var clause = ClausePrefix().Match(text);
                if (clause.Success) articles[^1].Clauses.Add(text[clause.Length..].Trim());
                else if (articles[^1].Clauses.Count > 0) articles[^1].Clauses[^1] += "\n" + text;
                else articles[^1].Clauses.Add(text);
                continue;
            }

            // Trước điều đầu tiên: quốc hiệu, tên hợp đồng, căn cứ, thông tin hai bên, lời dẫn.
            if (NationalHeader().IsMatch(text) || PartyLine().IsMatch(text) || p.IsTableRow) continue;
            if (title is null && text.Contains("HỢP ĐỒNG", StringComparison.Ordinal) && !text.StartsWith("Số", StringComparison.Ordinal))
            {
                title = text;
                continue;
            }
            if (title is not null && subtitle is null && legalBases.Count == 0 && intro.Count == 0 && text == text.ToUpperInvariant()
                && text.Any(char.IsLetter))
            {
                subtitle = text;
                continue;
            }
            if (LegalBasis().IsMatch(text)) { legalBases.Add(text.TrimStart('–', '-', '•', ' ')); continue; }
            if (text.StartsWith("Hôm nay", StringComparison.OrdinalIgnoreCase)) continue;
            intro.Add(text);
        }

        if (title is null) warnings.Add("Không tìm thấy tên hợp đồng – đã đặt tên mặc định.");
        if (articles.Count == 0) warnings.Add("Không nhận ra điều khoản nào (dòng \"Điều 1. …\").");

        var parsedArticles = articles.Select(a => new TemplateArticle(a.Title, a.Clauses)).ToList();
        if (articles.Count > 0 && !parsedArticles.Any(a => a.Title.Contains(SpecialTermsTitle, StringComparison.OrdinalIgnoreCase)))
        {
            parsedArticles.Add(new TemplateArticle(SpecialTermsTitle,
                ["Các thỏa thuận dưới đây chỉ áp dụng cho Bãi đỗ xe tại phụ lục và được ưu tiên áp dụng nếu khác với các điều khoản khác của Hợp đồng:"],
                IsSpecialTerms: true));
            warnings.Add("Đã thêm điều \"Thỏa thuận riêng\" (in thỏa thuận riêng của từng bãi) vào cuối các điều – có thể di chuyển.");
        }
        else
        {
            parsedArticles = parsedArticles
                .Select(a => a.Title.Contains(SpecialTermsTitle, StringComparison.OrdinalIgnoreCase) ? a with { IsSpecialTerms = true } : a)
                .ToList();
        }

        var parsedAppendices = appendices.Select(a => new TemplateAppendix(TemplateAppendixKind.Text, a.Title, null, a.Notes)).ToList();
        if (articles.Count > 0) AddSystemAppendices(parsedAppendices, warnings);

        var content = new TemplateContent(
            new TemplateHeader("TP. Hồ Chí Minh", title ?? "Hợp đồng hợp tác khai thác bãi đỗ xe", subtitle),
            legalBases,
            intro.Count == 0 ? null : string.Join('\n', intro),
            parsedArticles,
            parsedAppendices,
            new TemplateSignature(signNote),
            null);
        return new ContractAnalysis(content, null, warnings);
    }

    /// <summary>Bảo đảm có phụ lục bảng bãi đỗ và tài khoản nhận thanh toán (hệ thống điền dữ liệu).</summary>
    public static void AddSystemAppendices(List<TemplateAppendix> appendices, List<string> warnings)
    {
        if (!appendices.Any(a => a.Kind == TemplateAppendixKind.LotInfo))
        {
            var i = appendices.FindIndex(a => a.Title.Contains("bãi", StringComparison.OrdinalIgnoreCase));
            if (i >= 0) appendices[i] = appendices[i] with { Kind = TemplateAppendixKind.LotInfo };
            else appendices.Add(new TemplateAppendix(TemplateAppendixKind.LotInfo, $"Phụ lục {appendices.Count + 1:00}", "BÃI ĐỖ XE HỢP TÁC", []));
            warnings.Add("Phụ lục bãi đỗ xe dùng bảng hệ thống (tên, địa chỉ, sức chứa, giờ hoạt động của bãi).");
        }
        if (!appendices.Any(a => a.Kind == TemplateAppendixKind.PayoutAccount))
        {
            var i = appendices.FindIndex(a => a.Title.Contains("tài khoản", StringComparison.OrdinalIgnoreCase)
                                              || a.Notes.Any(n => n.Contains("tài khoản", StringComparison.OrdinalIgnoreCase)));
            if (i >= 0 && appendices[i].Kind == TemplateAppendixKind.Text) appendices[i] = appendices[i] with { Kind = TemplateAppendixKind.PayoutAccount };
            else appendices.Add(new TemplateAppendix(TemplateAppendixKind.PayoutAccount, $"Phụ lục {appendices.Count + 1:00}",
                "THÔNG TIN TÀI KHOẢN NHẬN THANH TOÁN", []));
            warnings.Add("Phụ lục tài khoản nhận thanh toán dùng bảng hệ thống (chủ tài khoản, số tài khoản, ngân hàng).");
        }
    }

    [GeneratedRegex(@"^ĐIỀU\s+\d+\s*[.:\-–]?\s*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ArticleHeading();

    [GeneratedRegex(@"^PHỤ LỤC\b", RegexOptions.IgnoreCase)]
    private static partial Regex AppendixHeading();

    [GeneratedRegex(@"^\d+\.\d+\.?\s+")]
    private static partial Regex ClausePrefix();

    [GeneratedRegex(@"^[-–•\s]*Căn cứ\b", RegexOptions.IgnoreCase)]
    private static partial Regex LegalBasis();

    [GeneratedRegex(@"CỘNG HÒA XÃ HỘI|Độc lập\s*[-–]\s*Tự do", RegexOptions.IgnoreCase)]
    private static partial Regex NationalHeader();

    [GeneratedRegex(@"^(BÊN [AB]\b|Địa chỉ|Mã số|MST|Điện thoại|Email|Người đại diện|Đại diện|Chức vụ|Tài khoản|Số tài khoản|Ngân hàng|\(Sau đây)", RegexOptions.IgnoreCase)]
    private static partial Regex PartyLine();

    [GeneratedRegex(@"^ĐẠI DIỆN BÊN|\(Ký,", RegexOptions.IgnoreCase)]
    private static partial Regex SignatureLine();
}
