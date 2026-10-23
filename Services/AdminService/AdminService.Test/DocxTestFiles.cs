using System.IO.Compression;
using System.Security;
using System.Text;

namespace AdminService.Test;

/// <summary>Dựng file DOCX tối giản trong bộ nhớ cho test (zip + [Content_Types].xml + word/document.xml).</summary>
internal static class DocxTestFiles
{
    private const string Ns = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>Mỗi đoạn là một mảng run (chữ của từng run) – để thử placeholder bị Word tách qua nhiều run.</summary>
    public static byte[] Create(params string[][] paragraphs) => Create(paragraphs, tableRows: []);

    public static byte[] Create(IEnumerable<string[]> paragraphs, IEnumerable<string[]> tableRows)
    {
        var body = new StringBuilder();
        foreach (var runs in paragraphs)
        {
            body.Append("<w:p>");
            foreach (var r in runs) body.Append("<w:r><w:t xml:space=\"preserve\">").Append(SecurityElement.Escape(r)).Append("</w:t></w:r>");
            body.Append("</w:p>");
        }
        var rows = tableRows.ToList();
        if (rows.Count > 0)
        {
            body.Append("<w:tbl>");
            foreach (var row in rows)
            {
                body.Append("<w:tr>");
                foreach (var cell in row)
                    body.Append("<w:tc><w:p><w:r><w:t>").Append(SecurityElement.Escape(cell)).Append("</w:t></w:r></w:p></w:tc>");
                body.Append("</w:tr>");
            }
            body.Append("</w:tbl>");
        }

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>");
            Write(zip, "word/document.xml",
                $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"{Ns}\"><w:body>{body}</w:body></w:document>");
        }
        return stream.ToArray();
    }

    /// <summary>Từng đoạn một run.</summary>
    public static byte[] FromLines(params string[] lines) => Create(lines.Select(l => new[] { l }).ToArray());

    private static void Write(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
