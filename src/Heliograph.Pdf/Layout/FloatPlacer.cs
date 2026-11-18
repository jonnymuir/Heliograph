using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// Puts a paragraph's floating pictures on its page where they are anchored: across from the page, the margin or the text,
/// down from the page, the margin or the paragraph's top, by an offset or aligned in that frame. A picture behind the text
/// is drawn first; one in front, after. A described picture in the body is a figure in its paragraph; any other is an
/// artifact.
/// </summary>
internal static class FloatPlacer
{
    public static void Place(LaidOutPage page, Paragraph p, PageGeometry g, double paragraphTop, StructNode? node)
    {
        foreach (var f in p.Inlines.OfType<FloatingPicture>())
        {
            var picture = f.Picture;
            var (left, width) = Across(f.Anchor.Across, g);
            var (top, height) = Down(f.Anchor.Down, g, paragraphTop);
            var x = Aligned(f.Anchor.AcrossAlign, left, width, picture.Width, f.Anchor.AcrossOffset, "left", "right");
            var y = Aligned(f.Anchor.DownAlign, top, height, picture.Height, f.Anchor.DownOffset, "top", "bottom");
            var figure = node is not null && picture.Alt is { } alt ? node.Add("Figure", alt: alt) : null;
            var op = new ImageOp(x, y, picture.Width, picture.Height, picture.File, figure);
            if (f.Wrap.Kind != WrapKind.None)
            {
                var clear = f.Wrap.Clear;
                page.Wraps.Add(new Exclusion(x - clear.Left, y - clear.Top, x + picture.Width + clear.Right, y + picture.Height + clear.Bottom, f.Wrap));
            }

            if (f.Behind)
            {
                page.Ops.Insert(0, op);
            }
            else
            {
                page.Ops.Add(op);
            }
        }
    }

    // The frame a picture is placed across in: the page, the left or right margin, or the text area.
    public static (double Left, double Width) Across(string from, PageGeometry g) => from switch
    {
        "page" => (0, g.Width),
        "leftMargin" or "insideMargin" => (0, g.Left),
        "rightMargin" or "outsideMargin" => (g.Width - g.Right, g.Right),
        _ => (g.Left, g.ContentWidth),
    };

    // The frame a picture is placed down in: the page, the top or bottom margin, the text area, or from its paragraph.
    private static (double Top, double Height) Down(string from, PageGeometry g, double paragraphTop) => from switch
    {
        "page" => (0, g.Height),
        "topMargin" => (0, g.Top),
        "bottomMargin" => (g.Height - g.Bottom, g.Bottom),
        "margin" => (g.Top, g.Height - g.Top - g.Bottom),
        _ => (paragraphTop, 0),
    };

    public static double Aligned(string? align, double start, double frame, double size, double offset, string near, string far) =>
        align switch
        {
            null => start + offset,
            "center" => start + ((frame - size) / 2),
            _ when align == far => start + frame - size,
            _ when align == near => start,
            _ => start + offset,
        };
}
