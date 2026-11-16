using System.Text.Json;
using Heliograph.Documents;

namespace Heliograph.Messages;

/// <summary>The SMS part of a correspondence: plain text, with tags from the dictionary.</summary>
public static class SmsTemplate
{
    private const string Part = "sms";

    /// <summary>Checks the text against the dictionary. A template that compiles can be rendered.</summary>
    public static SmsCompilation Compile(string text, FieldDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(dictionary);

        var diagnostics = new List<Diagnostic>();
        var template = TextTemplate.Parse(text, Part, dictionary, diagnostics);
        if (diagnostics.Errors() > 0)
        {
            return new SmsCompilation(null, diagnostics);
        }

        // Found once, at the first place: an author who pasted a curly quote should hear before the bill arrives.
        foreach (var (line, wording) in template.Wording)
        {
            if (SmsLength.FirstOutsideBasicSet(wording) is { } character)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.SmsNeedsUnicode,
                    $"The character {character} is outside the basic set for text messages. Every message from this template will "
                    + "hold 70 characters, not 160, so most will be charged as more than one.",
                    "Replace it with a plain character, such as a straight quote or a hyphen, unless you need it.",
                    new DiagnosticLocation(Part, line, wording.Length <= 80 ? wording : wording[..80] + "..."), DiagnosticSeverity.Warning));
                break;
            }
        }

        return new SmsCompilation(new CompiledSms(template, dictionary), diagnostics);
    }
}

/// <summary>The result of compiling an SMS template: one that can be rendered, or what is wrong with it.</summary>
public sealed class SmsCompilation
{
    internal SmsCompilation(CompiledSms? sms, IReadOnlyList<Diagnostic> diagnostics)
    {
        Sms = sms;
        Diagnostics = diagnostics;
    }

    /// <summary>The compiled template, or null when any diagnostic is an error.</summary>
    public CompiledSms? Sms { get; }

    /// <summary>Everything found. A template that compiled may still carry warnings for its author to look at.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Succeeded => Sms is not null;
}

/// <summary>An SMS template that has passed every check. It can be rendered any number of times, from any thread.</summary>
public sealed class CompiledSms
{
    private readonly TextTemplate _template;
    private readonly FieldDictionary _dictionary;

    internal CompiledSms(TextTemplate template, FieldDictionary dictionary)
    {
        _template = template;
        _dictionary = dictionary;
    }

    /// <summary>The message for one recipient, or what is wrong with their data.</summary>
    public SmsRender Render(JsonElement data, RenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var recipient = new RecipientData(data, settings, _dictionary);
        var lines = _template.Render(recipient);
        if (recipient.Diagnostics.Count > 0)
        {
            return new SmsRender(null, default, 0, recipient.Diagnostics);
        }

        var text = string.Join('\n', lines.Select(line => string.Concat(line.Pieces.Select(piece => piece.Text))));
        var (encoding, segments) = SmsLength.Measure(text);
        return new SmsRender(text, encoding, segments, []);
    }
}

/// <summary>A rendered text message, with how it will be carried and how many messages it will be charged as.</summary>
public sealed class SmsRender
{
    internal SmsRender(string? text, SmsEncoding encoding, int segments, IReadOnlyList<Diagnostic> diagnostics)
    {
        Text = text;
        Encoding = encoding;
        Segments = segments;
        Diagnostics = diagnostics;
    }

    /// <summary>The message, or null when there are diagnostics.</summary>
    public string? Text { get; }

    public SmsEncoding Encoding { get; }

    /// <summary>How many messages the text will be sent, and charged, as.</summary>
    public int Segments { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}
