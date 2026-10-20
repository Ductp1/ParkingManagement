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
/// <c># Tiêu đề</c> · <c>## Điều</c> · <c>### Mục</c> · <c>^ dòng căn giữa</c> · <c>- gạch đầu dòng</c> ·
/// <c>|| nhãn: || giá trị</c> (các dòng liền nhau thành bảng thông tin 2 cột) · <c>!! trái !! phải</c> (khối chữ ký, căn giữa) ·
/// <c>---</c> sang trang · dòng trống = khoảng cách ·
/// dòng khác = đoạn văn.
/// </para>
/// </summary>
public sealed partial class HtmlContractRenderer : IContractRenderer
{
    public const string ContentType = "text/html; charset=utf-8";

    /// <summary>Giữ nguyên chữ tiếng Việt (UTF-8), chỉ mã hóa ký tự đặc biệt của HTML.</summary>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public RenderedContract Render(string templateBody, int templateVersion, IReadOnlyDictionary<string, string> values)
    {
        var html = new StringBuilder();
        var open = Block.None;

        foreach (var raw in templateBody.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            var block = KindOf(line);
            if (block != open)
            {
                Close(html, open);
                if (block == Block.List) html.Append("<ul>\n");
                if (block == Block.Table) html.Append("<table class=\"cols\">\n");
                if (block == Block.Sign) html.Append("<table class=\"sign\">\n");
                open = block is Block.List or Block.Table or Block.Sign ? block : Block.None;
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
                case Block.List:
                    html.Append("<li>").Append(Fill(line[2..], values)).Append("</li>\n");
                    break;
                case Block.Table:
                case Block.Sign:
                    var cells = line[2..].Split(block == Block.Table ? "||" : "!!");
                    var pair = cells.Length == 1 ? new[] { cells[0], string.Empty } : cells[..2];
                    html.Append("<tr>");
                    foreach (var cell in pair)
                        html.Append("<td>").Append(Fill(cell.Trim(), values)).Append("</td>");
                    html.Append("</tr>\n");
                    break;
                default:
                    html.Append("<p>").Append(Fill(line, values)).Append("</p>\n");
                    break;
            }
        }
        Close(html, open);

        var title = Encoder.Encode(
            $"Hợp đồng {Value(values, "ContractNumber")} – phiên bản {Value(values, "Version")}");
        var document = $$"""
            <!doctype html>
            <html lang="vi">
            <head>
            <meta charset="utf-8">
            <title>{{title}}</title>
            <style>
            @page { size: A4; margin: 20mm 18mm; }
            body { font-family: "Times New Roman", "Noto Serif", serif; font-size: 13pt; max-width: 794px; margin: 32px auto; padding: 0 40px; line-height: 1.5; color: #111; text-align: justify; }
            h1 { font-size: 16pt; text-align: center; text-transform: uppercase; margin: 16px 0 4px; }
            h2 { font-size: 13pt; margin: 18px 0 6px; }
            h3 { font-size: 13pt; font-style: italic; margin: 10px 0 4px; }
            p { margin: 4px 0; }
            p.center { text-align: center; }
            ul { margin: 4px 0 4px 24px; padding: 0; }
            li { margin: 2px 0; }
            table.cols { width: 100%; border-collapse: collapse; margin: 6px 0; }
            table.cols td { vertical-align: top; padding: 2px 8px 2px 0; text-align: left; }
            table.cols td:first-child { width: 34%; }
            table.sign { width: 100%; border-collapse: collapse; margin: 24px 0 8px; text-align: center; }
            table.sign td { width: 50%; vertical-align: top; padding: 2px 8px; }
            table.sign tr:first-child td { font-weight: bold; }
            table.sign td:empty { height: 90px; }
            .gap { height: 10px; }
            .page-break { break-after: page; height: 24px; }
            footer { margin-top: 40px; font-size: 9pt; color: #555; border-top: 1px solid #ccc; padding-top: 6px; text-align: left; }
            @media print { body { margin: 0; padding: 0; max-width: none; } footer { position: fixed; bottom: 0; left: 0; right: 0; } }
            </style>
            </head>
            <body>
            <main>
            {{html}}</main>
            <footer>Mẫu hợp đồng v{{templateVersion}} · {{title}}</footer>
            </body>
            </html>
            """;
        return new RenderedContract(Encoding.UTF8.GetBytes(document), ContentType, ".html");
    }

    private enum Block { None, Blank, PageBreak, Title, Article, Section, Center, List, Table, Sign, Paragraph }

    private static Block KindOf(string line) => line switch
    {
        "" => Block.Blank,
        "---" => Block.PageBreak,
        _ when line.StartsWith("### ", StringComparison.Ordinal) => Block.Section,
        _ when line.StartsWith("## ", StringComparison.Ordinal) => Block.Article,
        _ when line.StartsWith("# ", StringComparison.Ordinal) => Block.Title,
        _ when line.StartsWith("^ ", StringComparison.Ordinal) => Block.Center,
        _ when line.StartsWith("- ", StringComparison.Ordinal) => Block.List,
        _ when line.StartsWith("||", StringComparison.Ordinal) => Block.Table,
        _ when line.StartsWith("!!", StringComparison.Ordinal) => Block.Sign,
        _ => Block.Paragraph,
    };

    private static void Close(StringBuilder html, Block open)
    {
        if (open == Block.List) html.Append("</ul>\n");
        if (open is Block.Table or Block.Sign) html.Append("</table>\n");
    }

    /// <summary>Encode phần chữ của mẫu rồi thay placeholder bằng giá trị đã encode.</summary>
    private static string Fill(string text, IReadOnlyDictionary<string, string> values)
        => Placeholder().Replace(Encoder.Encode(text),
            m => values.TryGetValue(m.Groups[1].Value, out var value) ? Encoder.Encode(value) : m.Value);

    private static string Value(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) ? value : string.Empty;

    [GeneratedRegex(@"\{\{([A-Za-z]+)\}\}")]
    private static partial Regex Placeholder();
}
