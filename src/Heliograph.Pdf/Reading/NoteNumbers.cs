namespace Heliograph.Pdf.Reading;

/// <summary>
/// Notes numbered in the order the letter refers to them, as Word numbers them: footnotes 1, 2, 3 and endnotes i, ii, iii
/// unless the document says otherwise, from where it says.
/// </summary>
internal sealed class NoteNumbers(string format = "decimal", int start = 1)
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

        return ListLabels.Number(start - 1 + number, format);
    }

    /// <summary>The mark at the start of a note's text: the number of the note being read.</summary>
    public string Mark() => Current is null ? "" : Refer(Current);
}
