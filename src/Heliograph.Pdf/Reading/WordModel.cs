namespace Heliograph.Pdf.Reading;

// A rendered letter read into what a page needs: paragraphs with their styles resolved, tables, and the section's page,
// margins, headers and footers. Lengths are in points.

internal sealed record RunStyle(bool Bold, bool Italic, bool Underline, double Size, string Font);

internal abstract record Inline(RunStyle Style);

internal sealed record TextInline(string Text, RunStyle Style) : Inline(Style);

internal sealed record TabInline(RunStyle Style) : Inline(Style);

internal sealed record BreakInline(RunStyle Style) : Inline(Style);

internal sealed record FieldInline(string Field, RunStyle Style) : Inline(Style);

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
internal sealed record ParagraphStyle(int? OutlineLevel, Spacing Spacing, Indent Indent, string Align, Flow Flow, IReadOnlyList<double> Tabs)
{
    public double DefaultTab { get; init; } = 36;

    public double Overlap { get; init; }

    /// <summary>The space before this paragraph when it follows another paragraph.</summary>
    public double SpaceAbove => Spacing.Before - Overlap;
}

internal abstract record Block;

internal sealed record Paragraph(ParagraphStyle Style, IReadOnlyList<Inline> Inlines, TextInline? Label, RunStyle MarkStyle) : Block
{
    public bool IsListItem => Label is not null;
}

internal sealed record Border(double Width);

internal sealed record CellBorders(Border? Top, Border? Left, Border? Bottom, Border? Right);

internal sealed record Cell(IReadOnlyList<Block> Blocks, int Span, CellBorders Borders);

/// <summary>A table row. Rows break across pages unless marked not to; heading rows repeat at the top of each page.</summary>
internal sealed record Row(IReadOnlyList<Cell> Cells, bool Header, bool CantSplit);

internal sealed record CellMargins(double Left, double Right, double Top, double Bottom);

internal sealed record Table(IReadOnlyList<double> Grid, IReadOnlyList<Row> Rows, double Indent, CellMargins Margins) : Block;

internal sealed record PageGeometry(double Width, double Height, double Top, double Bottom, double Left, double Right, double HeaderDistance, double FooterDistance)
{
    public double ContentWidth => Width - Left - Right;
}

/// <summary>The headers and footers of a section: the usual ones, and the first page's when it has its own.</summary>
internal sealed record Stories(IReadOnlyList<Block> Header, IReadOnlyList<Block> Footer, IReadOnlyList<Block>? FirstHeader, IReadOnlyList<Block>? FirstFooter);

internal sealed record Section(PageGeometry Page, Stories Stories);

internal sealed record WordDocument(string Title, string Language, Section Section, IReadOnlyList<Block> Body);
