namespace Heliograph;

/// <summary>How a field name is compared, and how the nearest name to a misspelt one is found.</summary>
internal static class FieldName
{
    private const string Apostrophes = "\u2018\u2019\u201B\u2032";
    private const string Quotes = "\u201C\u201D\u201E\u2033";
    private const string Dashes = "\u2010\u2011\u2012\u2013\u2014\u2212";
    private const string Invisible = "\u00AD\u200B\u200C\u200D\u2060\uFEFF";

    /// <summary>
    /// Lower case, with runs of white space collapsed to one space and the marks editors restyle folded to their plain
    /// forms, so that capitals, spacing, curly quotes and long dashes do not matter.
    /// </summary>
    public static string Normalise(string name) =>
        string.Join(' ', Fold(name).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    /// <summary>
    /// Undoes what an editor's autoformat does to typed text: curly quotes and apostrophes become straight, long
    /// dashes become hyphens, and characters that cannot be seen are dropped.
    /// </summary>
    public static string Fold(string text) => string.Concat(text.Where(c => !Invisible.Contains(c)).Select(Plain));

    private static char Plain(char c) => c switch
    {
        _ when Apostrophes.Contains(c) => '\'',
        _ when Quotes.Contains(c) => '"',
        _ when Dashes.Contains(c) => '-',
        _ => c,
    };

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
