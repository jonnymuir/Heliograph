using System.Globalization;

namespace Heliograph.Pdf.Reading;

/// <summary>Footnotes numbered 1, 2, 3 in the order the letter refers to them, as Word numbers them.</summary>
internal sealed class NoteNumbers
{
    private readonly Dictionary<string, int> _numbers = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    /// <summary>The notes' ids in the order they are referred to.</summary>
    public IReadOnlyList<string> Order => _order;

    /// <summary>The note whose text is being read, whose own mark is its number.</summary>
    public string? Current { get; set; }

    public string Refer(string id)
    {
        if (!_numbers.TryGetValue(id, out var number))
        {
            number = _numbers.Count + 1;
            _numbers[id] = number;
            _order.Add(id);
        }

        return number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The mark at the start of a note's text: the number of the note being read.</summary>
    public string Mark() => Current is null ? "" : Refer(Current);
}
