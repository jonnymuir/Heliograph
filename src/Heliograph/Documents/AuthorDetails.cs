using System.Xml.Linq;

namespace Heliograph.Documents;

/// <summary>
/// An editor records people in a document: who wrote it, who last saved it, who commented, and sometimes a company
/// and a manager. A template's records would be copied into every letter rendered from it, telling each recipient
/// the names of members of staff.
/// </summary>
internal static class AuthorDetails
{
    private static readonly string[] CoreNames = ["creator", "lastModifiedBy"];
    private static readonly string[] ExtendedNames = ["Company", "Manager"];

    /// <summary>Every part that names people, with those names emptied, keyed by part name.</summary>
    public static IEnumerable<KeyValuePair<string, XDocument>> Removed(DocxPackage package)
    {
        foreach (var (suffix, names) in new[] { ("/metadata/core-properties", CoreNames), ("/extended-properties", ExtendedNames) })
        {
            if (Properties(package, suffix) is var (name, properties))
            {
                properties.Descendants().Where(e => names.Contains(e.Name.LocalName)).ToList().ForEach(e => e.Value = "");
                yield return new KeyValuePair<string, XDocument>(name, properties);
            }
        }

        // The list of people who commented on the document or tracked changes in it.
        foreach (var name in package.Names.Where(n => n.EndsWith("/people.xml", StringComparison.OrdinalIgnoreCase)))
        {
            var people = package.Xml(name)!;
            people.Root?.RemoveNodes();
            yield return new KeyValuePair<string, XDocument>(name, people);
        }
    }

    private static (string Name, XDocument Properties)? Properties(DocxPackage package, string relationshipSuffix)
    {
        var name = package.RelationshipsIn("_rels/.rels")
            .Where(r => !r.External && r.Type.EndsWith(relationshipSuffix, StringComparison.Ordinal))
            .Select(r => r.Target.TrimStart('/'))
            .FirstOrDefault();
        return name is not null && package.Xml(name) is { } properties ? (name, properties) : null;
    }
}
