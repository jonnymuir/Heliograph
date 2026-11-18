namespace Heliograph.Pdf.Layout;

/// <summary>A letter's notes as its lines are placed: footnotes kept room for on their page, endnotes noted for later.</summary>
internal sealed class PageNotes(Footnotes footnotes, Endnotes endnotes)
{
    public Footnotes Footnotes { get; } = footnotes;

    public Endnotes Endnotes { get; } = endnotes;

    public void Place(Line line, StructNode? owner)
    {
        Footnotes.Place(line, owner);
        Endnotes.Place(line, owner);
    }
}
