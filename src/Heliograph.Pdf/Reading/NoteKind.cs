namespace Heliograph.Pdf.Reading;

/// <summary>A kind of note: the part its text is in, its element there, its settings, and how it is numbered by default.</summary>
internal sealed record NoteKind(string Part, string Element, string Settings, string Format)
{
    public static readonly NoteKind Footnote = new("footnotes", "footnote", "footnotePr", "decimal");

    public static readonly NoteKind Endnote = new("endnotes", "endnote", "endnotePr", "lowerRoman");
}
