using System.Globalization;

namespace Heliograph.Tests.Support;

/// <summary>
/// The pictures RandomLayout sets in line with the text: one small PNG, drawn at sizes from a little taller than the text
/// to most of a column, after any run of a paragraph.
/// </summary>
internal static class RandomPictures
{
    /// <summary>The relationship id RandomLayout gives its picture: after styles, numbering, settings, two headers and a footer.</summary>
    public const string RelationshipId = "rIdX6";

    public static readonly byte[] Png = TestPng.Write(4, 4, 2, 8, (x, y) => [x * 60, y * 60, 200]);

    /// <summary>The paragraph with a picture set after one of its runs, chosen at random.</summary>
    public static string Into(string paragraph, Random random)
    {
        var ends = new List<int>();
        for (var at = paragraph.IndexOf("</w:r>", StringComparison.Ordinal); at >= 0; at = paragraph.IndexOf("</w:r>", at + 1, StringComparison.Ordinal))
        {
            ends.Add(at + "</w:r>".Length);
        }

        var place = ends.Count == 0 ? paragraph.LastIndexOf("</w:p>", StringComparison.Ordinal) : ends[random.Next(ends.Count)];
        return paragraph.Insert(place, Run(random.Next(20, 200), random.Next(10, 150)));
    }

    private static string Run(int width, int height)
    {
        var (cx, cy) = ((width * 12700).ToString(CultureInfo.InvariantCulture), (height * 12700).ToString(CultureInfo.InvariantCulture));
        return "<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\">"
            + $"<wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:docPr id=\"1\" name=\"Picture\" descr=\"Squares in shades of blue\"/>"
            + "<a:graphic xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">"
            + "<pic:pic xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:nvPicPr><pic:cNvPr id=\"1\" name=\"Picture\"/><pic:cNvPicPr/></pic:nvPicPr>"
            + $"<pic:blipFill><a:blip xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" r:embed=\"{RelationshipId}\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>"
            + $"<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>"
            + "</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
    }
}
