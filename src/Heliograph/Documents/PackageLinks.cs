namespace Heliograph.Documents;

/// <summary>Follows a part's relationships to the parts they point at, inside the package only.</summary>
internal static class PackageLinks
{
    private static readonly System.Xml.Linq.XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>The bytes of the part a part's relationship points at, by its id; null for none, or one outside the file.</summary>
    public static byte[]? Linked(DocxPackage package, string partName, string relationshipId)
    {
        var folder = partName.Contains('/', StringComparison.Ordinal) ? partName[..(partName.LastIndexOf('/') + 1)] : "";
        var relationship = (package.Xml($"{folder}_rels/{partName[folder.Length..]}.rels")?.Root?.Elements(Relationships + "Relationship") ?? [])
            .FirstOrDefault(r => (string?)r.Attribute("Id") == relationshipId
                && !string.Equals((string?)r.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase));
        var target = (string?)relationship?.Attribute("Target");
        return target is null ? null : package.Bytes(target.StartsWith('/') ? target.TrimStart('/') : Normalise(folder + target));
    }

    /// <summary>Where a part's relationship to something outside the file points, such as a link's address; null for none.</summary>
    public static string? External(DocxPackage package, string partName, string relationshipId)
    {
        var folder = partName.Contains('/', StringComparison.Ordinal) ? partName[..(partName.LastIndexOf('/') + 1)] : "";
        var relationship = (package.Xml($"{folder}_rels/{partName[folder.Length..]}.rels")?.Root?.Elements(Relationships + "Relationship") ?? [])
            .FirstOrDefault(r => (string?)r.Attribute("Id") == relationshipId
                && string.Equals((string?)r.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase));
        return (string?)relationship?.Attribute("Target");
    }

    // "word/../media/a.png" as "media/a.png".
    private static string Normalise(string path)
    {
        var parts = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment == ".." && parts.Count > 0)
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else if (segment is not ("." or ".."))
            {
                parts.Add(segment);
            }
        }

        return string.Join('/', parts);
    }
}
