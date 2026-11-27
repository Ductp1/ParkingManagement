using System.Text;
using AdminService.Application.Features.ContractTemplates;
using AdminService.Domain.ValueObjects;
using AdminService.Infrastructure.Documents;
using AdminService.Infrastructure.Integrations;
using AdminService.Infrastructure.Persistence.Seeding;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-116 v11: nội dung mẫu dạng form – composer, đọc markup cũ, bộ tách DOCX không AI, map kết quả AI, đọc / điền file DOCX.
public class ContractTemplateContentTests
{
    private static readonly IReadOnlyList<ContractPlaceholderDto> Catalog = ContractPlaceholders.Catalog(new DateTime(2026, 10, 23));

    // ==================== Composer ====================

    [Fact]
    public void Composer_numbers_clauses_escapes_free_text_and_builds_system_blocks()
    {
        var content = new TemplateContent(
            new TemplateHeader("Hà Nội", "Hợp đồng thử", "PHỤ ĐỀ"),
            ["– Căn cứ A;", "Căn cứ B."],
            "# lời dẫn\n|| không phải bảng",
            [
                new TemplateArticle("phạm vi", ["Khoản một", "Khoản hai\na) ý con"]),
                new TemplateArticle("Thỏa thuận riêng", ["Lời dẫn riêng"], IsSpecialTerms: true),
            ],
            [new TemplateAppendix(TemplateAppendixKind.LotInfo, "Phụ lục 01", null, ["Ghi chú"])],
            new TemplateSignature(null),
            "Chú thích cuối");

        var markup = ContractTemplateComposer.ToMarkup(content);

        Assert.Contains("!! Số: {{ContractNumber}} !! Hà Nội, {{IssuedDateLong}}", markup);
        Assert.Contains("# Hợp đồng thử\n^ **PHỤ ĐỀ**", markup);
        Assert.Contains("~ – Căn cứ A;\n~ – Căn cứ B.", markup);
        Assert.Contains("\\# lời dẫn\n\\|| không phải bảng", markup);
        Assert.Contains("## ĐIỀU 1. PHẠM VI\n1.1. Khoản một\n1.2. Khoản hai\n\\a) ý con", markup);
        Assert.Contains("## ĐIỀU 2. THỎA THUẬN RIÊNG\n\\Lời dẫn riêng\n{{SpecialTerms}}", markup);
        Assert.Contains("!! (Ký, ghi rõ họ tên, đóng dấu) !! (Ký, ghi rõ họ tên, đóng dấu)", markup);
        Assert.Contains("| {{LotName}} | {{LotAddress}}", markup);
        Assert.Contains("~ Chú thích cuối", markup);
        Assert.Empty(ContractPlaceholders.MissingRequired(markup).Where(k => k is not ("CommissionRate" or "EffectiveFrom" or "EffectiveTo")));
    }

    [Fact]
    public void Renderer_prints_escaped_lines_as_plain_paragraphs()
    {
        var html = Encoding.UTF8.GetString(new HtmlContractRenderer()
            .Render("\\# không phải tiêu đề **đậm** {{N}}", 1, new Dictionary<string, string> { ["N"] = "x" }, false).Content);

        Assert.Contains("<p># không phải tiêu đề <strong>đậm</strong> x</p>", html);
        Assert.DoesNotContain("<h1>", html);
    }

    // ==================== Đọc markup cũ ====================

    [Fact]
    public void Markup_parser_reads_seed_v4_into_form_content()
    {
        var content = ContractMarkupParser.Parse(PartnerContractTemplates.V4Body);

        Assert.Equal(("TP. Hồ Chí Minh", "Hợp đồng hợp tác khai thác bãi đỗ xe", "TRÊN NỀN TẢNG SMART PARKING MARKETPLACE"),
            (content.Header.PlaceOfSigning, content.Header.Title, content.Header.Subtitle));
        Assert.Equal(5, content.LegalBases.Count);
        Assert.Equal(13, content.Articles.Count);
        Assert.Equal("GIẢI THÍCH TỪ NGỮ", content.Articles[0].Title);
        Assert.Equal(6, content.Articles[0].Clauses.Count);
        Assert.True(Assert.Single(content.Articles, a => a.IsSpecialTerms).Title.Contains("THỎA THUẬN RIÊNG"));
        Assert.Contains("Số tiền quyết toán", content.Articles[3].Clauses[1]);   // dòng căn giữa nối vào khoản 4.2
        Assert.Contains("{{ApplicationCode}}", content.Introduction);
        Assert.Equal([TemplateAppendixKind.LotInfo, TemplateAppendixKind.PayoutAccount], content.Appendices.Select(a => a.Kind));
        Assert.Contains(content.Appendices[1].Notes, n => n.StartsWith("Hình thức nhận:"));
        Assert.Equal("(Ký, ghi rõ họ tên, đóng dấu)", content.Signature.SignNote);
        Assert.StartsWith("Ghi chú:", content.Footnote);
    }

    [Fact]
    public void Composed_standard_template_has_every_required_placeholder()
    {
        var markup = ContractTemplateComposer.ToMarkup(ContractMarkupParser.Parse(PartnerContractTemplates.V4Body));

        Assert.Empty(ContractPlaceholders.MissingRequired(markup));
        Assert.Empty(ContractPlaceholders.Unknown(markup));
    }

    // ==================== Bộ tách DOCX không AI ====================

