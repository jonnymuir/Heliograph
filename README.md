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
| Lists that repeat a table row | Not yet |
| A letterhead or footer shared between templates | Not yet |
| Email and SMS parts | Not yet |
| PDF output | Not yet |
| A package on nuget.org | Not yet. Reference the project from a checkout |

## A template

```text
Dear ((Applicant name)),

((if Licence approved))
Your licence for ((Event name)) on ((Event date)) is approved. Pay £((Licence fee)) by ((Fee due date)).
((end if))
```

[Writing a template](https://github.com/jonnymuir/Heliograph/blob/main/docs/writing-a-template.md)
is the guide for authors.

## In code

```csharp
var dictionary = FieldDictionary.Parse(File.ReadAllText(dictionaryPath));

// Compile once. A template that does not compile comes back with every problem found.
using var template = File.OpenRead(templatePath);
var compilation = DocumentTemplate.Compile(template, dictionary);
foreach (var problem in compilation.Diagnostics)
{
    Console.WriteLine($"{problem.Severity} {problem.Code}: {problem.Message} {problem.Fix}");
}

if (compilation.Document is not { } document)
{
    return;
}

// Render as often as you like, from any thread: once for each recipient.
var recipient = JsonDocument.Parse(File.ReadAllText(recipientPath)).RootElement;
var render = document.Render(recipient, new RenderSettings(CultureInfo.GetCultureInfo("en-GB")));
if (render.Docx is { } letter)
{
    File.WriteAllBytes(letterPath, letter);
}
```

[Using Heliograph](https://github.com/jonnymuir/Heliograph/blob/main/docs/using-heliograph.md)
is the guide for developers, with the dictionary's format and what the library guarantees.

## More

| | |
|---|---|
| [Diagnostics](https://github.com/jonnymuir/Heliograph/blob/main/docs/diagnostics.md) | Every problem Heliograph reports, by code |
| [Edge cases](https://github.com/jonnymuir/Heliograph/blob/main/docs/edge-cases.md) | Every way a template can go wrong, and what happens |
| [Benchmark letters](https://github.com/jonnymuir/Heliograph/tree/main/benchmarks) | Three real-looking letters with their templates, dictionaries and data |
| [Design brief](https://github.com/jonnymuir/Heliograph/blob/main/docs/brief.md) and [plan](https://github.com/jonnymuir/Heliograph/blob/main/docs/plan.md) | Why it is built this way, and what comes next |
| [AGENTS.md](https://github.com/jonnymuir/Heliograph/blob/main/AGENTS.md) | Where an AI agent should start |

## Licence

[MIT](https://github.com/jonnymuir/Heliograph/blob/main/LICENSE)
