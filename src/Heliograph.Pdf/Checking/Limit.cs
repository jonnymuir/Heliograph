using System.Xml.Linq;

namespace Heliograph.Pdf.Checking;

/// <summary>
/// Something the converter cannot lay out yet: how to find it in a part, and what to tell the author. Found is given
/// the part's root and returns each place it occurs with a word or two naming what is there.
/// </summary>
internal sealed record Limit(string Code, Func<XElement, IEnumerable<(XElement Place, string What)>> Found, Func<string, string> Message, string Fix, bool Warning = false);
