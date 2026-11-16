using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Heliograph.Library;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Heliograph.Mcp;

/// <summary>
/// The authoring operations as MCP tools. Each calls <see cref="CorrespondenceLibrary"/> in the same process as the
/// host's screens and API, so an agent and a person work on the same drafts and get the same diagnostics.
/// </summary>
/// <remarks>
/// Rendering a published version for a real recipient is not offered here. It is the host's run-time call, and it
/// carries personal data that has no business passing through an authoring conversation.
/// </remarks>
[McpServerToolType]
public static class CorrespondenceAuthoringTools
{
    private static readonly string[] PartNames = ["document", "email", "sms"];

    [McpServerTool(Name = "list_correspondence", ReadOnly = true)]
    [Description("List every correspondence: its key, name, the dictionary it uses and its newest published version (null if never published).")]
    public static Task<IReadOnlyList<CorrespondenceSummary>> ListCorrespondence(CorrespondenceLibrary library, CancellationToken ct) =>
        library.ListAsync(ct);

    [McpServerTool(Name = "list_dictionaries", ReadOnly = true)]
    [Description("List the names of the dictionaries a correspondence can use. A dictionary is the closed list of fields a template may refer to.")]
    public static Task<IReadOnlyList<string>> ListDictionaries(CorrespondenceLibrary library, CancellationToken ct) =>
        library.ListDictionariesAsync(ct);

    [McpServerTool(Name = "read_dictionary", ReadOnly = true)]
    [Description(
        "Read a dictionary: every field a template may use, with the tag to write for it, ready to copy. Call this before " +
        "writing or changing a template, and use the tags exactly as given. A field with a 'list' can only go in a table " +
        "row (document) or on a line of its own (email, text), which repeats for each item. A yes or no field is never " +
        "printed: use it as ((if Field)) ... ((end if)).")]
    public static async Task<DictionarySheet> ReadDictionary(
        CorrespondenceLibrary library,
        [Description("The dictionary's name, from list_dictionaries or a correspondence's 'dictionary'.")] string name,
        CancellationToken ct)
    {
        var json = await library.ReadDictionaryAsync(name, ct).ConfigureAwait(false)
            ?? throw new McpException($"There is no dictionary called \"{name}\". Call list_dictionaries to see the names that exist.");
        try
        {
            var dictionary = FieldDictionary.Parse(json);
            return new DictionarySheet(name, [.. dictionary.AllFields.Select(field => new DictionaryEntry(
                field.Type == FieldType.YesNo ? $"((if {field.Name}))" : field.Type == FieldType.List ? null : $"(({field.Name}))",
                field.Name, field.Key, TypeName(field.Type), dictionary.ListOf(field)?.Name))]);
        }
        catch (FormatException e)
        {
            throw new McpException($"The dictionary \"{name}\" cannot be read. {e.Message}");
        }
    }

    [McpServerTool(Name = "read_correspondence", ReadOnly = true)]
    [Description(
        "Read the draft of a correspondence: its name, dictionary, the email and text message as text, whether it has a " +
        "document, and its revision. Pass the revision to save_correspondence. Returns null if there is none with this key.")]
    public static async Task<DraftView?> ReadCorrespondence(
        CorrespondenceLibrary library,
        [Description("The correspondence's key, from list_correspondence.")] string key,
        [Description("True to include the document (.docx) as base64. It can be large; leave false unless you need the file.")] bool includeDocument = false,
        CancellationToken ct = default)
    {
        if (await library.ReadDraftAsync(key, ct).ConfigureAwait(false) is not { } draft)
        {
            return null;
        }

        var newest = await library.ReadVersionAsync(key, null, ct).ConfigureAwait(false);
        return new DraftView(
            draft.Key, draft.Revision, draft.Content.Name, draft.Content.Dictionary, draft.Content.Email, draft.Content.Sms,
            draft.Content.Document is not null,
            includeDocument && draft.Content.Document is { } document ? Convert.ToBase64String(document) : null,
            newest?.Version);
    }

