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
    public int ColumnSpan { get; init; } = 1;

    public StructNode Add(string type, string? scope = null, int columnSpan = 1)
    {
        var child = new StructNode(type, this) { Scope = scope, ColumnSpan = columnSpan };
        Kids.Add(child);
        return child;
    }
}

/// <summary>A piece of marked text on a page that belongs to a structure node.</summary>
internal sealed record MarkedRef(int Page, int Mcid);

internal abstract record Op;

/// <summary>Text at a baseline. With no node it is an artifact: a header, a footer or a repeated table heading.</summary>
internal sealed record TextOp(double X, double Baseline, string Text, RunStyle Style, StructNode? Node) : Op;

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
