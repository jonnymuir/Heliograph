using System.Xml.Linq;
using static Heliograph.Pdf.Reading.WordXml;

namespace Heliograph.Pdf.Reading;

internal sealed record PageGeometry(double Width, double Height, double Top, double Bottom, double Left, double Right, double HeaderDistance, double FooterDistance)
{
    public double ContentWidth => Width - Left - Right;

    /// <summary>
    /// A section's page. A document that leaves its page out is laid out on Word's own default, as Word and LibreOffice
    /// do: US Letter with inch margins and the header and footer half an inch from the edge.
    /// </summary>
    public static PageGeometry Of(XElement sect)
    {
        var size = sect.Element(W + "pgSz");
        var margin = sect.Element(W + "pgMar");
        return new PageGeometry(
            Twips(size, "w") ?? 612, Twips(size, "h") ?? 792,
            Twips(margin, "top") ?? 72, Twips(margin, "bottom") ?? 72, Twips(margin, "left") ?? 72, Twips(margin, "right") ?? 72,
            Twips(margin, "header") ?? 36, Twips(margin, "footer") ?? 36);
    }
}
