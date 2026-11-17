using System.Text.Json;
using Heliograph.Library;

namespace Heliograph.Api;

/// <summary>A draft as the workbench shows it. The document travels on its own endpoint, as a file.</summary>
public sealed record DraftResponse(
    string Key, int Revision, string Name, string Dictionary, string? Email, string? Sms, bool HasDocument, int? LatestVersion);

/// <summary>A change to a draft's name, dictionary or text parts. A part that is not given is kept.</summary>
/// <param name="ExpectedRevision">The revision the change was made to, or 0 for a new correspondence.</param>
/// <param name="Remove">Parts to take out: any of "document", "email", "sms".</param>
public sealed record SaveDraftRequest(int ExpectedRevision, string? Name, string? Dictionary, string? Email, string? Sms, IReadOnlyList<string>? Remove);

/// <summary>Data for a preview: a sample's name, or data written out. The language and region default to en-GB.</summary>
public sealed record PreviewRequest(string? Sample, JsonElement? Data, string? Culture);

/// <summary>A run-time render for one recipient: their data, the language, and a version, or the newest.</summary>
public sealed record RenderRequest(JsonElement Data, string? Culture, int? Version, bool IncludeDocument);

/// <summary>The email and text parts of a preview or render, and whether there is a document.</summary>
public sealed record PartsResponse(int? Version, EmailResponse? Email, SmsResponse? Sms, bool HasDocument, string? DocumentBase64, IReadOnlyList<Diagnostic> Diagnostics)
{
    public static PartsResponse From(LibraryRender render, bool includeDocument) => new(
        render.Version,
        render.Rendered?.Email is { } email ? new EmailResponse(email.Subject!, email.Html!, email.Text!) : null,
        render.Rendered?.Sms is { } sms ? new SmsResponse(sms.Text!, sms.Encoding.ToString(), sms.Segments) : null,
        render.Rendered?.Docx is not null,
        includeDocument && render.Rendered?.Docx is { } docx ? Convert.ToBase64String(docx) : null,
        render.Diagnostics);
}

public sealed record EmailResponse(string Subject, string Html, string Text);

public sealed record SmsResponse(string Text, string Encoding, int Segments);
