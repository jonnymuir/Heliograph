using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// An editor records who wrote and who last saved a document in its properties. A template's properties would be
/// copied into every letter rendered from it, telling each recipient the name of a member of staff.
/// </summary>
internal static class AuthorDetails
{
    private static readonly string[] Names = ["creator", "lastModifiedBy"];

    /// <summary>The properties part with the names of people emptied, keyed by its name, or nothing when there is none.</summary>
    public static KeyValuePair<string, XDocument>? Removed(DocxPackage package)
    {
        var name = package.RelationshipsIn("_rels/.rels")
            .Where(r => !r.External && r.Type.EndsWith("/metadata/core-properties", StringComparison.Ordinal))
            .Select(r => r.Target.TrimStart('/'))
            .FirstOrDefault();
        if (name is null || package.Xml(name) is not { } properties)
        {
            return null;
        }

        foreach (var element in properties.Descendants().Where(e => Names.Contains(e.Name.LocalName)))
        {
            element.Value = "";
        }

        return new KeyValuePair<string, XDocument>(name, properties);
    }
}
