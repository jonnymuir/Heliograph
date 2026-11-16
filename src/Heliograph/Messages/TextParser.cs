using Heliograph.Documents;

namespace Heliograph.Messages;

/// <summary>Reads a plain-text template line by line, reporting what is wrong the way the document compiler does.</summary>
internal sealed class TextParser(string part, FieldDictionary dictionary, List<Diagnostic> diagnostics)
{
    private const int ExcerptLength = 80;

    private readonly Stack<(List<TextBlock> Outer, Field? Field, DiagnosticLocation Location)> _open = new();
    private readonly List<(int Line, string Text)> _wording = [];
    private List<TextBlock> _blocks = [];

    public IEnumerable<(int Line, string Text)> Wording => _wording;

    public void Line(string text, int number)
    {
        var location = new DiagnosticLocation(part, number, text.Length <= ExcerptLength ? text : text[..ExcerptLength] + "...");
        var before = diagnostics.Errors();
        var tags = new TagReader(dictionary, location, diagnostics).Read(text);
        if (tags is [{ Kind: not TagKind.Value } only] && text.Trim().Length == only.End - only.Start)
        {
            Boundary(only, location);
            return;
        }

        InlineConditions.Unpaired(tags, location, diagnostics);
        if (diagnostics.Errors() > before)
        {
            return;
        }

        var position = 0;
        var nodes = Nodes(text, ref position, new Queue<Tag>(tags));
        _wording.Add((number, string.Concat(Literals(nodes))));
        var lists = Fields(nodes).Select(dictionary.ListOf).OfType<Field>().Distinct().ToList();
        if (lists.Count > 1)
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.RowMixesLists,
                $"This line uses fields from two lists, \"{lists[0].Name}\" and \"{lists[1].Name}\". A line can repeat for one list only.",
                "Put the fields of each list on a line of their own.", location));
            return;
        }

        _blocks.Add(new TextLine(nodes, lists.FirstOrDefault()));
    }

    /// <summary>The finished blocks. A condition still open is reported where it was opened.</summary>
    public List<TextBlock> Finish()
    {
        foreach (var (_, field, location) in _open.Reverse())
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.ConditionNotClosed,
                $"The condition{(field is null ? "" : $" on \"{field.Name}\"")} is not closed. It has no ((end if)) after it.",
                "Put ((end if)) on a line of its own after the lines the condition governs.", location));
        }

        return _open.Count == 0 ? _blocks : _open.Last().Outer;
    }

    /// <summary>A condition tag alone on its line opens or closes a run of lines.</summary>
    private void Boundary(Tag tag, DiagnosticLocation location)
    {
        if (tag.Kind == TagKind.If)
        {
            _open.Push((_blocks, tag.Field, location));
            _blocks = [];
        }
        else if (_open.TryPop(out var opened))
        {
            if (opened.Field is not null)
            {
                opened.Outer.Add(new TextCondition(opened.Field, _blocks));
            }

            _blocks = opened.Outer;
        }
        else
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.EndWithoutCondition,
                "This ((end if)) has no ((if ...)) before it to end.",
                "Remove it, or add the ((if ...)) tag it belongs to on a line of its own above.", location));
        }
    }

    /// <summary>Turns a line and its tags into pieces, each condition holding what lies between its two tags.</summary>
    private static List<TextNode> Nodes(string text, ref int position, Queue<Tag> tags)
    {
        var nodes = new List<TextNode>();
        while (tags.TryPeek(out var tag) && tag.Kind != TagKind.EndIf)
        {
            tags.Dequeue();
            Add(nodes, text[position..tag.Start]);
            position = tag.End;
            if (tag.Kind == TagKind.Value)
            {
                nodes.Add(new ValueNode(tag.Field!));
                continue;
            }

            var inner = Nodes(text, ref position, tags);
            var close = tags.Dequeue();
            Add(inner, text[position..close.Start]);
            position = close.End;
            nodes.Add(new IfNode(tag.Field!, inner));
        }

        if (tags.Count == 0)
        {
            Add(nodes, text[position..]);
            position = text.Length;
        }

        return nodes;
    }

    private static void Add(List<TextNode> nodes, string text)
    {
        if (text.Length > 0)
        {
            nodes.Add(new Literal(text));
        }
    }

    private static IEnumerable<Field> Fields(IEnumerable<TextNode> nodes) => nodes.SelectMany(node => node switch
    {
        ValueNode value => [value.Field],
        IfNode condition => Fields(condition.Nodes).Prepend(condition.Field),
        _ => Enumerable.Empty<Field>(),
    });

    private static IEnumerable<string> Literals(IEnumerable<TextNode> nodes) => nodes.SelectMany(node => node switch
    {
        Literal literal => [literal.Text],
        IfNode condition => Literals(condition.Nodes),
        _ => Enumerable.Empty<string>(),
    });
}