    [Fact]
    public void Plain_parser_skips_party_blocks_and_signature()
    {
        var analysis = PlainTextContractParser.Parse(
        [
            new DocxParagraph("HỢP ĐỒNG DỊCH VỤ", null, false),
            new DocxParagraph("Bên A: Công ty X", null, false),
            new DocxParagraph("Địa chỉ: 1 Lê Lợi", null, false),
            new DocxParagraph("Hai bên thỏa thuận như sau:", null, false),
            new DocxParagraph("Tổng quát", "Heading1", false),
            new DocxParagraph("ĐIỀU 1: Định nghĩa", null, false),
            new DocxParagraph("1.1 Nền tảng là hệ thống.", null, false),
            new DocxParagraph("ĐẠI DIỆN BÊN A | ĐẠI DIỆN BÊN B", null, true),
            new DocxParagraph("(Ký, ghi rõ họ tên)", null, false),
        ]);

        Assert.Equal("HỢP ĐỒNG DỊCH VỤ", analysis.Content.Header.Title);
        Assert.Equal("Hai bên thỏa thuận như sau:\nTổng quát", analysis.Content.Introduction);
        var article = analysis.Content.Articles[0];
        Assert.Equal(("Định nghĩa", "Nền tảng là hệ thống."), (article.Title, Assert.Single(article.Clauses)));
        Assert.Equal("(Ký, ghi rõ họ tên)", analysis.Content.Signature.SignNote);
        Assert.True(analysis.Content.Articles[^1].IsSpecialTerms);
    }

    // ==================== Map kết quả AI ====================

    [Fact]
    public void Ai_result_is_validated_unknown_placeholders_removed_and_special_article_kept_once()
    {
        const string json = """
            {
              "placeOfSigning": "", "title": "HỢP ĐỒNG HỢP TÁC", "subtitle": "", "legalBases": ["Căn cứ Luật X;"], "introduction": "",
              "articles": [
                { "title": "Phí", "clauses": ["Phí {{CommissionRate}}% cho {{PartnerName}}.", "  "], "isSpecialTerms": false },
                { "title": "Thỏa thuận riêng", "clauses": ["Áp dụng riêng {{SpecialTerms}}"], "isSpecialTerms": true },
                { "title": "Khác", "clauses": [], "isSpecialTerms": true },
                { "title": "", "clauses": [], "isSpecialTerms": false }
              ],
              "appendices": [ { "kind": "PayoutAccount", "title": "Phụ lục 02", "subtitle": "", "notes": [] } ],
              "signNote": "", "footnote": "",
              "platform": { "name": "", "address": "", "taxCode": "", "phone": "", "email": "", "representativeName": "", "representativePosition": "" },
              "warnings": ["Không chắc phụ lục."]
            }
            """;

        var analysis = ClaudeContractDocumentAnalyzer.Map(json, Catalog);

        Assert.Equal(["Phí {{CommissionRate}}% cho PartnerName."], analysis.Content.Articles[0].Clauses);
        Assert.Equal("Áp dụng riêng SpecialTerms", analysis.Content.Articles[1].Clauses[0]);
        Assert.Equal([false, true, false], analysis.Content.Articles.Select(a => a.IsSpecialTerms));
        Assert.Equal("TP. Hồ Chí Minh", analysis.Content.Header.PlaceOfSigning);
        Assert.Null(analysis.Platform);
        Assert.Contains(analysis.Content.Appendices, a => a.Kind == TemplateAppendixKind.LotInfo);
        Assert.Contains("Không chắc phụ lục.", analysis.Warnings);
        Assert.Contains(analysis.Warnings, w => w.Contains("PartnerName"));
    }

    // ==================== Đọc / điền DOCX ====================

    [Fact]
    public void Docx_reader_returns_paragraphs_and_table_rows()
    {
        var docx = DocxTestFiles.Create([["Điều 1. ", "Phạm vi"], ["  "]], tableRows: [["Tên bãi", "Địa chỉ"], ["Bãi A", "1 Lê Lợi"]]);

        var paragraphs = new DocxDocumentService().ReadParagraphs(docx);

        Assert.Equal(["Điều 1. Phạm vi", "", "Tên bãi | Địa chỉ", "Bãi A | 1 Lê Lợi"], paragraphs.Select(p => p.Text));
        Assert.Equal([false, false, true, true], paragraphs.Select(p => p.IsTableRow));
    }

    [Fact]
    public void Docx_fill_replaces_placeholders_in_place_and_across_runs()
    {
        var service = new DocxDocumentService();
        var docx = DocxTestFiles.Create(["Số {{ContractNumber}} – {{Unknown}}"], ["Bên B: {{Business", "Name}}"], ["Không đổi"]);

        var filled = service.FillPlaceholders(docx, new Dictionary<string, string>
        {
            ["ContractNumber"] = "HD-SP-2026-0007", ["BusinessName"] = "Công ty <A&B>",
        });

        Assert.Equal(["Số HD-SP-2026-0007 – {{Unknown}}", "Bên B: Công ty <A&B>", "Không đổi"], service.ReadParagraphs(filled).Select(p => p.Text));
    }

    [Fact]
    public void Docx_reader_rejects_non_docx()
        => Assert.Throws<ValidationException>(() => new DocxDocumentService().ReadParagraphs("PK not a zip"u8.ToArray()));
}
