namespace Heliograph;

/// <summary>How a field name is compared, and how the nearest name to a misspelt one is found.</summary>
internal static class FieldName
{
    /// <summary>Lower case, with runs of white space collapsed to one space, so capitals and spacing do not matter.</summary>
    public static string Normalise(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    /// <summary>The dictionary name closest to what was written, when one is close enough to be a likely slip.</summary>
    public static string? Nearest(string written, IEnumerable<string> names)
    {
        var target = Normalise(written);
        var best = names
            .Select(name => (Name: name, Distance: Distance(target, Normalise(name))))
            .OrderBy(candidate => candidate.Distance)
            .FirstOrDefault();
        return best.Name is not null && best.Distance <= Math.Max(2, target.Length / 3) ? best.Name : null;
    }

    /// <summary>Levenshtein distance: the number of single-character edits between two strings.</summary>
    private static int Distance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(substitution, Math.Min(previous[j] + 1, current[j - 1] + 1));
            }

            previous = current;
        }

        return previous[b.Length];
    }
}
