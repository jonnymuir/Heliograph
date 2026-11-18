namespace Heliograph.Pdf.Layout;

/// <summary>
/// The links on one line as it is placed: each run of text with one address is a Link in the structure, holding its text,
/// and an area on the page that goes to the address when clicked.
/// </summary>
internal sealed class LinkSpans(LaidOutPage page, double top, double bottom)
{
    private string? _address;
    private StructNode? _node;
    private double _start;
    private double _end;

    /// <summary>The node a piece's text belongs to: its link, when it is in one, else the paragraph's.</summary>
    public StructNode? NodeFor(Piece piece, double x, StructNode? paragraph)
    {
        if (piece.Style.Link != _address)
        {
            End();
            _address = piece.Style.Link;
            _node = _address is not null ? paragraph?.Add("Link") : null;
            _start = x;
        }

        _end = x + piece.Width;
        return _address is null ? paragraph : _node;
    }

    /// <summary>Closes the link the line ends in, if any.</summary>
    public void End()
    {
        if (_address is not null && _node is not null)
        {
            page.Ops.Add(new LinkOp(_start, top, _end, bottom, _address, _node));
        }

        _address = null;
        _node = null;
    }
}
