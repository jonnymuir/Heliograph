using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// The widths a paragraph has been laid out at as it runs down columns of different widths. When the rest of it goes on in
/// a column of another width, the paragraph is broken again: the lines already placed at the widths they had, the rest at
/// the new one. The lines already placed come out the same, so the rest carries on from where they leave off.
/// </summary>
internal sealed class ColumnWidths(Paragraph p, double width)
{
    // Each width before the current one, with how far down the paragraph its lines reach.
    private readonly List<(double Until, double Width)> _runs = [];
    private double _width = width;

    /// <summary>The paragraph's lines, broken again when the column the rest goes on in is of another width.</summary>
    public List<Line> Next(List<Line> lines, int placed, double now, Func<FieldInline, string> field)
    {
        if (Math.Abs(now - _width) < 0.01)
        {
            return lines;
        }

        _runs.Add((lines.Take(placed).Sum(l => l.Height), _width));
        _width = now;
        var widest = Math.Max(_runs.Max(r => r.Width), now);
        var right = p.Style.Indent.Right;
        return LineBreaker.Break(p, widest, field, new LineBounds(0, (top, _) => new LineRoom(0, At(top) - right, 0)));
    }

    // The width of the line starting this far down the paragraph.
    private double At(double top) => _runs.Where(r => top < r.Until - 0.01).Select(r => r.Width).DefaultIfEmpty(_width).First();
}
