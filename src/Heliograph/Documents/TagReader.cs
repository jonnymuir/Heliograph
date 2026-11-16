namespace Heliograph.Documents;

internal enum TagKind
{
    /// <summary>((Field name)): print the field's value here.</summary>
    Value,

    /// <summary>((if Field name)): show what follows only when the field is yes.</summary>
    If,

    /// <summary>((end if)): the end of the content a condition governs.</summary>
    EndIf,
}

/// <summary>
/// One tag found in a paragraph's text: what it asks for, where it is, and the field it names. An ((if ...)) whose
/// field was reported as wrong is kept with no field, so its ((end if)) still has something to close.
/// </summary>
internal readonly record struct Tag(TagKind Kind, int Start, int End, Field? Field);

/// <summary>Reads the tags out of one paragraph's text and checks each against the dictionary.</summary>
internal sealed class TagReader(FieldDictionary dictionary, DiagnosticLocation location, List<Diagnostic> diagnostics)
{
    public const string Open = "((";
    public const string Close = "))";
    private const string IfWord = "if";
    private const string EndIfWords = "end if";

    public List<Tag> Read(string text)
    {
        var tags = new List<Tag>();
        var position = 0;
        while (text.IndexOf(Open, position, StringComparison.Ordinal) is var found and >= 0)
        {
            // In "(see ((Reference)))" or a link "](((Reference)))", the tag is the innermost pair of brackets.
            var open = found;
            while (open + Open.Length < text.Length && text[open + Open.Length] == '(')
            {
                open++;
            }

            var close = text.IndexOf(Close, open + Open.Length, StringComparison.Ordinal);
            if (close < 0)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.TagNotClosed,
                    "A tag is opened with (( but never closed.",
                    "Add )) after the field's name, in the same paragraph.", location));
                break;
            }

            var written = text[(open + Open.Length)..close].Trim();
            position = close + Close.Length;
            if (Understand(written) is var (kind, field))
            {
                tags.Add(new Tag(kind, open, position, field));
            }
        }

        return tags;
    }

    /// <summary>Works out what a tag asks for, or reports why it cannot be understood.</summary>
    private (TagKind Kind, Field? Field)? Understand(string written)
    {
        if (written.Length == 0)
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.TagHasNoName,
                "A tag has nothing between (( and )).",
                "Put the name of a field from the dictionary between the brackets.", location));
            return null;
        }

        var normalised = FieldName.Normalise(written);
        if (normalised == EndIfWords)
        {
            return (TagKind.EndIf, null);
        }

        var isCondition = normalised.StartsWith(IfWord + " ", StringComparison.Ordinal);
        var name = isCondition ? written[IfWord.Length..].Trim() : written;
        var field = Lookup(name, isCondition);
        if (field is not null && CheckType(isCondition, field))
        {
            return (isCondition ? TagKind.If : TagKind.Value, field);
        }

        // A condition that is wrong is kept, with no field, so that its ((end if)) is not reported as well.
        return isCondition ? (TagKind.If, null) : null;
    }

    private Field? Lookup(string name, bool isCondition)
    {
        if (dictionary.Find(name) is { } field)
        {
            return field;
        }

        var nearest = FieldName.Nearest(name, dictionary.AllFields.Select(f => f.Name));
        var suggestion = isCondition ? $"(({IfWord} {nearest}))" : $"(({nearest}))";
        diagnostics.Add(new Diagnostic(DiagnosticCodes.UnknownField,
            $"The dictionary has no field called \"{name}\".",
            nearest is null ? "Use the name of a field from the dictionary." : $"Did you mean {suggestion}?",
            location));
        return null;
    }

    /// <summary>A condition needs a yes or no field, and a yes or no field can only be used in a condition.</summary>
    private bool CheckType(bool isCondition, Field field)
    {
        if (isCondition && field.Type != FieldType.YesNo)
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.ConditionNeedsYesNo,
                $"\"{field.Name}\" is not a yes or no field, so content cannot be shown or hidden on it.",
                "Use a yes or no field from the dictionary after \"if\".", location));
            return false;
        }

        if (!isCondition && field.Type == FieldType.List)
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.ListFieldOutsideRow,
                $"\"{field.Name}\" is a list, which cannot be printed.",
                $"Use its fields, such as (({field.Fields![0].Name})). In a document they go in a row of a table, and in a message on a "
                + "line of their own. The row or line is repeated for each item.",
                location));
            return false;
        }

        if (!isCondition && field.Type == FieldType.YesNo)
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.YesNoPrinted,
                $"\"{field.Name}\" is a yes or no field, which cannot be printed.",
                $"To show content only when it is yes, put ((if {field.Name})) before the content and ((end if)) after it. "
                + "To print the words, ask for them to be supplied as a text field.",
                location));
            return false;
        }

        return true;
    }
}
