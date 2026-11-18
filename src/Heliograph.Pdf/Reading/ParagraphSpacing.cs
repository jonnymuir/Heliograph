namespace Heliograph.Pdf.Reading;

/// <summary>
/// The space between two paragraphs, side by side in a story or a cell. Space before and after is left out between
/// paragraphs of the same style when the paragraph says so (Don't add space between paragraphs of the same style, as
/// Word's List Paragraph does). In a document without doNotUseHTMLParagraphAutoSpacing, as a new Word document is saved,
/// what is left is the larger of the two, not their sum.
/// </summary>
internal static class ParagraphSpacing
{
    public static (Paragraph Above, Paragraph Below) Between(Paragraph above, Paragraph below, bool larger)
    {
        // As Word lays it out: next to a paragraph of its own style, a paragraph that says so drops its own space, after
        // for the one above and before for the one below. Where the larger spacing is kept, the share of the space before
        // taken as overlap is worked out from the space after as it was, so a paragraph that drops 4 points after, above
        // one with 9 before, is 5 points from it. LibreOffice differs, and Word is followed (docs/pdf-engine.md).
        var after = above.Style.Spacing.After;
        if (above.Style.StyleId == below.Style.StyleId)
        {
            above = above.Style.Contextual ? above with { Style = above.Style with { Spacing = above.Style.Spacing with { After = 0 } } } : above;
            below = below.Style.Contextual ? below with { Style = below.Style with { Spacing = below.Style.Spacing with { Before = 0 } } } : below;
        }

        var overlap = larger ? Math.Min(below.Style.Spacing.Before, after) : 0;
        return (above, below with { Style = below.Style with { Overlap = overlap } });
    }
}