    [McpServerTool(Name = "save_correspondence", Destructive = false)]
    [Description(
        "Save the draft of a correspondence, and get back everything wrong with it. A draft is saved even when it has " +
        "errors; it just cannot be published until they are fixed. Parts you do not pass are kept as they are. Saving " +
        "changes only the draft: recipients are sent the published version until you publish again. For a new " +
        "correspondence pass expectedRevision 0, a name and a dictionary.")]
    public static async Task<SaveView> SaveCorrespondence(
        CorrespondenceLibrary library,
        [Description("The key: lower-case letters, digits and hyphens, such as \"decision-letter\".")] string key,
        [Description("The revision read_correspondence returned, or 0 for a new correspondence. If someone else has saved since, nothing is saved.")] int expectedRevision,
        [Description("What people call it, such as \"Decision letter\". Needed for a new correspondence.")] string? name = null,
        [Description("The name of the dictionary it uses. Needed for a new correspondence.")] string? dictionary = null,
        [Description("The email part: a first line \"Subject: ...\", a blank line, then the body in the small set of Markdown the guide describes.")] string? email = null,
        [Description("The text message part, as plain text.")] string? sms = null,
        [Description("The document part: a .docx file as base64. Written in a word processor; you cannot make one from text.")] string? documentBase64 = null,
        [Description("Parts to remove: any of \"document\", \"email\", \"sms\".")] string[]? remove = null,
        CancellationToken ct = default)
    {
        remove ??= [];
        if (remove.Except(PartNames, StringComparer.Ordinal).FirstOrDefault() is { } unknown)
        {
            throw new McpException($"\"{unknown}\" is not a part. The parts are \"document\", \"email\" and \"sms\".");
        }

        var existing = (await library.ReadDraftAsync(key, ct).ConfigureAwait(false))?.Content;
        if (existing is null && (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dictionary)))
        {
            throw new McpException("A new correspondence needs a name and a dictionary. Call list_dictionaries to see the dictionaries.");
        }

        var content = new CorrespondenceContent
        {
            Name = name ?? existing!.Name,
            Dictionary = dictionary ?? existing!.Dictionary,
            Document = Kept("document", remove, documentBase64 is null ? null : Decode(documentBase64), existing?.Document),
            Email = Kept("email", remove, email, existing?.Email),
            Sms = Kept("sms", remove, sms, existing?.Sms),
        };
        var save = await library.SaveDraftAsync(key, content, expectedRevision, ct).ConfigureAwait(false);
        return new SaveView(save.Saved, save.Revision, save.Saved && save.Diagnostics.All(d => d.Severity != DiagnosticSeverity.Error), Problems(save.Diagnostics));
    }

    /// <summary>A part as it will be saved: removed if asked, replaced if given, otherwise as it was.</summary>
    private static T? Kept<T>(string part, string[] remove, T? given, T? was)
        where T : class =>
        remove.Contains(part) ? null : given ?? was;

    [McpServerTool(Name = "check_correspondence", ReadOnly = true)]
    [Description("Compile the draft and list everything wrong with it, each with where it is and how to fix it. An error stops it being published; a warning does not.")]
    public static async Task<CheckView> CheckCorrespondence(
        CorrespondenceLibrary library,
        [Description("The correspondence's key.")] string key,
        CancellationToken ct)
    {
        var check = await library.CheckAsync(key, ct).ConfigureAwait(false);
        return new CheckView(check.CanPublish, Problems(check.Diagnostics));
    }

    [McpServerTool(Name = "check_dictionary", ReadOnly = true)]
    [Description("List every correspondence that uses a dictionary, with what is wrong with each draft against the dictionary as it is now. Use it after a dictionary changes.")]
    public static async Task<IReadOnlyList<DictionaryUseView>> CheckDictionary(
        CorrespondenceLibrary library,
        [Description("The dictionary's name.")] string name,
        CancellationToken ct) =>
        [.. (await library.CheckDictionaryAsync(name, ct).ConfigureAwait(false)).Select(use => new DictionaryUseView(use.Key, use.Name, Problems(use.Diagnostics)))];

    [McpServerTool(Name = "preview_correspondence", ReadOnly = true)]
    [Description(
        "Render the draft for one made-up recipient, so you can read each part as they would: the email's subject, HTML " +
        "and plain text, and the text message with the number of messages it will be charged as. The data is a JSON object " +
        "keyed by each field's 'key' from read_dictionary: text as a string, a date as \"2027-06-19\", money and numbers as " +
        "numbers, yes or no as true or false, an address as a list of lines, a list as an array of objects. Use invented " +
        "people only.")]
    public static async Task<PreviewView> PreviewCorrespondence(
        CorrespondenceLibrary library,
        [Description("The correspondence's key.")] string key,
        [Description("Sample data for one recipient, as a JSON object.")] string dataJson,
        [Description("The language and region for dates and numbers, such as \"en-GB\".")] string culture = "en-GB",
        [Description("True to include the rendered document (.docx) as base64.")] bool includeDocument = false,
        CancellationToken ct = default)
    {
        using var data = ParseData(dataJson);
        var preview = await library.PreviewAsync(key, data.RootElement, new RenderSettings(Culture(culture)), ct).ConfigureAwait(false);
        var rendered = preview.Rendered;
        return new PreviewView(
            rendered?.Email is { } email ? new EmailView(email.Subject!, email.Html!, email.Text!) : null,
            rendered?.Sms is { } sms ? new SmsView(sms.Text!, sms.Encoding.ToString(), sms.Segments) : null,
            rendered?.Docx is not null,
            includeDocument && rendered?.Docx is { } docx ? Convert.ToBase64String(docx) : null,
            Problems(preview.Diagnostics));
    }

    [McpServerTool(Name = "publish_correspondence", Destructive = false)]
    [Description(
        "Publish the draft as the next numbered version. From then on it is what recipients are sent. A draft with an " +
        "error is refused. A version never changes and cannot be unpublished; to correct one, publish another. Only " +
        "publish when the person has asked you to.")]
    public static async Task<PublishView> PublishCorrespondence(
        CorrespondenceLibrary library,
        [Description("The correspondence's key.")] string key,
        CancellationToken ct)
    {
        var publish = await library.PublishAsync(key, ct).ConfigureAwait(false);
        return new PublishView(publish.Published, publish.Version, Problems(publish.Diagnostics));
    }

    private static IReadOnlyList<ProblemView> Problems(IReadOnlyList<Diagnostic> diagnostics) => [.. diagnostics.Select(d => new ProblemView(
        d.Code, d.Severity == DiagnosticSeverity.Error ? "error" : "warning", d.Message, d.Fix, d.Location?.Part, d.Location?.Paragraph, d.Location?.Excerpt))];

    private static string TypeName(FieldType type) => type switch
    {
        FieldType.YesNo => "yes or no",
        _ => type.ToString().ToLowerInvariant(),
    };

    private static byte[] Decode(string base64)
    {
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new McpException("documentBase64 is not valid base64. Pass the .docx file's bytes, base64-encoded.");
        }
    }

    private static JsonDocument ParseData(string json)
    {
        try
        {
            var data = JsonDocument.Parse(json);
            return data.RootElement.ValueKind == JsonValueKind.Object
                ? data
                : throw new McpException("dataJson must be a JSON object keyed by each field's key.");
        }
        catch (JsonException e)
        {
            throw new McpException($"dataJson is not valid JSON. {e.Message}");
        }
    }

    private static CultureInfo Culture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name, predefinedOnly: true);
        }
        catch (CultureNotFoundException)
        {
            throw new McpException($"\"{name}\" is not a language and region. Use a name such as \"en-GB\" or \"cy-GB\".");
        }
    }
}

