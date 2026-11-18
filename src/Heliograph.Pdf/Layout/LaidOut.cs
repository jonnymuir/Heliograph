using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// A node of the structure a screen reader follows: Document, H1, P, L, LI, Table, TR, TD and so on. Its kids are other
/// nodes, or marks of text on a page (added when the page is written).
/// </summary>
internal sealed class StructNode(string type, StructNode? parent)
{
    public string Type { get; } = type;
    public StructNode? Parent { get; } = parent;
    public List<object> Kids { get; } = [];
    public string? Scope { get; init; }

    /// <summary>What a figure shows, for a screen reader.</summary>
    public string? Alt { get; init; }
    public int ColumnSpan { get; init; } = 1;

    /// <summary>The element's id, which a note must have.</summary>
    public string? Id { get; init; }

    public StructNode Add(string type, string? scope = null, int columnSpan = 1, string? alt = null)
    {
        var child = new StructNode(type, this) { Scope = scope, ColumnSpan = columnSpan, Alt = alt };
        Kids.Add(child);
        return child;
    }
}

/// <summary>A piece of marked text on a page that belongs to a structure node.</summary>
internal sealed record MarkedRef(int Page, int Mcid);

internal abstract record Op;

/// <summary>Text at a baseline. With no node it is an artifact: a header, a footer or a repeated table heading.</summary>
internal sealed record TextOp(double X, double Baseline, string Text, RunStyle Style, StructNode? Node) : Op;

/// <summary>A picture at its top-left corner and size. With no node it is an artifact: decorative, undescribed, or in a header.</summary>
internal sealed record ImageOp(double X, double Top, double Width, double Height, Pictures.PictureFile File, StructNode? Node) : Op;

/// <summary>An area of a page that goes to an address when clicked, and the Link in the structure it belongs to.</summary>
internal sealed record LinkOp(double X1, double Top, double X2, double Bottom, string Address, StructNode Node) : Op;

/// <summary>A link's clickable area, as its Link holds it: the page it is on and its annotation object.</summary>
internal sealed record AnnotationRef(int Page, int Id);

/// <summary>A filled rectangle, such as a cell's shading; always an artifact. Y runs down the page.</summary>
internal sealed record FillOp(double X, double Top, double Width, double Height, string Color) : Op;

/// <summary>A ruled line, such as a table border; always an artifact. Y runs down the page.</summary>
internal sealed record LineOp(double X1, double Y1, double X2, double Y2, double Width) : Op;

internal sealed class LaidOutPage
{
    public List<Op> Ops { get; } = [];
}

/// <summary>Where a line goes: whose text it is, and whether it is an artifact.</summary>
internal sealed record Owner(StructNode? Node, StructNode? Label)
{
    public static readonly Owner Artifact = new(null, null);
}
