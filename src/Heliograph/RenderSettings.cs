using System.Globalization;

namespace Heliograph;

/// <summary>
/// Everything a render depends on besides the template and the data. The caller supplies it, so a render reads
/// nothing from the machine it runs on.
/// </summary>
/// <param name="Culture">The language and region that dates and numbers are written in: month names, and the marks in 1,234.50.</param>
public sealed record RenderSettings(CultureInfo Culture);
