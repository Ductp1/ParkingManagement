using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.RegularExpressions;
using AdminService.Application.Features.PartnerOnboarding;

namespace AdminService.Infrastructure.Documents;

/// <summary>
/// US-116: sinh hợp đồng dạng HTML (UTF-8, đủ dấu tiếng Việt, khổ A4 khi in ra PDF từ trình duyệt) – không cần package PDF.
/// Nội dung mẫu và mọi giá trị đều được HTML-encode trước khi ghép nên dữ liệu đối tác không chèn được mã HTML / script.
/// Placeholder không có giá trị được giữ nguyên để dễ phát hiện mẫu sai.
/// <para>
/// Cú pháp mẫu (mỗi dòng một khối):
/// <c># Tiêu đề</c> · <c>## Điều</c> · <c>### Mục</c> · <c>^ dòng căn giữa</c> · <c>~ dòng in nghiêng</c> · <c>- gạch đầu dòng</c> ·
/// <c>|| nhãn: || giá trị</c> (bảng thông tin 2 cột) · <c>!! trái !! phải</c> (khối 2 cột căn giữa, dòng đầu đậm – quốc hiệu, chữ ký) ·
/// <c>#| cột 1 | cột 2 | …</c> dòng tiêu đề + <c>| ô 1 | ô 2 | …</c> dòng dữ liệu (bảng có khung) · <c>---</c> sang trang ·
/// dòng trống = khoảng cách · dòng bắt đầu "1.2." = khoản (thụt lề) · dòng khác = đoạn văn. Trong dòng: <c>**chữ đậm**</c>.
/// Dòng chỉ có <c>{{Key}}</c> = khối chèn nhiều dòng (xem <see cref="AppendInserted"/>).
/// Dòng bắt đầu bằng <c>\</c> = đoạn văn nguyên văn (không đọc cú pháp đầu dòng) – dùng cho chữ Admin nhập trên form.
/// </para>
/// </summary>
public sealed partial class HtmlContractRenderer : IContractRenderer
{
    public const string ContentType = "text/html; charset=utf-8";

