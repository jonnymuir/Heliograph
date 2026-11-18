using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>One cell of a row, broken into lines, each with its top measured from the top of the cell's text.</summary>
internal sealed record CellLayout(double Left, double Width, double TextLeft, CellBorders Borders, List<(double Top, Line Line, Owner Owner)> Lines, double Height)
{
    public string VAlign { get; init; } = "top";
}
