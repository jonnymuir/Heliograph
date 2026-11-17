using Heliograph.Pdf.Fonts;

namespace Heliograph.Pdf.Layout;

/// <summary>Puts a line on a page: aligned, justified if it should be, each piece of text owned by its structure node.</summary>
internal static class LinePlacer
{
    public static void Place(LaidOutPage page, Line line, double left, double top, Owner owner)
    {
        var lastWord = line.Pieces.FindLastIndex(q => q.Piece.Kind == PieceKind.Word);
        if (lastWord < 0)
        {
            return;
        }

        var end = line.Pieces[lastWord].X + line.Pieces[lastWord].Piece.Width;
        var shift = line.Align switch { "center" => (line.Limit - end) / 2, "right" => line.Limit - end, _ => 0.0 };
        var justify = line.Align == "both" && !line.Last && !line.EndsWithBreak;
        var gaps = justify ? line.Pieces.Take(lastWord).Count(q => q.Piece.Kind == PieceKind.Space) : 0;
        var extra = gaps > 0 ? (line.Limit - end) / gaps : 0;
        var baseline = top + line.Ascent;
        var added = 0.0;
        for (var i = 0; i <= lastWord; i++)
        {
            var (x, piece) = line.Pieces[i];
            var node = owner.Node is null ? null : piece.IsLabel ? owner.Label ?? owner.Node : owner.Node;

            // A space is real text, so copying and screen readers keep words apart. Justified lines widen the gap after it.
            Emit(page, new TextOp(left + x + shift + added, baseline, piece.Text, piece.Style, node));
            if (piece.Kind == PieceKind.Space)
            {
                added += extra;
            }
        }
    }

    // A run of text in one style, owner and place joins what came just before, so the page's content stays small.
    private static void Emit(LaidOutPage page, TextOp op)
    {
        if (page.Ops.Count > 0 && page.Ops[^1] is TextOp last && last.Node == op.Node && last.Style == op.Style &&
            Math.Abs(last.Baseline - op.Baseline) < 0.001 &&
            Math.Abs(last.X + FontLibrary.Face(last.Style).Width(last.Text, last.Style.Size) - op.X) < 0.001)
        {
            page.Ops[^1] = last with { Text = last.Text + op.Text };
            return;
        }

        page.Ops.Add(op);
    }
}
