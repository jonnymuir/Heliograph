# Heliograph

Write outgoing correspondence in the word processor you already use. Heliograph checks it and
fills it in for each person you send it to.

A heliograph sent messages by flashing sunlight across long distances.

- **Authors use their own tools.** A template is an ordinary Word document, written in Word,
  Microsoft 365 or LibreOffice with nothing installed. Where the letter changes from person to
  person, the author writes a tag: `((Applicant name))`.
- **A dictionary says what a template may use**, so nobody types a field name from memory and
  hopes.
- **Mistakes are found when a template is uploaded, not when a letter goes out.** Every problem
  comes back at once, in plain words, with where it is and how to fix it.
- **It renders and never sends.** Your system, or a host such as
  [Wayfinder](https://github.com/jonnymuir/Wayfinder), delivers what Heliograph produces.
- **Whatever a person can do, an AI agent can do the same way**, and gets the same answers.

## What works today

| | |
|---|---|
| Word document templates (`.docx`) | Yes: values, conditions around paragraphs and inside sentences, in the body, tables, headers, footers and notes |
| Lists that repeat a table row | Yes |
| One call that compiles and renders the letter, the email and the text together | Yes |
| A letterhead or footer shared between templates | Not yet |
| SMS part | Yes: the same tags and conditions, with the encoding and the number of messages it will be charged as |
| Email part | Yes: a subject and a body in a small set of Markdown, rendered as accessible HTML and as plain text |
| An MCP server for AI agents | Yes: `Heliograph.Mcp` offers the authoring operations, and serves the guides people read as the agent's skills |
| Stored correspondence, with drafts and numbered versions | Yes, over a store the host supplies |
| PDF output | Yes: an accessible, tagged PDF through `Heliograph.Pdf.LibreOffice`, checked against PDF/UA by an independent validator |
| A package on nuget.org | Not yet. Reference the project from a checkout |

## A template

```text
Dear ((Applicant name)),

((if Licence approved))
Your licence for ((Event name)) on ((Event date)) is approved. Pay £((Licence fee)) by ((Fee due date)).
((end if))
```

[Writing a template](https://github.com/jonnymuir/Heliograph/blob/main/docs/skills/writing-a-template/SKILL.md)
is the guide for authors.

## In code

```csharp
var dictionary = FieldDictionary.Parse(File.ReadAllText(Path.Combine(folder, "dictionary.json")));

// A correspondence has a part for each channel it is sent on. Leave out any you do not use.
using var document = File.OpenRead(Path.Combine(folder, "document.docx"));
var compilation = Correspondence.Compile(new CorrespondenceSource
{
    Document = document,
    Email = File.ReadAllText(Path.Combine(folder, "email.md")),
    Sms = File.ReadAllText(Path.Combine(folder, "sms.txt")),
}, dictionary);

// Compile once. Every problem in every part comes back together, with where it is and how to fix it.
foreach (var problem in compilation.Diagnostics)
{
    Console.WriteLine($"{problem.Severity} {problem.Code}: {problem.Message} {problem.Fix}");
}

if (compilation.Correspondence is not { } correspondence)
{
    return;
}

// Render as often as you like, from any thread: once for each recipient.
var recipient = JsonDocument.Parse(File.ReadAllText(recipientPath)).RootElement;
var rendered = correspondence.Render(recipient, new RenderSettings(CultureInfo.GetCultureInfo("en-GB")));
if (rendered.Diagnostics.Count > 0)
{
    return; // The data was wrong, so nothing is produced. The diagnostics say what to supply.
}

File.WriteAllBytes(Path.Combine(output, "letter.docx"), rendered.Docx!);
File.WriteAllText(Path.Combine(output, "email.html"), rendered.Email!.Html);
File.WriteAllText(Path.Combine(output, "sms.txt"), rendered.Sms!.Text);
```

[Using Heliograph](https://github.com/jonnymuir/Heliograph/blob/main/docs/skills/using-heliograph/SKILL.md)
is the guide for developers, with the dictionary's format and what the library guarantees.

## More

| | |
|---|---|
| [Diagnostics](https://github.com/jonnymuir/Heliograph/blob/main/docs/skills/diagnostics/SKILL.md) | Every problem Heliograph reports, by code |
| [Edge cases](https://github.com/jonnymuir/Heliograph/blob/main/docs/edge-cases.md) | Every way a template can go wrong, and what happens |
| [Benchmark letters](https://github.com/jonnymuir/Heliograph/tree/main/benchmarks) | Three real-looking letters with their templates, dictionaries and data |
| [Design brief](https://github.com/jonnymuir/Heliograph/blob/main/docs/brief.md) and [plan](https://github.com/jonnymuir/Heliograph/blob/main/docs/plan.md) | Why it is built this way, and what comes next |
| [AGENTS.md](https://github.com/jonnymuir/Heliograph/blob/main/AGENTS.md) | Where an AI agent should start |

## Licence

[MIT](https://github.com/jonnymuir/Heliograph/blob/main/LICENSE)
