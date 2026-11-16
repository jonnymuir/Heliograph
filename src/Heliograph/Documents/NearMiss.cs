using System.Text.RegularExpressions;

namespace Heliograph.Documents;

/// <summary>
/// Finds text that looks like a tag for a real field but is not one: the field's name with the wrong brackets, or
/// with one bracket missing. Nothing fails when a tag is not recognised, so this is the only warning an author gets.
/// </summary>
internal sealed class NearMiss
{
    private const string Opening = @"[(\[{<«（]";
    private const string Closing = @"[)\]}>»）]";

    private readonly List<(Field Field, Regex Pattern)> _patterns;

    public NearMiss(FieldDictionary dictionary)
    {
        _patterns = [.. dictionary.AllFields.Select(field => (field, Pattern(field)))];
    }

    /// <summary>The first field whose name appears beside a bracket in text that holds no recognised tag.</summary>
    public Field? In(string text)
    {
        var folded = FieldName.Fold(text);
        return _patterns.FirstOrDefault(p => p.Pattern.IsMatch(folded)).Field;
    }

    private static Regex Pattern(Field field)
    {
        var words = FieldName.Normalise(field.Name).Split(' ').Select(Regex.Escape);
        var name = $@"(?:if\s+)?(?<!\w){string.Join(@"\s+", words)}(?!\w)";
        return new Regex($@"{Opening}\s*{name}|{name}\s*{Closing}",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}
