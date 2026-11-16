using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Messages;

namespace Heliograph;

/// <summary>
/// What an author supplies for one correspondence: a part for each channel it is sent on. Any part may be left
/// out, but not all of them.
/// </summary>
public sealed class CorrespondenceSource
{
    /// <summary>The document part: a .docx, for post, print and PDF.</summary>
    public Stream? Document { get; init; }

    /// <summary>The email part: a first line "Subject: ...", then the body.</summary>
    public string? Email { get; init; }

    /// <summary>The SMS part: plain text.</summary>
    public string? Sms { get; init; }
}

/// <summary>A piece of correspondence: one named thing with a part for each channel, all drawing on one dictionary.</summary>
public static class Correspondence
{
    /// <summary>
    /// Compiles every part against the dictionary. A mistake in any part stops the whole correspondence, and every
    /// part's problems are reported together.
    /// </summary>
    public static CorrespondenceCompilation Compile(CorrespondenceSource source, FieldDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dictionary);
        if (source is { Document: null, Email: null, Sms: null })
        {
            throw new ArgumentException("A correspondence needs at least one part: a document, an email or a text message.", nameof(source));
        }

        var document = source.Document is null ? null : DocumentTemplate.Compile(source.Document, dictionary);
        var email = source.Email is null ? null : EmailTemplate.Compile(source.Email, dictionary);
        var sms = source.Sms is null ? null : SmsTemplate.Compile(source.Sms, dictionary);
        List<Diagnostic> diagnostics = [.. document?.Diagnostics ?? [], .. email?.Diagnostics ?? [], .. sms?.Diagnostics ?? []];
        return diagnostics.Errors() > 0
            ? new CorrespondenceCompilation(null, diagnostics)
            : new CorrespondenceCompilation(new CompiledCorrespondence(document?.Document, email?.Email, sms?.Sms), diagnostics);
    }
}

/// <summary>The result of compiling a correspondence: one that can be rendered, or what is wrong with its parts.</summary>
public sealed class CorrespondenceCompilation
{
    internal CorrespondenceCompilation(CompiledCorrespondence? correspondence, IReadOnlyList<Diagnostic> diagnostics)
    {
        Correspondence = correspondence;
        Diagnostics = diagnostics;
    }

    /// <summary>The compiled correspondence, or null when any diagnostic in any part is an error.</summary>
    public CompiledCorrespondence? Correspondence { get; }

    /// <summary>Everything found in every part. Each says which part it is in.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Succeeded => Correspondence is not null;
}

/// <summary>A correspondence that has passed every check. It can be rendered any number of times, from any thread.</summary>
public sealed class CompiledCorrespondence
{
    internal CompiledCorrespondence(CompiledDocument? document, CompiledEmail? email, CompiledSms? sms)
    {
        Document = document;
        Email = email;
        Sms = sms;
    }

    /// <summary>The compiled document part, or null if the correspondence has none.</summary>
    public CompiledDocument? Document { get; }

    /// <summary>The compiled email part, or null if the correspondence has none.</summary>
    public CompiledEmail? Email { get; }

    /// <summary>The compiled SMS part, or null if the correspondence has none.</summary>
    public CompiledSms? Sms { get; }

    /// <summary>
    /// Renders every part for one recipient. When the data is wrong for any part, no part is produced, so a
    /// recipient is never sent the text without the letter it refers to.
    /// </summary>
    public RenderedCorrespondence Render(JsonElement data, RenderSettings settings)
    {
        var document = Document?.Render(data, settings);
        var email = Email?.Render(data, settings);
        var sms = Sms?.Render(data, settings);

        // The same missing value is found by every part that uses it, and is reported once.
        var diagnostics = new[] { document?.Diagnostics, email?.Diagnostics, sms?.Diagnostics }
            .SelectMany(found => found ?? []).Distinct().ToList();
        return diagnostics.Count > 0
            ? new RenderedCorrespondence(null, null, null, diagnostics)
            : new RenderedCorrespondence(document?.Docx, email, sms, []);
    }
}

/// <summary>Every part of a correspondence, rendered for one recipient.</summary>
public sealed class RenderedCorrespondence
{
    internal RenderedCorrespondence(byte[]? docx, EmailRender? email, SmsRender? sms, IReadOnlyList<Diagnostic> diagnostics)
    {
        Docx = docx;
        Email = email;
        Sms = sms;
        Diagnostics = diagnostics;
    }

    /// <summary>The finished document, or null if the correspondence has no document part or there are diagnostics.</summary>
#pragma warning disable CA1819 // The rendered file is a block of bytes; a copy per read would only cost memory.
    public byte[]? Docx { get; }
#pragma warning restore CA1819

    /// <summary>The finished email, or null if the correspondence has no email part or there are diagnostics.</summary>
    public EmailRender? Email { get; }

    /// <summary>The finished text message, or null if the correspondence has no SMS part or there are diagnostics.</summary>
    public SmsRender? Sms { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}
