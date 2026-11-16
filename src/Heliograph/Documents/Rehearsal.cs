using System.Globalization;
using System.Text.Json;

namespace Heliograph.Documents;

/// <summary>
/// Renders a compiled template for every outcome its conditions allow, with the most awkward value each field's
/// type permits, and checks every result. Whether a document is structurally sound depends only on which
/// conditions are yes and the shape of the values, so a template that passes here cannot produce a broken file for
/// any recipient. One that fails is refused at compile, where its author is, not when a letter is due.
/// </summary>
internal static class Rehearsal
{
    // Up to this many conditions, every combination is tried. Beyond it, all yes, all no, and each one on its own.
    private const int EveryCombinationUpTo = 6;

    private static readonly RenderSettings Settings = new(CultureInfo.InvariantCulture);
    private static readonly string[] AddressLines = ["First line", "Second line"];
    private static readonly int[] ShortLists = [0, 1];

    public static Diagnostic? Run(CompiledDocument document)
    {
        var conditions = document.ConditionFields;
        var dictionary = document.Dictionary;

        // Every outcome with two items in each list, then lists that are empty and lists of one, with everything shown.
        var runs = Outcomes(conditions.Count).Select(yes => (yes, Items: 2))
            .Concat(ShortLists.Select(items => (yes: (Func<int, bool>)(_ => true), Items: items)));
        foreach (var (yes, items) in runs)
        {
            var answers = conditions.Select((field, index) => (field, IsYes: yes(index))).ToList();
            var data = JsonSerializer.SerializeToElement(Data(dictionary.Fields, answers, items));
            var parts = document.RenderParts(new RecipientData(data, Settings, dictionary));
            if (parts.Values.Select(WordStructure.Problem).FirstOrDefault(problem => problem is not null) is { } problem)
            {
                return new Diagnostic(DiagnosticCodes.WouldRenderBrokenDocument,
                    $"This template would produce a document that cannot be opened {When(answers, items)}: {problem}.",
                    "This is a fault in Heliograph, not in your template. Please report it with the template attached. "
                    + "Moving or removing the condition nearest the problem may work round it.");
            }
        }

        return null;
    }

    /// <summary>Each outcome to try, as a function from a condition's position to whether it is yes.</summary>
    private static IEnumerable<Func<int, bool>> Outcomes(int conditions)
    {
        if (conditions <= EveryCombinationUpTo)
        {
            return Enumerable.Range(0, 1 << conditions).Select<int, Func<int, bool>>(bits => index => (bits >> index & 1) == 1);
        }

        var all = new Func<int, bool>[] { _ => true, _ => false };
        var onlyOne = Enumerable.Range(0, conditions).SelectMany(one => new Func<int, bool>[] { index => index == one, index => index != one });
        return all.Concat(onlyOne);
    }

    /// <summary>A value for every field: the most awkward its type allows, and for a list, that many items.</summary>
    private static Dictionary<string, object> Data(IEnumerable<Field> fields, List<(Field Field, bool IsYes)> answers, int items) =>
        fields.ToDictionary(field => field.Key, field => field.Type switch
        {
            // Two paragraphs, so that every text tag is tried at splitting the paragraph it is in.
            FieldType.Text => (object)"First.\n\nSecond.",
            FieldType.Date => "2000-01-01",
            FieldType.Address => AddressLines,
            FieldType.YesNo => answers.FirstOrDefault(answer => answer.Field == field).IsYes,
            FieldType.List => Enumerable.Range(0, items).Select(_ => Data(field.Fields!, answers, items)).ToArray(),
            _ => 1,
        });

    private static string When(List<(Field Field, bool IsYes)> answers, int items)
    {
        var lists = items == 1 ? "each list has 1 item" : $"each list has {items} items";
        var conditions = answers.Select(answer => $"\"{answer.Field.Name}\" is {(answer.IsYes ? "yes" : "no")}");
        return "when " + string.Join(", ", conditions.Append(lists));
    }
}
