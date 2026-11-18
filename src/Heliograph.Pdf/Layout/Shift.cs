namespace Heliograph.Pdf.Layout;

/// <summary>Moves what was drawn on a canvas of its own onto a page, at an offset across and down.</summary>
internal static class Shift
{
    public static Op Moved(Op op, double dx, double dy) => op switch
    {
        TextOp t => t with { X = t.X + dx, Baseline = t.Baseline + dy },
        ImageOp i => i with { X = i.X + dx, Top = i.Top + dy },
        LinkOp l => l with { X1 = l.X1 + dx, X2 = l.X2 + dx, Top = l.Top + dy, Bottom = l.Bottom + dy },
        FillOp f => f with { X = f.X + dx, Top = f.Top + dy },
        LineOp l => l with { X1 = l.X1 + dx, X2 = l.X2 + dx, Y1 = l.Y1 + dy, Y2 = l.Y2 + dy },
        _ => op,
    };
}
