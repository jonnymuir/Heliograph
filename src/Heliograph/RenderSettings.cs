using System.Globalization;

namespace Heliograph;

/// <summary>
/// Everything a render depends on besides the template and the data. The caller supplies it, so a render reads
/// nothing from the machine it runs on.
/// </summary>
/// <param name="Culture">The language and region that dates, numbers and money are written in, including the currency.</param>
public sealed record RenderSettings(CultureInfo Culture);
