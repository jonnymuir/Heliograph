using System.Text.Json;
using System.Text.RegularExpressions;
using Heliograph.Documents;

namespace Heliograph.Messages;

/// <summary>
/// The email part of a correspondence: a subject of one line, and a body in a small, fixed set of Markdown:
/// headings (# and ##), bullets (- or *), numbered items (1.), a rule (---), **bold** and [links](address).
/// </summary>
public static partial class EmailTemplate
{
    private const string SubjectPart = "email subject";
    private const string BodyPart = "email";
    private const string SubjectLine = "Subject:";
    // Stand-ins used only while a line is checked: for a value, and for where a condition starts or ends.
    private const char Value = (char)0xFFFC;
    private const char ConditionEdge = (char)0xFFFB;

    /// <summary>Compiles an email kept as one text: a first line "Subject: ...", then the body.</summary>
    public static EmailCompilation Compile(string source, FieldDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(source);

        var lines = source.ReplaceLineEndings("\n").Split('\n');
        return lines[0].StartsWith(SubjectLine, StringComparison.OrdinalIgnoreCase)
            ? Compile(lines[0][SubjectLine.Length..].Trim(), string.Join('\n', lines.Skip(1)), dictionary, linesBefore: 1)
            : new EmailCompilation(null, [NoSubject($"The first line must start with \"{SubjectLine}\".", new DiagnosticLocation(BodyPart, 1, lines[0]))]);
    }

    /// <summary>Compiles an email from its subject and its body.</summary>
    public static EmailCompilation Compile(string subject, string body, FieldDictionary dictionary) =>
        Compile(subject, body, dictionary, linesBefore: 0);

    private static EmailCompilation Compile(string subject, string body, FieldDictionary dictionary, int linesBefore)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(dictionary);

        if (string.IsNullOrWhiteSpace(subject) || subject.Trim().ReplaceLineEndings("\n").Contains('\n', StringComparison.Ordinal))
        {
            return new EmailCompilation(null, [NoSubject("An email needs a subject, on one line.", new DiagnosticLocation(SubjectPart, 1, subject.Trim()))]);
        }

