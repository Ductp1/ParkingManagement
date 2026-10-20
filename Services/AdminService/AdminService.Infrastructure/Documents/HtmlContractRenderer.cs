using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.RegularExpressions;
using AdminService.Application.Features.PartnerOnboarding;

namespace AdminService.Infrastructure.Documents;

/// <summary>
/// US-116: sinh hợp đồng dạng HTML (UTF-8, đủ dấu tiếng Việt, in được ra PDF từ trình duyệt) – không cần package PDF.
/// Nội dung mẫu và mọi giá trị đều được HTML-encode trước khi ghép nên dữ liệu đối tác không chèn được mã HTML / script.
/// Placeholder không có giá trị được giữ nguyên để dễ phát hiện mẫu sai.
/// </summary>
public sealed partial class HtmlContractRenderer : IContractRenderer
{
    public const string ContentType = "text/html; charset=utf-8";

    /// <summary>Giữ nguyên chữ tiếng Việt (UTF-8), chỉ mã hóa ký tự đặc biệt của HTML.</summary>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public RenderedContract Render(string templateBody, int templateVersion, IReadOnlyDictionary<string, string> values)
    {
        var paragraphs = new StringBuilder();
        foreach (var raw in templateBody.Replace("\r\n", "\n").Split('\n'))
        {
            // Tách dòng trước khi encode (HtmlEncoder mã hóa cả ký tự xuống dòng).
            var line = Placeholder().Replace(Encoder.Encode(raw),
                m => values.TryGetValue(m.Groups[1].Value, out var value) ? Encoder.Encode(value) : m.Value);
            paragraphs.Append(line.Length == 0 ? "<p>&nbsp;</p>" : $"<p>{line}</p>").Append('\n');
        }

        var title = Encoder.Encode(
            $"Hợp đồng {Value(values, "ContractNumber")} – phiên bản {Value(values, "Version")}");
        var html = $$"""
            <!doctype html>
            <html lang="vi">
            <head>
            <meta charset="utf-8">
            <title>{{title}}</title>
            <style>
            body { font-family: "Inter", "Noto Sans", Arial, sans-serif; max-width: 800px; margin: 40px auto; padding: 0 24px; line-height: 1.6; color: #111; }
            footer { margin-top: 48px; font-size: 12px; color: #555; border-top: 1px solid #ccc; padding-top: 8px; }
            @media print { body { margin: 0; } }
            </style>
            </head>
            <body>
            <main>
            {{paragraphs}}</main>
            <footer>Mẫu hợp đồng v{{templateVersion}}</footer>
            </body>
            </html>
            """;
        return new RenderedContract(Encoding.UTF8.GetBytes(html), ContentType, ".html");
    }

    private static string Value(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) ? value : string.Empty;

    [GeneratedRegex(@"\{\{([A-Za-z]+)\}\}")]
    private static partial Regex Placeholder();
}