    /// <summary>Giữ nguyên chữ tiếng Việt (UTF-8), chỉ mã hóa ký tự đặc biệt của HTML.</summary>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public RenderedContract Render(string templateBody, int templateVersion, IReadOnlyDictionary<string, string> values,
        bool previewWatermark)
    {
        var html = new StringBuilder();
        var open = Block.None;

        foreach (var raw in templateBody.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (StandalonePlaceholder().Match(line) is { Success: true } inserted
                && values.TryGetValue(inserted.Groups[1].Value, out var insertedText))
            {
                Close(html, open);
                open = Block.None;
                AppendInserted(html, insertedText);
                continue;
            }

            var block = KindOf(line);
            var group = block == Block.GridHeader ? Block.Grid : block;
            if (group != open)
            {
                Close(html, open);
                html.Append(group switch
                {
                    Block.List => "<ul>\n",
                    Block.Table => "<table class=\"cols\">\n",
                    Block.Sign => "<table class=\"sign\">\n",
                    Block.Grid => "<table class=\"grid\">\n",
                    _ => string.Empty,
                });
                open = group is Block.List or Block.Table or Block.Sign or Block.Grid ? group : Block.None;
            }

            switch (block)
            {
                case Block.Blank:
                    html.Append("<div class=\"gap\"></div>\n");
                    break;
                case Block.PageBreak:
                    html.Append("<div class=\"page-break\"></div>\n");
                    break;
                case Block.Title:
                    html.Append("<h1>").Append(Fill(line[2..], values)).Append("</h1>\n");
                    break;
                case Block.Article:
                    html.Append("<h2>").Append(Fill(line[3..], values)).Append("</h2>\n");
                    break;
                case Block.Section:
                    html.Append("<h3>").Append(Fill(line[4..], values)).Append("</h3>\n");
                    break;
                case Block.Center:
                    html.Append("<p class=\"center\">").Append(Fill(line[2..], values)).Append("</p>\n");
                    break;
                case Block.Italic:
                    html.Append("<p class=\"italic\">").Append(Fill(line[2..], values)).Append("</p>\n");
                    break;
                case Block.List:
                    html.Append("<li>").Append(Fill(line[2..], values)).Append("</li>\n");
                    break;
                case Block.Table:
                case Block.Sign:
                    var cells = line[2..].Split(block == Block.Table ? "||" : "!!");
                    var pair = cells.Length == 1 ? new[] { cells[0], string.Empty } : cells[..2];
                    AppendRow(html, pair, "td", values);
                    break;
                case Block.GridHeader:
                    AppendRow(html, line[2..].Split('|'), "th", values);
                    break;
                case Block.Grid:
                    AppendRow(html, line[1..].Split('|'), "td", values);
                    break;
                case Block.Literal:
                    html.Append("<p>").Append(Fill(line[1..], values)).Append("</p>\n");
                    break;
                case Block.Clause:
                    html.Append("<p class=\"clause\">").Append(Fill(line, values)).Append("</p>\n");
                    break;
                default:
                    html.Append("<p>").Append(Fill(line, values)).Append("</p>\n");
                    break;
            }
        }
        Close(html, open);

        var contractNumber = Encoder.Encode(Value(values, "ContractNumber"));
        var version = Encoder.Encode(Value(values, "Version"));
        var title = $"Hợp đồng {contractNumber} – phiên bản {version}";
        var watermark = previewWatermark
            ? "<div class=\"watermark\" aria-hidden=\"true\">BẢN XEM TRƯỚC<br>KHÔNG CÓ GIÁ TRỊ PHÁP LÝ</div>\n"
            : string.Empty;
        var document = $$"""
            <!doctype html>
            <html lang="vi">
            <head>
            <meta charset="utf-8">
            <title>{{title}}</title>
            <style>
            @page { size: A4; margin: 20mm 18mm 24mm; }
            body { font-family: "Times New Roman", "Noto Serif", serif; font-size: 13pt; max-width: 794px; margin: 32px auto; padding: 0 40px; line-height: 1.5; color: #111; text-align: justify; }
            h1 { font-size: 16pt; text-align: center; text-transform: uppercase; margin: 16px 0 2px; }
            h2 { font-size: 13pt; text-transform: uppercase; margin: 16px 0 6px; }
            h3 { font-size: 13pt; margin: 10px 0 4px; }
            p { margin: 4px 0; }
            p.center { text-align: center; margin: 2px 0; }
            p.italic { font-style: italic; }
            p.clause { margin: 4px 0 4px 28px; }
            ul { margin: 4px 0 4px 24px; padding: 0; }
            li { margin: 2px 0; }
            table { width: 100%; border-collapse: collapse; margin: 6px 0; }
            table.cols td { vertical-align: top; padding: 2px 8px 2px 0; text-align: left; }
            table.cols td:first-child { width: 30%; }
            table.sign { text-align: center; }
            table.sign td { width: 50%; vertical-align: top; padding: 2px 8px; }
            table.sign tr:first-child td { font-weight: bold; }
            table.sign td:empty { height: 90px; }
            table.grid { margin: 8px 0; text-align: left; }
            table.grid th, table.grid td { border: 1px solid #333; padding: 4px 6px; vertical-align: top; font-size: 12pt; }
            table.grid th { background: #f0f0f0; }
            .gap { height: 8px; }
            .page-break { break-after: page; height: 24px; }
            .watermark { position: fixed; top: 40%; left: 0; right: 0; text-align: center; font-size: 44pt; font-weight: bold; color: rgba(180, 0, 0, 0.08); transform: rotate(-30deg); pointer-events: none; line-height: 1.3; }
            footer { margin-top: 40px; font-size: 9pt; color: #555; border-top: 1px solid #ccc; padding-top: 6px; text-align: left; }
            @media print { body { margin: 0; padding: 0; max-width: none; } footer { position: fixed; bottom: -14mm; left: 0; right: 0; } }
            </style>
            </head>
            <body>
            {{watermark}}<main>
            {{html}}</main>
            <footer>Hợp đồng số {{contractNumber}} · Phiên bản {{version}} · Mẫu hợp đồng v{{templateVersion}}<br>Ký nháy: Bên A ……… Bên B ………</footer>
            </body>
            </html>
            """;
        return new RenderedContract(Encoding.UTF8.GetBytes(document), ContentType, ".html");
    }

    private enum Block { None, Blank, PageBreak, Title, Article, Section, Center, Italic, List, Table, Sign, GridHeader, Grid, Clause, Literal, Paragraph }

    private static Block KindOf(string line) => line switch
    {
        "" => Block.Blank,
        "---" => Block.PageBreak,
        _ when line.StartsWith("### ", StringComparison.Ordinal) => Block.Section,
        _ when line.StartsWith("## ", StringComparison.Ordinal) => Block.Article,
        _ when line.StartsWith("#|", StringComparison.Ordinal) => Block.GridHeader,
        _ when line.StartsWith("# ", StringComparison.Ordinal) => Block.Title,
        _ when line.StartsWith("^ ", StringComparison.Ordinal) => Block.Center,
        _ when line.StartsWith("~ ", StringComparison.Ordinal) => Block.Italic,
        _ when line.StartsWith("- ", StringComparison.Ordinal) => Block.List,
        _ when line.StartsWith("||", StringComparison.Ordinal) => Block.Table,
        _ when line.StartsWith("!!", StringComparison.Ordinal) => Block.Sign,
        _ when line.StartsWith('|') => Block.Grid,
        _ when line.StartsWith('\\') => Block.Literal,
        _ when ClausePrefix().IsMatch(line) => Block.Clause,
        _ => Block.Paragraph,
    };

    private static void AppendRow(StringBuilder html, IEnumerable<string> cells, string tag, IReadOnlyDictionary<string, string> values)
    {
        html.Append("<tr>");
        foreach (var cell in cells)
            html.Append('<').Append(tag).Append('>').Append(Fill(cell.Trim(), values)).Append("</").Append(tag).Append('>');
        html.Append("</tr>\n");
    }

    /// <summary>
    /// Khối chèn (dòng mẫu chỉ có <c>{{Key}}</c>, VD thỏa thuận riêng): mỗi dòng của giá trị là một đoạn, dòng "1.2." thụt lề như khoản.
    /// Chỉ encode, không đọc cú pháp mẫu – chữ Admin nhập không tạo được tiêu đề, bảng hay chữ đậm.
    /// </summary>
    private static void AppendInserted(StringBuilder html, string text)
    {
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                html.Append("<div class=\"gap\"></div>\n");
                continue;
            }
            html.Append(ClausePrefix().IsMatch(line) ? "<p class=\"clause\">" : "<p>").Append(Encoder.Encode(line)).Append("</p>\n");
        }
    }

    private static void Close(StringBuilder html, Block open)
    {
        if (open == Block.List) html.Append("</ul>\n");
        if (open is Block.Table or Block.Sign or Block.Grid) html.Append("</table>\n");
    }

    /// <summary>
    /// Encode chữ của mẫu, đổi **đậm** của mẫu thành &lt;strong&gt;, RỒI mới thay placeholder bằng giá trị đã encode –
    /// dữ liệu đối tác có dấu ** cũng không thành định dạng.
    /// </summary>
    private static string Fill(string text, IReadOnlyDictionary<string, string> values)
    {
        var encoded = Bold().Replace(Encoder.Encode(text), "<strong>$1</strong>");
        return Placeholder().Replace(encoded,
            m => values.TryGetValue(m.Groups[1].Value, out var value) ? Encoder.Encode(value) : m.Value);
    }

    private static string Value(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) ? value : string.Empty;

    [GeneratedRegex(@"\{\{([A-Za-z]+)\}\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"^\s*\{\{([A-Za-z]+)\}\}$")]
    private static partial Regex StandalonePlaceholder();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"^\d+\.\d+\.\s")]
    private static partial Regex ClausePrefix();
}
