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
        var (size, margin) = (sect.Element(W + "pgSz"), sect.Element(W + "pgMar"));
        double Or(XElement? e, string name, double otherwise) => Twips(e, name) ?? otherwise;
        return new PageGeometry(
            Or(size, "w", 612), Or(size, "h", 792), Or(margin, "top", 72), Or(margin, "bottom", 72), Or(margin, "left", 72), Or(margin, "right", 72),
            Or(margin, "header", 36), Or(margin, "footer", 36));
    }
}
