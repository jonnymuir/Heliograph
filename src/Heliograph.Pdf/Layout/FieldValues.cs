using Heliograph.Pdf.Reading;

namespace Heliograph.Pdf.Layout;

/// <summary>
/// How fields are worked out in the text being laid out, where that is not passed along: in a table in a header or footer,
/// the page number and counts of that page. Anywhere else a table's fields are empty, as the check allows none there.
/// </summary>
internal static class FieldValues
{
    private static readonly AsyncLocal<Func<FieldInline, string>?> Current = new();

    /// <summary>The fields' values in the text being laid out now.</summary>
    public static string Of(FieldInline field) => Current.Value?.Invoke(field) ?? "";

    /// <summary>Works fields out this way until the returned scope ends.</summary>
    public static IDisposable Use(Func<FieldInline, string> values)
    {
        var before = Current.Value;
        Current.Value = values;
        return new Scope(() => Current.Value = before);
    }

    private sealed class Scope(Action end) : IDisposable
    {
        public void Dispose() => end();
    }
}
