using Heliograph.Documents;

namespace Heliograph.Messages;

/// <summary>One piece of a line: text the author wrote, a field to print, or text shown only when a field is yes.</summary>
internal abstract record TextNode;

internal sealed record Literal(string Text) : TextNode;

internal sealed record ValueNode(Field Field) : TextNode;

internal sealed record IfNode(Field Field, List<TextNode> Nodes) : TextNode;

/// <summary>A line of the message, or a run of lines governed by a condition.</summary>
internal abstract record TextBlock;

/// <summary>A line. When it uses a list's fields it is repeated for each item of <paramref name="RepeatsOver"/>.</summary>
internal sealed record TextLine(List<TextNode> Nodes, Field? RepeatsOver, DiagnosticLocation Location) : TextBlock;

internal sealed record TextCondition(Field Field, List<TextBlock> Blocks) : TextBlock;

/// <summary>
/// Which part of a correspondence a text is, for diagnostics. <paramref name="LinesBefore"/> is how many lines of
/// the author's file come before this text, so that a problem is reported at the line the author will find it on.
/// </summary>
internal sealed record TextPart(string Name, int LinesBefore = 0);

/// <summary>One line of a rendered message, with the line of the template it came from.</summary>
internal sealed record RenderedLine(TextLine Source, List<TextPiece> Pieces);

/// <summary>A piece of a rendered line. <paramref name="IsData"/> marks what came from the recipient's data, not the author.</summary>
internal readonly record struct TextPiece(string Text, bool IsData);

/// <summary>
/// A template written as plain text: the SMS part, and the lines of an email. It uses the same tags as a document.
/// A condition's tags on lines of their own govern the lines between them; in the same line they govern part of it.
/// A line that uses a list's fields is repeated for each item.
/// </summary>
internal sealed class TextTemplate
{
    private readonly List<TextBlock> _blocks;
    private readonly FieldDictionary _dictionary;

    private TextTemplate(List<TextBlock> blocks, FieldDictionary dictionary)
    {
        _blocks = blocks;
        _dictionary = dictionary;
    }

    /// <summary>The text the author wrote, without tags, line by line, for checks on the wording itself.</summary>
    public IEnumerable<(int Line, string Text)> Wording { get; private init; } = [];

    /// <summary>Every line of the template, including those inside conditions.</summary>
    public IEnumerable<TextLine> Lines => All(_blocks);

    public static TextTemplate Parse(string source, TextPart part, FieldDictionary dictionary, List<Diagnostic> diagnostics)
    {
        var parser = new TextParser(part.Name, dictionary, diagnostics);
        var lines = source.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            parser.Line(lines[index], part.LinesBefore + index + 1);
        }

        return new TextTemplate(parser.Finish(), dictionary) { Wording = parser.Wording };
    }

    private static IEnumerable<TextLine> All(IEnumerable<TextBlock> blocks) => blocks.SelectMany(block =>
        block is TextCondition condition ? All(condition.Blocks) : [(TextLine)block]);

    /// <summary>The message for one recipient, as lines, each a list of pieces.</summary>
    public List<RenderedLine> Render(RecipientData recipient)
    {
        var writer = new Writer(recipient, _dictionary);
        writer.Blocks(_blocks);
        return writer.Lines;
    }

    /// <summary>Writes the lines of one recipient's message.</summary>
    private sealed class Writer(RecipientData recipient, FieldDictionary dictionary)
    {
        public List<RenderedLine> Lines { get; } = [];

        public void Blocks(List<TextBlock> blocks)
        {
            foreach (var block in blocks)
            {
                Block(block);
            }
        }

        private void Block(TextBlock block)
        {
            if (block is TextCondition condition)
            {
                if (recipient.IsYes(condition.Field, null))
                {
                    Blocks(condition.Blocks);
                }

                return;
            }

            var line = (TextLine)block;
            if (line.RepeatsOver is not { } list)
            {
                Line(line, null);
                return;
            }

            var items = recipient.Items(list) ?? [];
            for (var number = 1; number <= items.Count; number++)
            {
                Line(line, new ItemScope(list, items[number - 1], number));
            }
        }

        private void Line(TextLine line, ItemScope? item)
        {
            var pieces = new List<TextPiece>();

            // A line that was all condition leaves no blank line behind once a condition in it is hidden.
            if (!Evaluate(line.Nodes, item, pieces) || pieces.Any(piece => !string.IsNullOrWhiteSpace(piece.Text)))
            {
                Lines.Add(new RenderedLine(line, pieces));
            }
        }

        /// <summary>Adds what the nodes print to <paramref name="pieces"/>. True when a condition among them was hidden.</summary>
        private bool Evaluate(List<TextNode> nodes, ItemScope? item, List<TextPiece> pieces)
        {
            var hidSomething = false;
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case Literal literal:
                        pieces.Add(new TextPiece(literal.Text, IsData: false));
                        break;
                    case ValueNode value when recipient.Paragraphs(value.Field, Scope(value.Field, item)) is { } paragraphs:
                        pieces.Add(new TextPiece(string.Join("\n\n", paragraphs.Select(lines => string.Join('\n', lines))), IsData: true));
                        break;
                    case IfNode condition when recipient.IsYes(condition.Field, Scope(condition.Field, item)):
                        hidSomething |= Evaluate(condition.Nodes, item, pieces);
                        break;
                    case IfNode:
                        hidSomething = true;
                        break;
                }
            }

            return hidSomething;
        }

        private ItemScope? Scope(Field field, ItemScope? item) => dictionary.ListOf(field) is null ? null : item;
    }
}
