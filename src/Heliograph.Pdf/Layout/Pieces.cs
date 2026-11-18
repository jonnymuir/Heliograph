using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

internal enum PieceKind
{
    Word,
    Space,
    Tab,
    Break,
}

/// <summary>A word, a run of spaces, a tab or a line break, measured in its style. A picture is a word that never splits.</summary>
internal sealed record Piece(string Text, RunStyle Style, double Width, PieceKind Kind, bool IsLabel = false)
{
    public PictureInline? Picture { get; init; }
}
