namespace Heliograph.Library;

/// <summary>What came of checking a draft.</summary>
/// <param name="Diagnostics">Everything found in every part. An error means the draft cannot be published.</param>
public sealed record CheckResult(IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>True when the draft could be published as it stands.</summary>
    public bool CanPublish => Diagnostics.Errors() == 0;
}

/// <summary>What came of saving a draft.</summary>
/// <param name="Revision">The draft's new revision, or null if nothing was saved.</param>
/// <param name="Diagnostics">
/// What is wrong with the draft. A draft is saved even when it does not compile, so that unfinished work is kept;
/// only a save that someone else got in ahead of is refused.
/// </param>
public sealed record SaveResult(int? Revision, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Saved => Revision is not null;
}

/// <summary>What came of publishing a draft.</summary>
/// <param name="Version">The number of the version that was published, or null if the draft was refused.</param>
/// <param name="Diagnostics">Why it was refused, and any warnings.</param>
public sealed record PublishResult(int? Version, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Published => Version is not null;
}

/// <summary>A correspondence rendered for one recipient from the library.</summary>
/// <param name="Version">
/// The published version that was rendered, for the host to record with what it sends. Null for a preview of a
/// draft, and when nothing was rendered.
/// </param>
/// <param name="Rendered">Every part, or null when there are diagnostics.</param>
/// <param name="Diagnostics">Why nothing was rendered.</param>
public sealed record LibraryRender(int? Version, RenderedCorrespondence? Rendered, IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>What a change to a dictionary means for one correspondence that uses it.</summary>
/// <param name="Key">The correspondence's key.</param>
/// <param name="Name">What people call it.</param>
/// <param name="Diagnostics">What is now wrong with its draft. Published versions are not affected: each keeps its own dictionary.</param>
public sealed record DictionaryUse(string Key, string Name, IReadOnlyList<Diagnostic> Diagnostics);
