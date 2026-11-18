using System.Text;
using Heliograph.Pdf.Fonts;
using Heliograph.Pdf.Layout;
using static Heliograph.Pdf.Writing.PdfObjects;

namespace Heliograph.Pdf.Writing;

/// <summary>
/// Writes one page's content stream. Text that belongs to the structure is marked with its node and a marked-content
/// id; everything else (headers, footers, borders) is marked as an artifact, so a screen reader skips it.
/// </summary>
internal sealed class ContentWriter(FontEmbedder fonts, ImageEmbedder images, double pageHeight)
{
    private readonly StringBuilder _content = new();
    private readonly HashSet<(string Name, int Id)> _fonts = [];
    private readonly HashSet<(string Name, int Id)> _images = [];
    private StructNode? _open;
    private bool _inArtifact;

    /// <summary>The structure node of each marked-content id on the page, in order.</summary>
    public List<StructNode> Marked { get; } = [];

    public IEnumerable<(string Name, int Id)> FontsUsed => _fonts;

    public IEnumerable<(string Name, int Id)> ImagesUsed => _images.OrderBy(i => i.Id);

    /// <summary>The page's link areas, in the order they were laid out.</summary>
    public List<LinkOp> Links { get; } = [];

    public byte[] Write(int pageIndex, LaidOutPage page)
    {
        foreach (var op in page.Ops)
        {
            switch (op)
            {
                case TextOp text:
                    if (text.Style.Background is not null)
                    {
                        Background(pageIndex, text);
                    }

                    Mark(pageIndex, text.Node, "/Artifact <</Type /Pagination>> BDC\n");
                    Text(text);
                    break;
                case ImageOp image:
                    Mark(pageIndex, image.Node, "/Artifact BMC\n");
                    var (name, id) = images.Use(image.File);
                    _images.Add((name, id));
                    _content.Append(Invariant($"q {N(image.Width)} 0 0 {N(image.Height)} {N(image.X)} {N(pageHeight - image.Top - image.Height)} cm /{name} Do Q\n"));
                    break;
                case LinkOp link:
                    Links.Add(link);
                    break;
                case FillOp fill:
                    Mark(pageIndex, null, "/Artifact BMC\n");
                    _content.Append(Invariant($"q {Rgb(fill.Color)} rg {N(fill.X)} {N(pageHeight - fill.Top - fill.Height)} {N(fill.Width)} {N(fill.Height)} re f Q\n"));
                    break;
                case LineOp line:
                    Mark(pageIndex, null, "/Artifact BMC\n");
                    _content.Append(Invariant($"{N(line.Width)} w 0 J {N(line.X1)} {N(pageHeight - line.Y1)} m {N(line.X2)} {N(pageHeight - line.Y2)} l S\n"));
                    break;
            }
        }

        Close();
        return Encoding.ASCII.GetBytes(_content.ToString());
    }

    private void Mark(int pageIndex, StructNode? node, string artifact)
    {
        if (node is null)
        {
            if (!_inArtifact)
            {
                Close();
                _content.Append(artifact);
                _inArtifact = true;
            }

            return;
        }

        if (_open != node || _inArtifact)
        {
            Close();
            node.Kids.Add(new MarkedRef(pageIndex, Marked.Count));
            _content.Append(Invariant($"/{node.Type} <</MCID {Marked.Count}>> BDC\n"));
            Marked.Add(node);
            _open = node;
        }
    }

    private void Close()
    {
        if (_open is not null || _inArtifact)
        {
            _content.Append("EMC\n");
        }

        _open = null;
        _inArtifact = false;
    }

    private void Text(TextOp text)
    {
        var style = text.Style;
        var face = FontLibrary.Face(style);
        var (name, id, hex) = fonts.Use(face, text.Text);
        _fonts.Add((name, id));
        var (size, width) = (TextLook.Size(style), TextLook.Width(text.Text, style));
        var baseline = pageHeight - text.Baseline + TextLook.Rise(style);
        var colour = Rgb(style.Color);
        _content.Append(Invariant($"BT /{name} {N(size)} Tf {colour} rg {N(style.CharacterSpacing)} Tc 1 0 0 1 {N(text.X)} {N(baseline)} Tm <{hex}> Tj ET\n"));
        var thickness = style.Size / 20;
        var lines = new List<double>();
        if (style.Underline)
        {
            lines.Add(pageHeight - text.Baseline - (style.Size / 8));
        }

        // Strikethrough runs through the middle of the lower-case letters; a double one is two lines either side of it.
        var through = baseline + (size * 0.25);
        lines.AddRange(style.Strike switch { 1 => [through], 2 => [through + thickness, through - thickness], _ => [] });
        foreach (var y in lines)
        {
            _content.Append(Invariant($"{colour} RG {N(thickness)} w {N(text.X)} {N(y)} m {N(text.X + width)} {N(y)} l S\n"));
        }
    }

    // A highlight or shading behind a run of text, as deep as the font reaches; decoration, so an artifact.
    private void Background(int pageIndex, TextOp text)
    {
        var (ascent, descent, _) = FontLibrary.Metrics(text.Style with { VertAlign = "baseline" });
        var bottom = pageHeight - text.Baseline - descent;
        Mark(pageIndex, null, "/Artifact BMC\n");
        _content.Append(Invariant($"q {Rgb(text.Style.Background)} rg {N(text.X)} {N(bottom)} {N(TextLook.Width(text.Text, text.Style))} {N(ascent + descent)} re f Q\n"));
    }

    // An RGB hex colour as PDF colour components; automatic is black.
    private static string Rgb(string? hex)
    {
        var value = Convert.ToInt32(hex ?? "000000", 16);
        return Invariant($"{N(((value >> 16) & 0xFF) / 255.0)} {N(((value >> 8) & 0xFF) / 255.0)} {N((value & 0xFF) / 255.0)}");
    }
}
