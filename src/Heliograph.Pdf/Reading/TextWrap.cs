namespace Heliograph.Pdf.Reading;

/// <summary>How text goes round a floating picture: not at all, beside its box, or above and below it only.</summary>
internal enum WrapKind
{
    None,
    Square,
    TopAndBottom,
}

/// <summary>Which side of a square-wrapped picture text goes: either, the left, the right, or the wider.</summary>
internal enum WrapSide
{
    BothSides,
    Left,
    Right,
    Largest,
}

/// <summary>How text goes round a floating picture, on which side, and the space kept clear round it.</summary>
internal sealed record TextWrap(WrapKind Kind, WrapSide Side, Clearance Clear)
{
    public static readonly TextWrap None = new(WrapKind.None, WrapSide.BothSides, Clearance.Nothing);
}

/// <summary>The space kept clear round a floating picture, in points, on each side.</summary>
internal sealed record Clearance(double Top, double Bottom, double Left, double Right)
{
    public static readonly Clearance Nothing = new(0, 0, 0, 0);
}
