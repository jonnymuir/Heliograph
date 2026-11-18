namespace Heliograph.Tests.Support;

/// <summary>Letters with footnotes, written as Word writes them: a footnotes part with its separators, and the two note styles.</summary>
internal static class NoteLetters
{
    private const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Rels = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public const string Styles = $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
        + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
        + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"FootnoteText\"><w:name w:val=\"footnote text\"/><w:basedOn w:val=\"Normal\"/><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr><w:rPr><w:sz w:val=\"20\"/></w:rPr></w:style>"
        + "<w:style w:type=\"character\" w:styleId=\"FootnoteReference\"><w:name w:val=\"footnote reference\"/><w:rPr><w:vertAlign w:val=\"superscript\"/></w:rPr></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"EndnoteText\"><w:name w:val=\"endnote text\"/><w:basedOn w:val=\"Normal\"/><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr><w:rPr><w:sz w:val=\"20\"/></w:rPr></w:style>"
        + "<w:style w:type=\"character\" w:styleId=\"EndnoteReference\"><w:name w:val=\"endnote reference\"/><w:rPr><w:vertAlign w:val=\"superscript\"/></w:rPr></w:style></w:styles>";

    /// <summary>A footnotes part with Word's two separators and each note's text.</summary>
    public static string Footnotes(params string[] notes) =>
        $"<w:footnotes {W}>"
        + "<w:footnote w:type=\"separator\" w:id=\"-1\"><w:p><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr><w:r><w:separator/></w:r></w:p></w:footnote>"
        + "<w:footnote w:type=\"continuationSeparator\" w:id=\"0\"><w:p><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr><w:r><w:continuationSeparator/></w:r></w:p></w:footnote>"
        + string.Concat(notes.Select((text, i) => $"<w:footnote w:id=\"{i + 1}\"><w:p><w:pPr><w:pStyle w:val=\"FootnoteText\"/></w:pPr>"
            + "<w:r><w:rPr><w:rStyle w:val=\"FootnoteReference\"/></w:rPr><w:footnoteRef/></w:r><w:r><w:t xml:space=\"preserve\"> " + text + "</w:t></w:r></w:p></w:footnote>"))
        + "</w:footnotes>";

    /// <summary>An endnotes part with Word's two separators and each note's text.</summary>
    public static string Endnotes(params string[] notes) =>
        $"<w:endnotes {W}>"
        + "<w:endnote w:type=\"separator\" w:id=\"-1\"><w:p><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr><w:r><w:separator/></w:r></w:p></w:endnote>"
        + "<w:endnote w:type=\"continuationSeparator\" w:id=\"0\"><w:p><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr><w:r><w:continuationSeparator/></w:r></w:p></w:endnote>"
        + string.Concat(notes.Select((text, i) => $"<w:endnote w:id=\"{i + 1}\"><w:p><w:pPr><w:pStyle w:val=\"EndnoteText\"/></w:pPr>"
            + "<w:r><w:rPr><w:rStyle w:val=\"EndnoteReference\"/></w:rPr><w:endnoteRef/></w:r><w:r><w:t xml:space=\"preserve\"> " + text + "</w:t></w:r></w:p></w:endnote>"))
        + "</w:endnotes>";

    /// <summary>A body paragraph whose text is followed by a reference to an endnote.</summary>
    public static string EndReferring(string text, int note) =>
        $"<w:p {W}><w:r><w:t>{text}</w:t></w:r><w:r><w:rPr><w:rStyle w:val=\"EndnoteReference\"/></w:rPr><w:endnoteReference w:id=\"{note}\"/></w:r></w:p>";

    /// <summary>A body paragraph whose text is followed by a reference to a note.</summary>
    public static string Referring(string text, int note) =>
        $"<w:p {W}><w:r><w:t>{text}</w:t></w:r><w:r><w:rPr><w:rStyle w:val=\"FootnoteReference\"/></w:rPr><w:footnoteReference w:id=\"{note}\"/></w:r></w:p>";

    /// <summary>
    /// A table of fees, the second row's first cell referring to a footnote. Word writes notes in the order the letter refers
    /// to them, and LibreOffice matches each to its reference by that order, so the note's number should be its place.
    /// </summary>
    public static string FeesTable(int note)
    {
        static string Cell(string text) => $"<w:tc><w:tcPr><w:tcW w:w=\"4500\" w:type=\"dxa\"/></w:tcPr><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:tc>";
        var referring = "<w:tc><w:tcPr><w:tcW w:w=\"4500\" w:type=\"dxa\"/></w:tcPr><w:p><w:r><w:t>Late charge</w:t></w:r><w:r><w:rPr><w:rStyle w:val=\"FootnoteReference\"/></w:rPr>"
            + $"<w:footnoteReference w:id=\"{note}\"/></w:r></w:p></w:tc>";
        return $"<w:tbl {W}><w:tblPr><w:tblW w:w=\"9000\" w:type=\"dxa\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"4500\"/><w:gridCol w:w=\"4500\"/></w:tblGrid>"
            + $"<w:tr>{Cell("Membership")}{Cell("£45.00")}</w:tr><w:tr>{referring}{Cell("£10.00")}</w:tr><w:tr>{Cell("Newsletter")}{Cell("£5.00")}</w:tr></w:tbl>";
    }

    public static string Plain(string text) => $"<w:p {W}><w:r><w:t>{text}</w:t></w:r></w:p>";

    /// <summary>A letter of these body paragraphs, with these notes.</summary>
    public static MemoryStream Letter(IEnumerable<string> body, params string[] notes) => Letter(body, new NoteSet(notes, []));

    /// <summary>A letter of these body paragraphs, with these footnotes and endnotes, and any settings.</summary>
    public static MemoryStream Letter(IEnumerable<string> body, NoteSet set)
    {
        var (notes, endnotes, settings) = (set.Footnotes, set.Endnotes, set.Settings);
        var docx = new TestDocx()
            .Part("word/styles.xml", Styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship(Rels + "/styles", "styles.xml", external: false)
            .Part("word/footnotes.xml", Footnotes(notes), "application/vnd.openxmlformats-officedocument.wordprocessingml.footnotes+xml")
            .Relationship(Rels + "/footnotes", "footnotes.xml", external: false);
        if (settings is not null)
        {
            docx.Part("word/settings.xml", $"<w:settings {W}>{settings}</w:settings>", "application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml")
                .Relationship(Rels + "/settings", "settings.xml", external: false);
        }

        if (endnotes.Length > 0)
        {
            docx.Part("word/endnotes.xml", Endnotes(endnotes), "application/vnd.openxmlformats-officedocument.wordprocessingml.endnotes+xml")
                .Relationship(Rels + "/endnotes", "endnotes.xml", external: false);
        }

        foreach (var p in body)
        {
            docx.RawParagraph(p);
        }

        return docx.Build();
    }
}

/// <summary>A letter's footnotes, endnotes and any settings, as the note letters write them.</summary>
internal sealed record NoteSet(string[] Footnotes, string[] Endnotes, string? Settings = null);
