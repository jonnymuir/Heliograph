namespace Heliograph.Tests.Support;

/// <summary>A letter with page fields in its footers, written as Word writes complex fields, for the tests and for Word.</summary>
internal static class FieldLetters
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Words = "Thank you for your application to the National Juggling Federation, which we received on 3 March and will answer soon.";

    // A complex field as Word writes it: begin, the instruction in as many runs as given, separate, a stale result, end.
    public static string Field(params string[] instruction) =>
        "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r>"
        + string.Concat(instruction.Select(i => $"<w:r><w:instrText xml:space=\"preserve\">{i}</w:instrText></w:r>"))
        + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>9</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";

    // Two pages in a first section, footed "Page n of m (t in all)"; then an appendix numbered in capital Roman numerals.
    public static MemoryStream Letter()
    {
        var styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>";
        string Run(string text) => $"<w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r>";
        var first = $"<w:ftr {W}><w:p>{Run("Page ")}{Field(" PAGE ")}{Run(" of ")}{Field(" SECTIONPAGES ", " \\* MERGEFORMAT ")}{Run(" (")}{Field(" NUMPAGES ")}{Run(" in all)")}</w:p></w:ftr>";
        var appendix = $"<w:ftr {W}><w:p>{Run("Appendix page ")}{Field(" PAGE ", " \\* ROMAN ")}{Run(" of ")}{Field(" SECTIONPAGES ")}</w:p></w:ftr>";
        var docx = new TestDocx()
            .Part("word/styles.xml", styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml").Relationship($"{R}/styles", "styles.xml", external: false)
            .Part("word/footer1.xml", first, "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml").Relationship($"{R}/footer", "footer1.xml", external: false)
            .Part("word/footer2.xml", appendix, "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml").Relationship($"{R}/footer", "footer2.xml", external: false);
        for (var i = 1; i < 40; i++)
        {
            docx.RawParagraph($"<w:p {W}><w:r><w:t>{Words}</w:t></w:r></w:p>");
        }

        docx.RawParagraph($"<w:p {W} xmlns:r=\"{R}\"><w:pPr><w:sectPr><w:footerReference w:type=\"default\" r:id=\"rIdX1\"/></w:sectPr></w:pPr><w:r><w:t>{Words}</w:t></w:r></w:p>");
        docx.RawParagraph($"<w:p {W}><w:r><w:t>The fees for the year.</w:t></w:r></w:p>");
        docx.RawParagraph($"<w:sectPr {W} xmlns:r=\"{R}\"><w:footerReference w:type=\"default\" r:id=\"rIdX2\"/><w:pgNumType w:start=\"1\"/></w:sectPr>");
        return docx.Build();
    }
}
