using System.Text;
using System.Text.RegularExpressions;

namespace Heliograph.Messages;

/// <summary>What a line of an email body is, decided from what the author wrote at its start.</summary>
internal enum LineKind
{
    Blank,
    Paragraph,
    Heading1,
    Heading2,
    Bullet,
    Numbered,
    Rule,
}

/// <summary>
/// Turns the rendered lines of an email body into HTML and into plain text. The structure (headings, lists, a rule)
/// comes only from the template: a value that happens to start with "# " is text, not a heading.
/// </summary>
internal static partial class EmailBody
{
    private const string Font = "font-family:Helvetica,Arial,sans-serif;color:#0b0c0c;";
    private const string Paragraph = $"margin:0 0 20px 0;font-size:19px;line-height:1.32;{Font}";

    private static readonly Dictionary<LineKind, (string Open, string Close)> Tags = new()
    {
        [LineKind.Paragraph] = ($"<p style=\"{Paragraph}\">", "</p>"),
        [LineKind.Heading1] = ($"<h1 style=\"margin:0 0 20px 0;font-size:36px;line-height:1.1;font-weight:bold;{Font}\">", "</h1>"),
        [LineKind.Heading2] = ($"<h2 style=\"margin:0 0 15px 0;font-size:27px;line-height:1.25;font-weight:bold;{Font}\">", "</h2>"),
        [LineKind.Bullet] = ($"<ul style=\"margin:0 0 20px 0;padding:0 0 0 20px;font-size:19px;line-height:1.32;{Font}\">", "</ul>"),
        [LineKind.Numbered] = ($"<ol style=\"margin:0 0 20px 0;padding:0 0 0 25px;font-size:19px;line-height:1.32;{Font}\">", "</ol>"),
    };

    /// <summary>What kind of line the author wrote, and how many characters of marker start it.</summary>
    public static (LineKind Kind, int Marker) Kind(TextLine line)
    {
        var start = line.Nodes.FirstOrDefault() is Literal literal ? literal.Text : "";
        if (line.Nodes.Count == 0 || line.Nodes.All(node => node is Literal { Text: var text } && string.IsNullOrWhiteSpace(text)))
        {
            return (LineKind.Blank, 0);
        }

        if (line.Nodes.Count == 1 && start.Trim() == "---")
        {
            return (LineKind.Rule, start.Length);
        }

        return start switch
        {
            _ when start.StartsWith("## ", StringComparison.Ordinal) => (LineKind.Heading2, 3),
            _ when start.StartsWith("# ", StringComparison.Ordinal) => (LineKind.Heading1, 2),
            _ when start.StartsWith("- ", StringComparison.Ordinal) || start.StartsWith("* ", StringComparison.Ordinal) => (LineKind.Bullet, 2),
            _ when NumberedStart().Match(start) is { Success: true } numbered => (LineKind.Numbered, numbered.Length),
            _ => (LineKind.Paragraph, 0),
        };
    }

    /// <summary>The body as HTML and as plain text.</summary>
    public static (string Html, string Text) Write(IEnumerable<RenderedLine> lines)
    {
        var html = new StringBuilder();
        var text = new List<string>();
        foreach (var group in Groups(lines))
        {
            var kind = group[0].Kind;
            if (kind == LineKind.Rule)
            {
                html.Append("<hr style=\"border:0;border-top:1px solid #b1b4b6;margin:0 0 20px 0;\">");
                text.Add("---");
                continue;
            }

            var isList = kind is LineKind.Bullet or LineKind.Numbered;
            var items = group.Select(line => (Html: InlineWriter.Html(line.Content), Text: InlineWriter.Text(line.Content))).ToList();
            var (open, close) = Tags[kind];
            html.Append(open)
                .Append(isList ? string.Concat(items.Select(item => $"<li style=\"margin:0 0 5px 0;\">{item.Html}</li>")) : string.Join("<br>", items.Select(item => item.Html)))
                .Append(close);
            text.Add(string.Join('\n', items.Select((item, index) => kind switch
            {
                LineKind.Bullet => "- " + item.Text,
                LineKind.Numbered => $"{index + 1}. {item.Text}",
                _ => item.Text,
            })));
        }

        return (html.ToString(), string.Join("\n\n", text));
    }

    /// <summary>Runs of lines that make one block: a paragraph's lines, or the items of one list. A blank line ends a run.</summary>
    private static List<List<(LineKind Kind, List<Inline> Content)>> Groups(IEnumerable<RenderedLine> lines)
    {
        var groups = new List<List<(LineKind Kind, List<Inline> Content)>>();
        var joins = false;
        foreach (var line in lines)
        {
            var (kind, marker) = Kind(line.Source);
            if (kind == LineKind.Blank)
            {
                joins = false;
                continue;
            }

            // The marker is the author's, at the start of the first piece, and is not part of what is printed.
            var pieces = line.Pieces.Select((piece, index) => index == 0 && marker > 0 ? piece with { Text = piece.Text[marker..] } : piece);
            var content = (kind, InlineMarkdown.Parse(pieces));
            var continues = joins && groups[^1][0].Kind == kind && kind is LineKind.Paragraph or LineKind.Bullet or LineKind.Numbered;
            if (continues)
            {
                groups[^1].Add(content);
            }
            else
            {
                groups.Add([content]);
            }

            joins = true;
        }

        return groups;
    }

    [GeneratedRegex(@"^\d{1,3}\. ", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedStart();
}

/// <summary>Writes the text within a line as HTML, with everything escaped, or as plain text.</summary>
internal static class InlineWriter
{
    public static string Html(IEnumerable<Inline> inlines) => string.Concat(inlines.Select(inline => inline switch
    {
        Plain plain => Escape(plain.Text).ReplaceLineEndings("<br>"),
        Bold bold => $"<strong>{Html(bold.Content)}</strong>",
        Link link => $"<a href=\"{Escape(link.Address)}\" style=\"color:#1d70b8;\">{Html(link.Content)}</a>",
        _ => "",
    }));

    public static string Text(IEnumerable<Inline> inlines) => string.Concat(inlines.Select(inline => inline switch
    {
        Plain plain => plain.Text,
        Bold bold => Text(bold.Content),
        Link link => $"{Text(link.Content)}: {link.Address}",
        _ => "",
    }));

    /// <summary>Makes text safe to put in HTML, in an element or in an attribute.</summary>
    public static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&#39;", StringComparison.Ordinal);
}