/// <summary>A dictionary laid out for an author: each field with the tag to write for it.</summary>
public sealed record DictionarySheet(string Name, IReadOnlyList<DictionaryEntry> Fields);

/// <param name="Tag">What to write in a template. Null for a list, which is never written itself.</param>
/// <param name="Name">The field's name.</param>
/// <param name="Key">The key the data carries the value under.</param>
/// <param name="Type">text, date, address, number, money, list, or yes or no.</param>
/// <param name="List">The list this field belongs to, or null.</param>
public sealed record DictionaryEntry(string? Tag, string Name, string Key, string Type, string? List);

public sealed record DraftView(
    string Key, int Revision, string Name, string Dictionary, string? Email, string? Sms, bool HasDocument, string? DocumentBase64, int? LatestVersion);

/// <summary>One diagnostic, as an agent is given it.</summary>
public sealed record ProblemView(string Code, string Severity, string Message, string Fix, string? Part, int? Paragraph, string? Excerpt);

public sealed record SaveView(bool Saved, int? Revision, bool CanPublish, IReadOnlyList<ProblemView> Problems);

public sealed record CheckView(bool CanPublish, IReadOnlyList<ProblemView> Problems);

public sealed record DictionaryUseView(string Key, string Name, IReadOnlyList<ProblemView> Problems);

public sealed record EmailView(string Subject, string Html, string Text);

public sealed record SmsView(string Text, string Encoding, int Segments);

public sealed record PreviewView(EmailView? Email, SmsView? Sms, bool HasDocument, string? DocumentBase64, IReadOnlyList<ProblemView> Problems);

public sealed record PublishView(bool Published, int? Version, IReadOnlyList<ProblemView> Problems);
