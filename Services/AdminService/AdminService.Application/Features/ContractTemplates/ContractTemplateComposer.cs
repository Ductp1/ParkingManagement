using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdminService.Domain.ValueObjects;

namespace AdminService.Application.Features.ContractTemplates;

/// <summary>
/// US-116 v11: dựng markup (cú pháp của HtmlContractRenderer) từ nội dung mẫu dạng form. Admin không gõ markup:
/// quốc hiệu, khối Bên A / Bên B, số khoản "n.m.", bảng phụ lục và khối chữ ký do hệ thống dựng; chữ Admin nhập luôn đứng sau
/// một tiền tố cố định ("## ", "~ ", "n.m. ") hoặc tiền tố "\" (đoạn văn nguyên văn) nên không thể biến thành tiêu đề / bảng.
/// </summary>
public static class ContractTemplateComposer
{
    public const string DefaultSignNote = "(Ký, ghi rõ họ tên, đóng dấu)";

    public static string ToMarkup(TemplateContent content)
    {
        var lines = new List<string>();
        var place = OneLine(content.Header.PlaceOfSigning);
        var signNote = string.IsNullOrWhiteSpace(content.Signature.SignNote) ? DefaultSignNote : OneLine(content.Signature.SignNote);

        lines.Add("!! {{PlatformName}} !! CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM");
        lines.Add("!! MSDN: {{PlatformTaxCode}} !! **Độc lập – Tự do – Hạnh phúc**");
        lines.Add($"!! Số: {{{{ContractNumber}}}} !! {place}, {{{{IssuedDateLong}}}}");
        lines.Add("");
        lines.Add($"# {OneLine(content.Header.Title)}");
        if (!string.IsNullOrWhiteSpace(content.Header.Subtitle)) lines.Add($"^ **{OneLine(content.Header.Subtitle)}**");
        lines.Add("^ ({{VersionNote}})");
        lines.Add("");
        foreach (var basis in content.LegalBases.Where(b => !string.IsNullOrWhiteSpace(b)))
            lines.Add($"~ – {OneLine(basis).TrimStart('–', '-', ' ')}");
        lines.Add($"Hôm nay, {{{{IssuedDateLong}}}}, tại {place}, chúng tôi gồm:");
        lines.Add("");
        lines.AddRange(PartyBlocks);
        lines.Add("");
        AddParagraphs(lines, content.Introduction);
        lines.Add("");

        for (var i = 0; i < content.Articles.Count; i++)
        {
            var article = content.Articles[i];
            var n = i + 1;
            lines.Add($"## ĐIỀU {n}. {OneLine(article.Title).ToUpperInvariant()}");
            if (article.IsSpecialTerms)
            {
                foreach (var lead in article.Clauses) AddParagraphs(lines, lead);
                lines.Add($"{{{{{ContractPlaceholders.SpecialTermsKey}}}}}");
            }
            else
            {
                for (var j = 0; j < article.Clauses.Count; j++)
                {
                    var parts = SplitLines(article.Clauses[j]);
                    if (parts.Count == 0) continue;
                    lines.Add($"{n}.{j + 1}. {parts[0]}");
                    lines.AddRange(parts.Skip(1).Select(Literal));
                }
            }
            lines.Add("");
        }

        lines.Add("!! ĐẠI DIỆN BÊN A !! ĐẠI DIỆN BÊN B");
        lines.Add($"!! {signNote} !! {signNote}");
        lines.Add("!! !!");
        lines.Add("!! **{{PlatformRepresentativePosition}}** !! **{{LegalRepresentativePosition}}**");
        lines.Add("!! **{{PlatformRepresentativeName}}** !! **{{LegalRepresentativeName}}**");

        for (var i = 0; i < content.Appendices.Count; i++)
        {
            var appendix = content.Appendices[i];
            lines.Add("");
            if (i == 0) lines.Add("---");
            lines.Add($"# {OneLine(appendix.Title)}");
            if (!string.IsNullOrWhiteSpace(appendix.Subtitle)) lines.Add($"^ **{OneLine(appendix.Subtitle)}**");
            lines.Add("^ (Kèm theo Hợp đồng số {{ContractNumber}} – Phiên bản {{Version}})");
            if (appendix.Kind == TemplateAppendixKind.LotInfo)
            {
                lines.Add("#| Tên bãi | Địa chỉ | Sức chứa | Giờ hoạt động | Trạng thái thẩm định");
                lines.Add("| {{LotName}} | {{LotAddress}} | {{LotCapacity}} | {{LotOpeningHours}} | Đã phê duyệt KYB");
            }
            else if (appendix.Kind == TemplateAppendixKind.PayoutAccount)
            {
                lines.Add("| **Tên chủ tài khoản** | {{BankAccountName}}");
                lines.Add("| **Số tài khoản** | {{BankAccountNumber}}");
                lines.Add("| **Ngân hàng** | {{BankName}} – BIN {{BankBin}}");
            }
            foreach (var note in appendix.Notes) AddParagraphs(lines, note);
        }

        if (!string.IsNullOrWhiteSpace(content.Footnote))
        {
            lines.Add("");
            foreach (var line in SplitLines(content.Footnote)) lines.Add($"~ {line}");
        }

        return string.Join('\n', lines);
    }

    /// <summary>Khối Bên A / Bên B – dữ liệu lấy từ placeholder, Admin không soạn.</summary>
    private static readonly string[] PartyBlocks =
    [
        "## BÊN A: {{PlatformName}}",
        "|| Địa chỉ trụ sở: || {{PlatformAddress}}",
        "|| Mã số doanh nghiệp: || {{PlatformTaxCode}}",
        "|| Điện thoại / Email: || {{PlatformPhone}} · {{PlatformEmail}}",
        "|| Người đại diện: || **{{PlatformRepresentativeName}}** – Chức vụ: {{PlatformRepresentativePosition}}",
        "~ (Sau đây gọi là “Bên A” hoặc “Nền tảng”)",
        "",
        "## BÊN B: {{BusinessName}}",
        "|| Địa chỉ trụ sở: || {{BusinessAddress}}",
        "|| Mã số doanh nghiệp: || {{TaxCode}}",
        "|| Điện thoại / Email: || {{Phone}} · {{Email}}",
        "|| Người đại diện: || **{{LegalRepresentativeName}}** – Chức vụ: {{LegalRepresentativePosition}}",
        "|| Tài khoản nhận thanh toán: || Chi tiết tại phụ lục",
        "~ (Sau đây gọi là “Bên B” hoặc “Chủ bãi”)",
    ];

    /// <summary>Đoạn văn nhiều dòng → mỗi dòng một đoạn nguyên văn ("\" + chữ).</summary>
    private static void AddParagraphs(List<string> lines, string? text) => lines.AddRange(SplitLines(text).Select(Literal));

    private static string Literal(string line) => "\\" + line;

    public static List<string> SplitLines(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    private static string OneLine(string? text) => string.Join(' ', SplitLines(text));
}

/// <summary>JSON của TemplateContent (cột ContentJson, DTO): camelCase, enum dạng chuỗi.</summary>
public static class TemplateContentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(TemplateContent content) => JsonSerializer.Serialize(content, Options);

    public static TemplateContent? Deserialize(string? json)
        => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<TemplateContent>(json, Options);
}
