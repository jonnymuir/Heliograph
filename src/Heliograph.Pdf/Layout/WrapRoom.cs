using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>A floating picture's box on its page, with the space kept clear round it, that text wraps round.</summary>
internal sealed record Exclusion(double Left, double Top, double Right, double Bottom, TextWrap Wrap);

/// <summary>Where a line may go across the text area: from Left to Right, or, when Skip is more than nought, lower down.</summary>
internal readonly record struct LineRoom(double Left, double Right, double Skip);

/// <summary>
/// The room a line has beside the floating pictures text wraps round on its page, as Word lays it out. A line that
/// meets a picture set above and below the text goes below it; beside one wrapped square, it keeps to the side the
/// picture says, or the wider side, and goes below it when no room is left there.
/// </summary>
internal static class WrapRoom
{
    public static LineRoom Of(IReadOnlyList<Exclusion> wraps, double textLeft, (double Top, double Height) line, (double Left, double Right) across)
    {
        var (left, right) = across;
        foreach (var e in wraps.Where(e => line.Top < e.Bottom && line.Top + line.Height > e.Top))
        {
            var (start, end) = (e.Left - textLeft, e.Right - textLeft);
            if (end <= left || start >= right)
            {
                continue;
            }

            if (e.Wrap.Kind == WrapKind.TopAndBottom)
            {
                return new(left, right, e.Bottom - line.Top);
            }

            (left, right) = Side(e.Wrap.Side, start - left, right - end) == WrapSide.Left ? (left, Math.Min(right, start)) : (Math.Max(left, end), right);
            if (right - left <= 0)
            {
                return new(across.Left, across.Right, e.Bottom - line.Top);
            }
        }

        return new(left, right, 0);
    }

    // Which side of a picture text goes: the one it says, or the wider (the check refuses text on both sides).
    private static WrapSide Side(WrapSide side, double leftRoom, double rightRoom) => side switch
    {
        WrapSide.Left or WrapSide.Right => side,
        _ => leftRoom >= rightRoom ? WrapSide.Left : WrapSide.Right,
    };
}
