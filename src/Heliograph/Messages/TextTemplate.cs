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
internal sealed record TextLine(List<TextNode> Nodes, Field? RepeatsOver) : TextBlock;

internal sealed record TextCondition(Field Field, List<TextBlock> Blocks) : TextBlock;

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

    public static TextTemplate Parse(string source, string part, FieldDictionary dictionary, List<Diagnostic> diagnostics)
    {
        var parser = new TextParser(part, dictionary, diagnostics);
        var lines = source.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            parser.Line(lines[index], index + 1);
        }

        return new TextTemplate(parser.Finish(), dictionary) { Wording = parser.Wording };
    }

    /// <summary>The message for one recipient, as lines, each a list of pieces.</summary>
    public List<List<TextPiece>> Render(RecipientData recipient)
    {
        var writer = new Writer(recipient, _dictionary);
        writer.Blocks(_blocks);
        return writer.Lines;
    }

    /// <summary>Writes the lines of one recipient's message.</summary>
    private sealed class Writer(RecipientData recipient, FieldDictionary dictionary)
    {
        public List<List<TextPiece>> Lines { get; } = [];

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
                Line(line.Nodes, null);
                return;
            }

            var items = recipient.Items(list) ?? [];
            for (var number = 1; number <= items.Count; number++)
            {
                Line(line.Nodes, new ItemScope(list, items[number - 1], number));
            }
        }

        private void Line(List<TextNode> nodes, ItemScope? item)
        {
            var pieces = new List<TextPiece>();

            // A line that was all condition leaves no blank line behind once a condition in it is hidden.
            if (!Evaluate(nodes, item, pieces) || pieces.Any(piece => !string.IsNullOrWhiteSpace(piece.Text)))
            {
                Lines.Add(pieces);
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
