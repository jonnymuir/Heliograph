using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

internal enum PieceKind
{
    Word,
    Space,
    Tab,
    Break,
    Leader,
}

/// <summary>
/// A word, a run of spaces, a tab or a line break, measured in its style. A picture is a word that never splits. A word with
/// characters its font lacks has Segments, each drawn in a font that has them, and is still one word to break lines at.
/// </summary>
internal sealed record Piece(string Text, RunStyle Style, double Width, PieceKind Kind, bool IsLabel = false)
{
    public PictureInline? Picture { get; init; }

    public IReadOnlyList<(string Text, RunStyle Style)>? Segments { get; init; }

    /// <summary>For a footnote's mark in the text, the note it refers to.</summary>
    public string? Note { get; init; }

    /// <summary>For a break, whether it is a page break.</summary>
    public bool PageBreak { get; init; }

    /// <summary>For an endnote's mark in the text, the endnote it refers to.</summary>
    public string? Endnote { get; init; }
}
