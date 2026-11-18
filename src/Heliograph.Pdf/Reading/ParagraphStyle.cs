namespace Heliograph.Pdf.Reading;

/// <summary>A tab stop: where it is from the text area's left edge, how text lines up on it, and what fills the gap before it.</summary>
internal sealed record TabStop(double Position, string Align = "left", string Leader = "none")
{
    /// <summary>A stop at the document's default interval, not one the paragraph sets.</summary>
    public bool Implicit { get; init; }
}

internal sealed record ParagraphStyle(int? OutlineLevel, Spacing Spacing, Indent Indent, string Align, Flow Flow, IReadOnlyList<TabStop> Tabs)
{
    public double DefaultTab { get; init; } = 36;

    public double Overlap { get; init; }

    /// <summary>The paragraph's style, and whether it leaves out space between paragraphs of that same style.</summary>
    public string StyleId { get; init; } = "";

    public bool Contextual { get; init; }

    /// <summary>The space before this paragraph when it follows another paragraph.</summary>
    public double SpaceAbove => Spacing.Before - Overlap;
}
