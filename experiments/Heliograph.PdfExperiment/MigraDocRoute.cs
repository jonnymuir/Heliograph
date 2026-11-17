using Heliograph.PdfExperiment.OwnEngine;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using MdParagraph = MigraDoc.DocumentObjectModel.Paragraph;
using MdSection = MigraDoc.DocumentObjectModel.Section;
using WordParagraph = Heliograph.PdfExperiment.OwnEngine.Paragraph;
using WordTable = Heliograph.PdfExperiment.OwnEngine.Table;
using WordBorder = Heliograph.PdfExperiment.OwnEngine.Border;

namespace Heliograph.PdfExperiment;

/// <summary>
/// The same Word model, laid out by MigraDoc and written by PDFsharp 6.2.4 (the last MIT release). Only the translation
/// is ours; line breaking, pages, tables and the PDF are theirs.
/// </summary>
public static class MigraDocRoute
{
    public static void UseFonts(string folder) => GlobalFontSettings.FontResolver ??= new LiberationResolver(folder);

    public static byte[] Convert(WordDocument word)
    {
        var document = new Document();
        document.Info.Title = word.Title;
        var section = document.AddSection();
        var s = word.Section;
        var setup = section.PageSetup;
        setup.PageWidth = Unit.FromPoint(s.Width);
        setup.PageHeight = Unit.FromPoint(s.Height);
        setup.TopMargin = Unit.FromPoint(s.Top);
        setup.BottomMargin = Unit.FromPoint(s.Bottom);
        setup.LeftMargin = Unit.FromPoint(s.Left);
        setup.RightMargin = Unit.FromPoint(s.Right);
        setup.HeaderDistance = Unit.FromPoint(s.HeaderDistance);
        setup.FooterDistance = Unit.FromPoint(s.FooterDistance);
        setup.DifferentFirstPageHeaderFooter = s.TitlePage;

        Story(section.Headers.Primary, s.Header);
        Story(section.Footers.Primary, s.Footer);
        if (s.TitlePage)
        {
            Story(section.Headers.FirstPage, s.FirstHeader);
            Story(section.Footers.FirstPage, s.FirstFooter);
        }

        foreach (var block in word.Body)
        {
            if (block is WordParagraph p) Fill(section.AddParagraph(), p);
            else if (block is WordTable t) AddTable(section, t);
        }

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var output = new MemoryStream();
        renderer.PdfDocument.Save(output);
        return output.ToArray();
    }

    private static void Story(HeaderFooter target, IReadOnlyList<Block> blocks)
    {
        foreach (var p in blocks.OfType<WordParagraph>()) Fill(target.AddParagraph(), p);
    }

    private static void Fill(MdParagraph target, WordParagraph p)
    {
        var s = p.Style;
        var f = target.Format;
        f.SpaceBefore = Unit.FromPoint(s.Before);
        f.SpaceAfter = Unit.FromPoint(s.After);
        f.LeftIndent = Unit.FromPoint(s.Left);
        f.RightIndent = Unit.FromPoint(s.Right);
        f.FirstLineIndent = Unit.FromPoint(s.FirstLine);
        f.KeepWithNext = s.KeepNext;
        f.KeepTogether = s.KeepLines;
        f.WidowControl = s.WidowControl;
        f.PageBreakBefore = s.PageBreakBefore;
        f.Alignment = s.Align switch { "center" => ParagraphAlignment.Center, "right" => ParagraphAlignment.Right, "both" => ParagraphAlignment.Justify, _ => ParagraphAlignment.Left };
        (f.LineSpacingRule, f.LineSpacing) = s.LineRule switch
        {
            "exact" => (LineSpacingRule.Exactly, Unit.FromPoint(s.Line / 20)),
            "atLeast" => (LineSpacingRule.AtLeast, Unit.FromPoint(s.Line / 20)),
            _ => (LineSpacingRule.Multiple, new Unit(s.Line / 240)),
        };
        f.Font.Name = "Liberation Sans";
        f.Font.Size = Unit.FromPoint(p.MarkStyle.Size);
        foreach (var tab in s.Tabs) f.TabStops.AddTabStop(Unit.FromPoint(tab));
        if (s.FirstLine < 0) f.TabStops.AddTabStop(Unit.FromPoint(s.Left));

        if (p.Label is TextInline label)
        {
            Text(target, label.Text, label.Style);
            target.AddTab();
        }

        foreach (var inline in p.Inlines)
        {
            switch (inline)
            {
                case TextInline t: Text(target, t.Text, t.Style); break;
                case TabInline: target.AddTab(); break;
                case BreakInline: target.AddLineBreak(); break;
                case FieldInline { Field: "PAGE" }: target.AddPageField(); break;
                case FieldInline { Field: "NUMPAGES" }: target.AddNumPagesField(); break;
            }
        }
    }

    private static void Text(MdParagraph target, string text, RunStyle style)
    {
        var run = target.AddFormattedText(text);
        run.Font.Name = "Liberation Sans";
        run.Font.Size = Unit.FromPoint(style.Size);
        run.Font.Bold = style.Bold;
        run.Font.Italic = style.Italic;
        if (style.Underline) run.Font.Underline = Underline.Single;
    }

    private static void AddTable(MdSection section, WordTable t)
    {
        var table = section.AddTable();
        table.Rows.LeftIndent = Unit.FromPoint(t.Indent);
        table.LeftPadding = Unit.FromPoint(t.MarginLeft);
        table.RightPadding = Unit.FromPoint(t.MarginRight);
        table.TopPadding = Unit.FromPoint(t.MarginTop);
        table.BottomPadding = Unit.FromPoint(t.MarginBottom);
        foreach (var width in t.Grid) table.AddColumn(Unit.FromPoint(width));
        foreach (var row in t.Rows)
        {
            var target = table.AddRow();
            target.HeadingFormat = row.Header;
            var column = 0;
            foreach (var cell in row.Cells)
            {
                var c = target.Cells[column];
                if (cell.Span > 1) c.MergeRight = cell.Span - 1;
                column += cell.Span;
                void Side(WordBorder? border, MigraDoc.DocumentObjectModel.Border side)
                {
                    if (border is null) return;
                    side.Width = Unit.FromPoint(border.Width);
                    side.Color = Colors.Black;
                }

                Side(cell.Top, c.Borders.Top);
                Side(cell.Bottom, c.Borders.Bottom);
                Side(cell.Left, c.Borders.Left);
                Side(cell.Right, c.Borders.Right);
                foreach (var p in cell.Blocks.OfType<WordParagraph>()) Fill(c.AddParagraph(), p);
            }
        }
    }

    private sealed class LiberationResolver(string folder) : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new((isBold, isItalic) switch
            {
                (true, true) => "LiberationSans-BoldItalic",
                (true, false) => "LiberationSans-Bold",
                (false, true) => "LiberationSans-Italic",
                _ => "LiberationSans-Regular",
            });

        public byte[] GetFont(string faceName) => File.ReadAllBytes(Path.Combine(folder, faceName + ".ttf"));
    }
}
