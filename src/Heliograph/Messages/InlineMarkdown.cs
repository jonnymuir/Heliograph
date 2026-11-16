using System.Text;

namespace Heliograph.Messages;

/// <summary>Text within a line of an email: plain, bold, or a link.</summary>
internal abstract record Inline;

internal sealed record Plain(string Text) : Inline;

internal sealed record Bold(List<Inline> Content) : Inline;

internal sealed record Link(List<Inline> Content, string Address) : Inline;

/// <summary>
/// Finds **bold** and [links](address) in a rendered line. Only what the author wrote can be markup: text from the
/// recipient's data is carried as a single opaque piece, so nothing in it is ever read as bold or as a link.
/// </summary>
internal static class InlineMarkdown
{
    private static readonly string[] SafeSchemes = ["https://", "http://", "mailto:", "tel:"];

    /// <summary>Whether an address plainly goes to a web page, an email address or a phone number.</summary>
    public static bool IsSafe(string address) => SafeSchemes.Any(scheme => address.StartsWith(scheme, StringComparison.OrdinalIgnoreCase));

    public static List<Inline> Parse(IEnumerable<TextPiece> pieces)
    {
        // One token for each character the author wrote, and one for each whole value from the data.
        var tokens = pieces.SelectMany(piece => piece.IsData
            ? [new Token(piece.Text, IsData: true)]
            : piece.Text.Select(character => new Token(character.ToString(), IsData: false))).ToList();
        return Parse(tokens, 0, tokens.Count);
    }

    private static List<Inline> Parse(List<Token> tokens, int start, int end)
    {
        var inlines = new List<Inline>();
        var text = new StringBuilder();
        var index = start;
        while (index < end)
        {
            if (LinkAt(tokens, index, end) is var (middle, close))
            {
                Flush(text, inlines);
                inlines.Add(MakeLink(tokens, index, middle, close));
                index = close + 1;
            }
            else if (Is(tokens, index, end, "**") && Find(tokens, index + 2, end, "**") is var boldEnd and >= 0)
            {
                Flush(text, inlines);
                inlines.Add(new Bold(Parse(tokens, index + 2, boldEnd)));
                index = boldEnd + 2;
            }
            else
            {
                text.Append(tokens[index++].Text);
            }
        }

        Flush(text, inlines);
        return inlines;
    }

    /// <summary>Where a link that opens at <paramref name="index"/> has its "](" and its ")", if it is a whole link.</summary>
    private static (int Middle, int Close)? LinkAt(List<Token> tokens, int index, int end)
    {
        if (!Is(tokens, index, end, "["))
        {
            return null;
        }

        var middle = Find(tokens, index + 1, end, "](");
        var close = middle < 0 ? -1 : Find(tokens, middle + 2, end, ")");
        return close < 0 ? null : (middle, close);
    }

    private static Inline MakeLink(List<Token> tokens, int open, int middle, int close)
    {
        // A value in an address is encoded, so it can fill in part of the address and never change where it goes.
        var address = string.Concat(tokens.Skip(middle + 2).Take(close - middle - 2)
            .Select(token => token.IsData ? Uri.EscapeDataString(token.Text) : token.Text));
        var content = Parse(tokens, open + 1, middle);

        // The compiler refuses an unsafe address. This is the last line of defence: the words, and no link.
        return IsSafe(address) ? new Link(content, address) : new Plain(InlineWriter.Text(content));
    }

    private static bool Is(List<Token> tokens, int index, int end, string marker) =>
        index + marker.Length <= end && marker.Select((character, offset) => tokens[index + offset]).Zip(marker)
            .All(pair => !pair.First.IsData && pair.First.Text[0] == pair.Second);

    private static int Find(List<Token> tokens, int from, int end, string marker)
    {
        for (var index = from; index + marker.Length <= end; index++)
        {
            if (Is(tokens, index, end, marker))
            {
                return index;
            }
        }

        return -1;
    }

    private static void Flush(StringBuilder text, List<Inline> inlines)
    {
        if (text.Length > 0)
        {
            inlines.Add(new Plain(text.ToString()));
            text.Clear();
        }
    }

    private readonly record struct Token(string Text, bool IsData);
}