        var diagnostics = new List<Diagnostic>();
        var subjectTemplate = TextTemplate.Parse(subject.Trim(), SubjectPart, dictionary, diagnostics);
        var bodyTemplate = TextTemplate.Parse(body, BodyPart, dictionary, diagnostics, linesBefore);
        diagnostics.AddRange(subjectTemplate.Lines.Where(line => line.RepeatsOver is not null).Select(line => new Diagnostic(
            DiagnosticCodes.ListFieldOutsideRow,
            $"The subject uses a field from the list \"{line.RepeatsOver!.Name}\". A subject is one line and cannot repeat.",
            "Put the list's fields on a line of the body.", line.Location)));
        diagnostics.AddRange(bodyTemplate.Lines.SelectMany(UnsafeLinks));
        return diagnostics.Errors() > 0
            ? new EmailCompilation(null, diagnostics)
            : new EmailCompilation(new CompiledEmail(subjectTemplate, bodyTemplate, dictionary), diagnostics);
    }

    /// <summary>
    /// A link's address must start with something the author wrote that plainly says where it goes. One that starts
    /// with a value, or with anything but a web, email or phone scheme, could be made to go anywhere, or to run script.
    /// </summary>
    private static IEnumerable<Diagnostic> UnsafeLinks(TextLine line)
    {
        var written = string.Concat(Flatten(line.Nodes));
        return LinkAddress().Matches(written).Select(match => match.Groups[1].Value)
            .Where(address => !InlineMarkdown.IsSafe(address) || address.Contains(ConditionEdge, StringComparison.Ordinal))
            .Select(address => new Diagnostic(DiagnosticCodes.EmailLinkNotSafe,
                $"The link to \"{Shown(address)}\" does not start with https://, http://, mailto: or tel:, or has a condition inside its address.",
                "Start the address with one of those, written out in the template. A value can fill in the rest of it. "
                + "To send people to different places, put a condition round each whole link.", line.Location));
    }

    private static IEnumerable<string> Flatten(IEnumerable<TextNode> nodes) => nodes.SelectMany(node => node switch
    {
        Literal literal => [literal.Text],
        IfNode condition => Flatten(condition.Nodes).Prepend(ConditionEdge.ToString()).Append(ConditionEdge.ToString()),
        _ => [Value.ToString()],
    });

    private static string Shown(string address) => address
        .Replace(Value.ToString(), "((...))", StringComparison.Ordinal).Replace(ConditionEdge.ToString(), "", StringComparison.Ordinal);

    private static Diagnostic NoSubject(string message, DiagnosticLocation location) => new(DiagnosticCodes.EmailSubjectMissing,
        message, "Give the email a subject of one line. In a single file, make the first line \"Subject: \" followed by it.", location);

    [GeneratedRegex(@"\]\(([^)]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex LinkAddress();
}

/// <summary>The result of compiling an email template: one that can be rendered, or what is wrong with it.</summary>
public sealed class EmailCompilation
{
    internal EmailCompilation(CompiledEmail? email, IReadOnlyList<Diagnostic> diagnostics)
    {
        Email = email;
        Diagnostics = diagnostics;
    }

    /// <summary>The compiled template, or null when any diagnostic is an error.</summary>
    public CompiledEmail? Email { get; }

    /// <summary>Everything found. A template that compiled may still carry warnings for its author to look at.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Succeeded => Email is not null;
}

/// <summary>An email template that has passed every check. It can be rendered any number of times, from any thread.</summary>
public sealed class CompiledEmail
{
    private readonly TextTemplate _subject;
    private readonly TextTemplate _body;
    private readonly FieldDictionary _dictionary;

    internal CompiledEmail(TextTemplate subject, TextTemplate body, FieldDictionary dictionary)
    {
        _subject = subject;
        _body = body;
        _dictionary = dictionary;
    }

    /// <summary>The email for one recipient, or what is wrong with their data.</summary>
    public EmailRender Render(JsonElement data, RenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var recipient = new RecipientData(data, settings, _dictionary);
        var subjectLines = _subject.Render(recipient);
        var bodyLines = _body.Render(recipient);
        if (recipient.Diagnostics.Count > 0)
        {
            return new EmailRender(null, null, null, recipient.Diagnostics);
        }

        // A subject is a header. Anything that is not printable becomes a space, so data cannot add a line to it.
        var subject = string.Concat(subjectLines.SelectMany(line => line.Pieces).Select(piece => piece.Text)
            .SelectMany(text => text.Select(character => char.IsControl(character) ? ' ' : character))).Trim();
        var (body, text) = EmailBody.Write(bodyLines);
        var html = $"<!DOCTYPE html><html lang=\"{InlineWriter.Escape(settings.Culture.Name)}\"><head><meta charset=\"utf-8\">"
                   + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
                   + $"<title>{InlineWriter.Escape(subject)}</title></head><body style=\"margin:0;padding:0;\">"
                   + $"<div style=\"max-width:580px;margin:0 auto;padding:20px;\">{body}</div></body></html>";
        return new EmailRender(subject, html, text, []);
    }
}

/// <summary>A rendered email: its subject, its body as HTML, and the same body as plain text for clients that want it.</summary>
public sealed class EmailRender
{
    internal EmailRender(string? subject, string? html, string? text, IReadOnlyList<Diagnostic> diagnostics)
    {
        Subject = subject;
        Html = html;
        Text = text;
        Diagnostics = diagnostics;
    }

    /// <summary>The subject line, or null when there are diagnostics.</summary>
    public string? Subject { get; }

    /// <summary>The body as a whole HTML document, or null when there are diagnostics.</summary>
    public string? Html { get; }

    /// <summary>The body as plain text, or null when there are diagnostics.</summary>
    public string? Text { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}
