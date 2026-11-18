namespace Heliograph.Pdf.Reading;

internal sealed record PageGeometry(double Width, double Height, double Top, double Bottom, double Left, double Right, double HeaderDistance, double FooterDistance)
{
    public double ContentWidth => Width - Left - Right;
}
