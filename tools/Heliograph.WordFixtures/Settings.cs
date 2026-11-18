namespace Heliograph.WordFixtures;

/// <summary>The app registration the tool signs in through: a public client, so its ID is not a secret.</summary>
internal static class Settings
{
    public const string ClientId = "";
}

/// <summary>Finds the repository's root from wherever the tool runs.</summary>
internal static class Repository
{
    public static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Heliograph.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Run the tool from inside the Heliograph repository.");
    }
}
