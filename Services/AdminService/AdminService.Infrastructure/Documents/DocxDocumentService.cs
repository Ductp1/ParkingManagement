using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AdminService.Application.Features.ContractTemplates;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Documents;

/// <summary>
/// US-116 v11: đọc / điền file DOCX bằng thư viện có sẵn của .NET (ZipArchive + XML), không cần package Office.
/// DOCX = zip chứa word/document.xml (WordprocessingML). Chỉ đọc chữ đoạn văn và hàng bảng; định dạng không cần cho việc tách.
/// </summary>
public sealed partial class DocxDocumentService : IDocxDocumentService
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string DocumentPart = "word/document.xml";
    /// <summary>Chặn zip bomb: phần XML giải nén tối đa 50 MB.</summary>
    private const long MaxPartBytes = 50L * 1024 * 1024;

    public IReadOnlyList<DocxParagraph> ReadParagraphs(byte[] docx)
    {
        using var zip = Open(docx);
        var document = LoadPart(zip, DocumentPart)
                       ?? throw new ValidationException("File không phải DOCX hợp lệ (thiếu word/document.xml).");
        var body = document.Root?.Element(W + "body");
        if (body is null) return [];

        var result = new List<DocxParagraph>();
        foreach (var element in body.Elements())
        {
            if (element.Name == W + "p")
                result.Add(new DocxParagraph(ParagraphText(element), Style(element), IsTableRow: false));
            else if (element.Name == W + "tbl")
                foreach (var row in element.Descendants(W + "tr"))
                {
                    var cells = row.Elements(W + "tc")
                        .Select(tc => string.Join(" ", tc.Descendants(W + "p").Select(ParagraphText).Where(t => t.Length > 0)))
                        .ToList();
                    if (cells.Any(c => c.Length > 0)) result.Add(new DocxParagraph(string.Join(" | ", cells), null, IsTableRow: true));
                }
        }
        return result;
    }

    public byte[] FillPlaceholders(byte[] docx, IReadOnlyDictionary<string, string> values)
    {
        using var output = new MemoryStream();
        output.Write(docx);
        using (var zip = Open(output, ZipArchiveMode.Update))
        {
            var parts = zip.Entries
                .Where(e => e.FullName == DocumentPart || PartName().IsMatch(e.FullName))
                .Select(e => e.FullName)
                .ToList();
            foreach (var name in parts)
            {
                var xml = LoadPart(zip, name);
                if (xml is null || !FillParagraphs(xml, values)) continue;
                zip.GetEntry(name)!.Delete();
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var stream = entry.Open();
                using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
                xml.Save(writer);
            }
        }
        return output.ToArray();
    }

    /// <summary>
    /// Thay {{Key}} theo từng đoạn: nếu placeholder nằm gọn trong một w:t thì thay tại chỗ (giữ định dạng); nếu Word tách nó qua
    /// nhiều run thì gộp chữ cả đoạn vào w:t đầu (đoạn đó mất định dạng khác nhau giữa các run). Trả true nếu có thay đổi.
    /// </summary>
    private static bool FillParagraphs(XDocument xml, IReadOnlyDictionary<string, string> values)
    {
        var changed = false;
        foreach (var paragraph in xml.Descendants(W + "p"))
        {
            var texts = paragraph.Descendants(W + "t").ToList();
            if (texts.Count == 0) continue;
            var joined = string.Concat(texts.Select(t => t.Value));
            if (!joined.Contains("{{", StringComparison.Ordinal)) continue;

            foreach (var t in texts)
            {
                var replaced = Replace(t.Value, values);
                if (replaced == t.Value) continue;
                t.Value = replaced;
                Preserve(t);
                changed = true;
            }

            var after = string.Concat(texts.Select(t => t.Value));
            var whole = Replace(after, values);
            if (whole == after) continue;
            texts[0].Value = whole;
            Preserve(texts[0]);
            foreach (var t in texts.Skip(1)) t.Value = string.Empty;
            changed = true;
        }
        return changed;
    }

    private static string Replace(string text, IReadOnlyDictionary<string, string> values)
        => Placeholder().Replace(text, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    private static void Preserve(XElement t) => t.SetAttributeValue(XNamespace.Xml + "space", "preserve");

    private static string ParagraphText(XElement paragraph)
    {
        var sb = new StringBuilder();
        foreach (var node in paragraph.Descendants())
        {
            if (node.Name == W + "t") sb.Append(node.Value);
            else if (node.Name == W + "tab") sb.Append(' ');
            else if (node.Name == W + "br") sb.Append(' ');
        }
        return Whitespace().Replace(sb.ToString(), " ").Trim();
    }

    private static string? Style(XElement paragraph)
        => paragraph.Element(W + "pPr")?.Element(W + "pStyle")?.Attribute(W + "val")?.Value;

    private static ZipArchive Open(byte[] docx) => Open(new MemoryStream(docx, writable: false), ZipArchiveMode.Read);

    private static ZipArchive Open(Stream stream, ZipArchiveMode mode)
    {
        try
        {
            var zip = new ZipArchive(stream, mode, leaveOpen: mode == ZipArchiveMode.Update);
            if (zip.GetEntry("[Content_Types].xml") is null || zip.GetEntry(DocumentPart) is null)
            {
                zip.Dispose();
                throw new ValidationException(NotDocx);
            }
            return zip;
        }
        catch (InvalidDataException)
        {
            throw new ValidationException(NotDocx);
        }
    }

    private static XDocument? LoadPart(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        if (entry is null) return null;
        // Length chỉ đọc được ở chế độ Read; chế độ Update dùng cho file đã kiểm tra lúc nhập.
        if (zip.Mode == ZipArchiveMode.Read && entry.Length > MaxPartBytes) throw new ValidationException("File DOCX quá lớn sau khi giải nén.");
        using var stream = entry.Open();
        try
        {
            return XDocument.Load(stream);
        }
        catch (XmlException)
        {
            throw new ValidationException("File DOCX bị hỏng (nội dung XML không đọc được).");
        }
    }

    private const string NotDocx = "File không phải DOCX hợp lệ. File .doc hoặc Google Docs hãy xuất sang .docx trước.";

    [GeneratedRegex(@"\{\{([A-Za-z]+)\}\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"^word/(header|footer)\d*\.xml$")]
    private static partial Regex PartName();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
