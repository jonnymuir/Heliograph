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
        // As LibreOffice lays it out: a paragraph that says so drops its space after before one of its style. Its space
        // before goes too when the one above says so, or, where the larger spacing is kept, whenever it says so itself. With
        // Word's List Paragraph the style says so, and both drop theirs either way.
        if (above.Style.StyleId == below.Style.StyleId)
        {
            above = above.Style.Contextual ? above with { Style = above.Style with { Spacing = above.Style.Spacing with { After = 0 } } } : above;
            var dropBefore = below.Style.Contextual && (above.Style.Contextual || larger);
            below = dropBefore ? below with { Style = below.Style with { Spacing = below.Style.Spacing with { Before = 0 } } } : below;
        }

        var overlap = larger ? Math.Min(below.Style.Spacing.Before, above.Style.Spacing.After) : 0;
        return (above, below with { Style = below.Style with { Overlap = overlap } });
    }
}
