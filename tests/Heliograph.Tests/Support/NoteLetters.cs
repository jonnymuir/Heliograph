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
        + "<w:style w:type=\"character\" w:styleId=\"FootnoteReference\"><w:name w:val=\"footnote reference\"/><w:rPr><w:vertAlign w:val=\"superscript\"/></w:rPr></w:style></w:styles>";

    /// <summary>A footnotes part with Word's two separators and each note's text.</summary>
    public static string Footnotes(params string[] notes) =>
        $"<w:footnotes {W}>"
        + "<w:footnote w:type=\"separator\" w:id=\"-1\"><w:p><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr><w:r><w:separator/></w:r></w:p></w:footnote>"
        + "<w:footnote w:type=\"continuationSeparator\" w:id=\"0\"><w:p><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr><w:r><w:continuationSeparator/></w:r></w:p></w:footnote>"
        + string.Concat(notes.Select((text, i) => $"<w:footnote w:id=\"{i + 1}\"><w:p><w:pPr><w:pStyle w:val=\"FootnoteText\"/></w:pPr>"
            + "<w:r><w:rPr><w:rStyle w:val=\"FootnoteReference\"/></w:rPr><w:footnoteRef/></w:r><w:r><w:t xml:space=\"preserve\"> " + text + "</w:t></w:r></w:p></w:footnote>"))
        + "</w:footnotes>";

    /// <summary>A body paragraph whose text is followed by a reference to a note.</summary>
    public static string Referring(string text, int note) =>
        $"<w:p {W}><w:r><w:t>{text}</w:t></w:r><w:r><w:rPr><w:rStyle w:val=\"FootnoteReference\"/></w:rPr><w:footnoteReference w:id=\"{note}\"/></w:r></w:p>";

    public static string Plain(string text) => $"<w:p {W}><w:r><w:t>{text}</w:t></w:r></w:p>";

    /// <summary>A letter of these body paragraphs, with these notes.</summary>
    public static MemoryStream Letter(IEnumerable<string> body, params string[] notes)
    {
        var docx = new TestDocx()
            .Part("word/styles.xml", Styles, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")
            .Relationship(Rels + "/styles", "styles.xml", external: false)
            .Part("word/footnotes.xml", Footnotes(notes), "application/vnd.openxmlformats-officedocument.wordprocessingml.footnotes+xml")
            .Relationship(Rels + "/footnotes", "footnotes.xml", external: false);
        foreach (var p in body)
        {
            docx.RawParagraph(p);
        }

        return docx.Build();
    }
}
