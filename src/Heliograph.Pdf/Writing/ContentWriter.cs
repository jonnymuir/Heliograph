using System.Text;
using Heliograph.Pdf.Fonts;
using Heliograph.Pdf.Layout;
using static Heliograph.Pdf.Writing.PdfObjects;

namespace Heliograph.Pdf.Writing;

/// <summary>
/// Writes one page's content stream. Text that belongs to the structure is marked with its node and a marked-content
/// id; everything else (headers, footers, borders) is marked as an artifact, so a screen reader skips it.
/// </summary>
internal sealed class ContentWriter(FontEmbedder fonts, double pageHeight)
{
    private readonly StringBuilder _content = new();
    private readonly HashSet<(string Name, int Id)> _fonts = [];
    private StructNode? _open;
    private bool _inArtifact;

    /// <summary>The structure node of each marked-content id on the page, in order.</summary>
    public List<StructNode> Marked { get; } = [];

    public IEnumerable<(string Name, int Id)> FontsUsed => _fonts;

    public byte[] Write(int pageIndex, LaidOutPage page)
    {
        foreach (var op in page.Ops)
        {
            switch (op)
            {
                case TextOp text:
                    Mark(pageIndex, text.Node, "/Artifact <</Type /Pagination>> BDC\n");
                    Text(text);
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
        var face = FontLibrary.Face(text.Style);
        var (name, id, hex) = fonts.Use(face, text.Text);
        _fonts.Add((name, id));
        var size = text.Style.Size;
        _content.Append(Invariant($"BT /{name} {N(size)} Tf 1 0 0 1 {N(text.X)} {N(pageHeight - text.Baseline)} Tm <{hex}> Tj ET\n"));
        if (text.Style.Underline)
        {
            var (x, y, w) = (text.X, pageHeight - text.Baseline - (size / 8), face.Width(text.Text, size));
            _content.Append(Invariant($"{N(size / 20)} w {N(x)} {N(y)} m {N(x + w)} {N(y)} l S\n"));
        }
    }
}
