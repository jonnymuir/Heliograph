namespace Heliograph.Pdf.Reading;

// A rendered letter read into what a page needs: paragraphs with their styles resolved, tables, and the section's page,
// margins, headers and footers. Lengths are in points.

/// <summary>What the part being read links to: its files, such as pictures, by relationship id, and its addresses outside.</summary>
internal sealed record PartLinks(Func<string, byte[]?> Files, Func<string, string?> Addresses)
{
    /// <summary>The footnotes' numbers, given in the order they are referred to.</summary>
    public NoteNumbers Notes { get; init; } = new();
}

internal abstract record Inline(RunStyle Style);

internal sealed record TextInline(string Text, RunStyle Style) : Inline(Style);

internal sealed record TabInline(RunStyle Style) : Inline(Style);

internal sealed record BreakInline(RunStyle Style) : Inline(Style);

internal sealed record FieldInline(string Field, RunStyle Style) : Inline(Style);

/// <summary>A reference to a footnote: its mark, the note's number, and the note it refers to.</summary>
internal sealed record NoteInline(string Mark, string Id, RunStyle Style) : Inline(Style);

/// <summary>
/// A picture set in the line, on the baseline, at its size in points. Alt is what a screen reader says; null for a
/// picture marked as decorative or with no description, which is drawn as an artifact.
/// </summary>
internal sealed record PictureInline(Pictures.PictureFile File, double Width, double Height, string? Alt, RunStyle Style) : Inline(Style);

/// <summary>
/// Where a floating picture goes, as Word anchors it: across from the page, the margin or its paragraph, by an offset or
/// aligned left, centre or right; and down from the page, the margin or its paragraph, by an offset or aligned.
/// </summary>
internal sealed record Anchor(string Across, double AcrossOffset, string? AcrossAlign, string Down, double DownOffset, string? DownAlign);

/// <summary>A picture that floats, with no text wrapped round it, in front of the text or behind it; it takes no room in the lines.</summary>
internal sealed record FloatingPicture(PictureInline Picture, Anchor Anchor, bool Behind) : Inline(Picture.Style);

/// <summary>Space around and between the lines of a paragraph. Line is in 240ths of a line for "auto", else twips.</summary>
internal sealed record Spacing(double Before, double After, double Line, string LineRule);

/// <summary>Indents from the text area's edges. A negative first line is a hanging indent.</summary>
internal sealed record Indent(double Left, double Right, double FirstLine);

/// <summary>How a paragraph may be split across pages.</summary>
internal sealed record Flow(bool KeepNext, bool KeepLines, bool WidowControl, bool PageBreakBefore);

/// <summary>
/// How a paragraph is set. DefaultTab is the document's interval between tab stops where none is set. Overlap is how much
/// of its space before is shared with the space after the paragraph above it: in a document without
/// doNotUseHTMLParagraphAutoSpacing, Word and LibreOffice put the larger of the two between paragraphs, not their sum.
/// </summary>
internal abstract record Block;

/// <summary>
/// A paragraph. A list item has a label, set at its level as the list says: aligned left, right or centred where the
/// first line starts, then a tab, a space or nothing before the text.
/// </summary>
internal sealed record Paragraph(ParagraphStyle Style, IReadOnlyList<Inline> Inlines, TextInline? Label, RunStyle MarkStyle) : Block
{
    public bool IsListItem => Label is not null;

    public int ListLevel { get; init; }

    public LabelPlacement Placement { get; init; } = LabelPlacement.Default;
}

/// <summary>Where a list label sits ("left", "right" or "center" of where the first line starts) and what follows it ("tab", "space" or "nothing").</summary>
internal sealed record LabelPlacement(string Align, string Suffix)
{
    public static readonly LabelPlacement Default = new("left", "tab");
}

internal sealed record Border(double Width);

internal sealed record CellBorders(Border? Top, Border? Left, Border? Bottom, Border? Right);

/// <summary>A cell: its text, how many grid columns it spans, its borders, and where its text sits ("top", "center", "bottom").</summary>
internal sealed record Cell(IReadOnlyList<Block> Blocks, int Span, CellBorders Borders)
{
    public string VAlign { get; init; } = "top";

    /// <summary>"restart" for a cell that starts a run merged down its column, "continue" for one merged into the cell above, else "".</summary>
    public string VMerge { get; init; } = "";

    /// <summary>The cell's shading, as RGB hex, or null for none.</summary>
    public string? Background { get; init; }
}

/// <summary>A table row. Rows break across pages unless marked not to; heading rows repeat at the top of each page.</summary>
internal sealed record Row(IReadOnlyList<Cell> Cells, bool Header, bool CantSplit)
{
    /// <summary>The row's set height, if any: at least that, or exactly that.</summary>
    public double Height { get; init; }

    public bool ExactHeight { get; init; }
}

internal sealed record CellMargins(double Left, double Right, double Top, double Bottom);

/// <summary>A table: its column grid, rows, indent, cell padding, and where it sits across the text area ("left", "center", "right").</summary>
internal sealed record Table(IReadOnlyList<double> Grid, IReadOnlyList<Row> Rows, double Indent, CellMargins Margins) : Block
{
    public string Align { get; init; } = "left";
}


/// <summary>The headers and footers of a section: the usual ones, and the first page's when it has its own.</summary>
internal sealed record Stories(IReadOnlyList<Block> Header, IReadOnlyList<Block> Footer, IReadOnlyList<Block>? FirstHeader, IReadOnlyList<Block>? FirstFooter);

internal sealed record Section(PageGeometry Page, Stories Stories);

internal sealed record WordDocument(string Title, string Language, Section Section, IReadOnlyList<Block> Body)
{
    /// <summary>Each footnote referred to, by its id: its paragraphs.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Block>> Notes { get; init; } = new Dictionary<string, IReadOnlyList<Block>>();

    /// <summary>
    /// An empty single-spaced paragraph in the document's default style. LibreOffice leaves one such line between the text and
    /// a page's footnotes, whatever the separator in the document says, and draws the separator line across it.
    /// </summary>
    public Paragraph? NoteSeparator { get; init; }
}
