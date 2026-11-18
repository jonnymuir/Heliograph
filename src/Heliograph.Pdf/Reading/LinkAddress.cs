namespace Heliograph.Pdf.Reading;

/// <summary>
/// The addresses a link in a PDF may go to: web pages, email and telephone numbers. Anything else, a local file or a
/// script say, is refused at compile, so a PDF never carries it.
/// </summary>
internal static class LinkAddress
{
    private static readonly string[] Schemes = ["http", "https", "mailto", "tel"];

    /// <summary>The address as a PDF link holds it (absolute, non-ASCII escaped), or null when it is not one of those.</summary>
    public static string? Safe(string? target) =>
        Uri.TryCreate(target, UriKind.Absolute, out var uri) && Schemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase) && uri.AbsoluteUri.All(char.IsAscii)
            ? uri.AbsoluteUri
            : null;
}
