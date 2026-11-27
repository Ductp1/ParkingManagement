using System.Text.RegularExpressions;
using AdminService.Domain.ValueObjects;

namespace AdminService.Application.Features.ContractTemplates;

/// <summary>
/// US-116 v11: đọc mẫu markup cũ (v1–v4, theo cú pháp HtmlContractRenderer) thành nội dung dạng form – dùng để seed mẫu chuẩn từ v4
/// và cho nút "Convert to form". Phần do hệ thống dựng (quốc hiệu, khối Bên A / Bên B, bảng phụ lục, chữ ký) được bỏ qua.
/// Dòng không phải khoản trong một điều (VD công thức căn giữa) được nối vào khoản trước như dòng tiếp theo.
/// </summary>
public static partial class ContractMarkupParser
{
    public static TemplateContent Parse(string markup)
    {
        var place = "TP. Hồ Chí Minh";
        string? title = null, subtitle = null, footnote = null, signNote = null;
        var legalBases = new List<string>();
        var intro = new List<string>();
        var articles = new List<(string Title, List<string> Clauses, bool Special)>();
        var appendices = new List<(string Title, string? Subtitle, TemplateAppendixKind Kind, List<string> Notes)>();
        var section = Section.Header;

        foreach (var raw in markup.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line == "---") continue;

            if (line.StartsWith("!!", StringComparison.Ordinal))
            {
                var cells = line[2..].Split("!!").Select(c => c.Trim()).ToArray();
                if (section == Section.Header && cells.Length > 1 && PlaceCell().Match(cells[1]) is { Success: true } m)
                    place = m.Groups[1].Value.Trim();
                if (section is Section.Articles or Section.Signature)
                {
                    section = Section.Signature;
                    if (signNote is null && cells[0].StartsWith('(')) signNote = cells[0];
                }
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                var text = line[2..].Trim();
                if (title is null) { title = text; section = Section.TitleBlock; }
                else
                {
                    appendices.Add((text, null, TemplateAppendixKind.Text, []));
                    section = Section.Appendix;
                }
                continue;
            }

            if (line.StartsWith("^ ", StringComparison.Ordinal))
            {
                var text = StripBold(line[2..].Trim());
                if (text.Contains("{{VersionNote}}") || text.StartsWith("(Kèm theo", StringComparison.Ordinal)) continue;
                if (section == Section.TitleBlock && subtitle is null) { subtitle = text; continue; }
                if (section == Section.Appendix && appendices[^1].Subtitle is null)
                {
                    appendices[^1] = appendices[^1] with { Subtitle = text };
                    continue;
                }
                AddText(text);
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                var heading = line[3..].Trim();
                if (heading.StartsWith("BÊN A", StringComparison.OrdinalIgnoreCase) || heading.StartsWith("BÊN B", StringComparison.OrdinalIgnoreCase))
                {
                    section = Section.Parties;
                    continue;
                }
                articles.Add((ArticlePrefix().Replace(heading, "").Trim(), [], false));
                section = Section.Articles;
                continue;
            }

            if (line.StartsWith("~ ", StringComparison.Ordinal))
            {
                var text = line[2..].Trim();
                if (section is Section.Header or Section.TitleBlock) legalBases.Add(text.TrimStart('–', '-', ' '));
                else if (section == Section.Parties) { /* "(Sau đây gọi là …)" – do hệ thống dựng */ }
                else if (section is Section.Appendix or Section.Signature) footnote = footnote is null ? text : $"{footnote}\n{text}";
                else AddText(text);
                continue;
            }

            if (line.StartsWith("||", StringComparison.Ordinal)) continue;   // bảng Bên A / Bên B

            if (line.StartsWith('|') || line.StartsWith("#|", StringComparison.Ordinal))
            {
                if (section != Section.Appendix) { AddText(StripBold(line.TrimStart('#', '|').Replace("|", " – ").Trim())); continue; }
                var a = appendices[^1];
                if (line.Contains("{{LotName}}") || line.StartsWith("#|", StringComparison.Ordinal))
                {
                    if (line.Contains("{{LotName}}")) appendices[^1] = a with { Kind = TemplateAppendixKind.LotInfo };
                    continue;
                }
                if (line.Contains("{{Bank")) { appendices[^1] = a with { Kind = TemplateAppendixKind.PayoutAccount }; continue; }
                var cells = line.TrimStart('|').Split('|').Select(c => StripBold(c.Trim())).Where(c => c.Length > 0).ToArray();
                a.Notes.Add(cells.Length > 1 ? $"{cells[0]}: {string.Join(" – ", cells[1..])}" : string.Join(" ", cells));
                continue;
            }

            var paragraph = line.StartsWith('\\') ? line[1..] : line.StartsWith("- ", StringComparison.Ordinal) ? "– " + line[2..] : line;
            if (paragraph.StartsWith("Hôm nay,", StringComparison.Ordinal) && paragraph.Contains("{{IssuedDateLong}}")) { section = Section.Parties; continue; }
            if (section == Section.Articles)
            {
                if (paragraph.Trim() == $"{{{{{ContractPlaceholders.SpecialTermsKey}}}}}")
                {
                    articles[^1] = articles[^1] with { Special = true };
                    continue;
                }
                var clause = ClausePrefix().Match(paragraph);
                if (clause.Success && !articles[^1].Special) articles[^1].Clauses.Add(paragraph[clause.Length..].Trim());
                else AddText(paragraph);
                continue;
            }
            AddText(paragraph);
        }

        var content = new TemplateContent(
            new TemplateHeader(place, title ?? "Hợp đồng hợp tác khai thác bãi đỗ xe", subtitle),
            legalBases,
            intro.Count == 0 ? null : string.Join('\n', intro),
            articles.Select(a => new TemplateArticle(a.Title, a.Clauses, a.Special)).ToList(),
            appendices.Select(a => new TemplateAppendix(a.Kind, a.Title, a.Subtitle, a.Notes)).ToList(),
            new TemplateSignature(signNote),
            footnote);
        return content;

        void AddText(string text)
        {
            switch (section)
            {
                case Section.Articles when articles[^1].Clauses.Count > 0 && !articles[^1].Special:
                    articles[^1].Clauses[^1] += "\n" + text;
                    break;
                case Section.Articles:
                    articles[^1].Clauses.Add(text);
                    break;
                case Section.Appendix:
                    appendices[^1].Notes.Add(text);
                    break;
                case Section.Parties or Section.TitleBlock or Section.Header:
                    if (!text.Contains("{{")) intro.Add(text);
                    else if (section == Section.Parties) intro.Add(text);
                    break;
                default:
                    footnote = footnote is null ? text : $"{footnote}\n{text}";
                    break;
            }
        }
    }

    private enum Section { Header, TitleBlock, Parties, Articles, Signature, Appendix }

    private static string StripBold(string text) => text.Replace("**", "");

    [GeneratedRegex(@"^(.+?),\s*\{\{IssuedDateLong\}\}")]
    private static partial Regex PlaceCell();

    [GeneratedRegex(@"^ĐIỀU\s+\d+\s*[.:]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ArticlePrefix();

    [GeneratedRegex(@"^\d+\.\d+\.\s*")]
    private static partial Regex ClausePrefix();
}
