using System.IO.Compression;
using System.Security;
using System.Text;
using Heliograph.Library;

namespace Heliograph.Documents;

/// <summary>
/// A Word document to start a template from, instead of a blank page. It has the title set, real heading styles,
/// a first page to write the letter on, and a tag sheet: every field in the dictionary with the tag to copy.
/// </summary>
/// <remarks>
/// The tag sheet starts with a line the compiler knows. A template that still has it is refused, so the sheet can
/// never be sent to a recipient by mistake.
/// </remarks>
public static class StarterDocument
{
    /// <summary>The words that start the tag sheet. A template that still contains them does not compile.</summary>
    public const string TagSheetMarker = "Heliograph tag sheet";

    private const string Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>Makes a starter document for a correspondence.</summary>
    /// <param name="title">What the letter is, for the recipient: it becomes the document's title, which a PDF needs.</param>
    /// <param name="dictionary">The dictionary the correspondence uses.</param>
    /// <param name="language">The document's language, such as "en-GB".</param>
    public static byte[] Create(string title, DictionarySheet dictionary, string language = "en-GB")
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", ContentTypes);
            Add(zip, "_rels/.rels", RootRelationships);
            Add(zip, "docProps/core.xml",
                "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
                + $"<dc:title>{Escape(title)}</dc:title><dc:language>{Escape(language)}</dc:language></cp:coreProperties>");
            Add(zip, "word/_rels/document.xml.rels", DocumentRelationships);
            Add(zip, "word/styles.xml", Styles(language));
            Add(zip, "word/document.xml", Body(title, dictionary));
        }

        return output.ToArray();
    }

    private static string Body(string title, DictionarySheet dictionary)
    {
        var body = new StringBuilder();
        body.Append(Paragraph(title, "Heading1"));
        body.Append(Paragraph("Write your letter here, in your own words and layout. Copy a tag from the tag sheet on the next page wherever the letter should change from one person to the next."));
        body.Append(Paragraph("Delete the tag sheet before you upload this document."));
        body.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>");
        body.Append(Paragraph($"{TagSheetMarker}: delete this page before you upload", "Heading1"));
        body.Append(Paragraph($"These are the fields in the dictionary \"{dictionary.Name}\". Copy a tag exactly as it is shown, brackets included."));
        body.Append(Table(dictionary));
        body.Append(Paragraph("A yes or no field shows or hides content: put the content between its tag and ((end if)). A field that belongs to a list goes in a table row, and the row is repeated for each item in the list."));
        return $"<w:document xmlns:w=\"{Word}\"><w:body>{body}<w:sectPr/></w:body></w:document>";
    }

    private static string Table(DictionarySheet dictionary)
    {
        var rows = new StringBuilder(Row(true, "Tag", "Field", "Type", "Notes"));
        foreach (var field in dictionary.Fields.Where(f => f.Tag is not null))
        {
            var notes = field.List is not null ? $"Only in a table row, which repeats for each item in {field.List}"
                : field.Type == "yes or no" ? "End with ((end if))"
                : "";
            rows.Append(Row(false, field.Tag!, field.Name, field.Type, notes));
        }

        // Four columns sharing the width of an A4 page inside its margins, in twentieths of a point.
        const string grid = "<w:tblGrid><w:gridCol w:w=\"2900\"/><w:gridCol w:w=\"2400\"/><w:gridCol w:w=\"1300\"/><w:gridCol w:w=\"2400\"/></w:tblGrid>";
        return "<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/><w:tblW w:w=\"5000\" w:type=\"pct\"/></w:tblPr>" + grid + rows + "</w:tbl>";
    }

    private static string Row(bool heading, params string[] cells) =>
        "<w:tr>" + (heading ? "<w:trPr><w:tblHeader/></w:trPr>" : "")
        + string.Concat(cells.Select(cell => $"<w:tc>{Paragraph(cell, bold: heading)}</w:tc>")) + "</w:tr>";

    private static string Paragraph(string text, string? style = null, bool bold = false) =>
        "<w:p>" + (style is null ? "" : $"<w:pPr><w:pStyle w:val=\"{style}\"/></w:pPr>")
        + $"<w:r>{(bold ? "<w:rPr><w:b/></w:rPr>" : "")}<w:t xml:space=\"preserve\">{Escape(text)}</w:t></w:r></w:p>";

    private static string Escape(string text) => SecurityElement.Escape(text) ?? "";

    private static string Styles(string language) =>
        $"<w:styles xmlns:w=\"{Word}\">"
        + $"<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\" w:cs=\"Arial\"/><w:sz w:val=\"24\"/><w:lang w:val=\"{Escape(language)}\"/></w:rPr></w:rPrDefault>"
        + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\"/></w:pPr></w:pPrDefault></w:docDefaults>"
        + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Heading1\"><w:name w:val=\"heading 1\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/>"
        + "<w:pPr><w:keepNext/><w:spacing w:before=\"240\" w:after=\"120\"/><w:outlineLvl w:val=\"0\"/></w:pPr><w:rPr><w:b/><w:sz w:val=\"36\"/></w:rPr></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Heading2\"><w:name w:val=\"heading 2\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/>"
        + "<w:pPr><w:keepNext/><w:spacing w:before=\"200\" w:after=\"100\"/><w:outlineLvl w:val=\"1\"/></w:pPr><w:rPr><w:b/><w:sz w:val=\"28\"/></w:rPr></w:style>"
        + "<w:style w:type=\"table\" w:styleId=\"TableGrid\"><w:name w:val=\"Table Grid\"/><w:tblPr><w:tblBorders>"
        + "<w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/><w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/>"
        + "<w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/><w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/>"
        + "<w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/>"
        + "</w:tblBorders></w:tblPr></w:style></w:styles>";

    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        writer.Write(content);
    }

    private const string ContentTypes =
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
        + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
        + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
        + "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>"
        + "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>"
        + "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>"
        + "</Types>";

    private const string RootRelationships =
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>"
        + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>"
        + "</Relationships>";

    private const string DocumentRelationships =
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
        + "</Relationships>";
}
