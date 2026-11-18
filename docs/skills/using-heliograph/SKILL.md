---
name: using-heliograph
description: How to call Heliograph from code. Covers the dictionary, compiling and rendering each part or a whole correspondence, stored correspondence with drafts and published versions, PDF, diagnostics, what the library guarantees and what the host must do. Use when writing or reviewing code that hosts Heliograph.
---

# Using Heliograph

For a developer, or an agent writing code for one, putting Heliograph into a system. It is a .NET
library with no package dependencies. It turns a template and one person's data into a finished
document. It does not send anything: delivery is your system's job.

It is not on nuget.org yet. Until it is, reference the project from a checkout of this repo.

## The three things you supply

| | What | Who writes it |
|---|---|---|
| **Dictionary** | The closed list of fields a template may use | Whoever knows the data. Generate it where you can |
| **Template** | A `.docx` with tags in it | A content designer. See [Writing a template](../writing-a-template/SKILL.md) |
| **Data** | One JSON object for each recipient, keyed by the dictionary's keys | Your system, at run time |

## The dictionary

A JSON file. Each field has the name an author writes in a tag, the key your data carries it
under, and a type.

```json
{
  "fields": [
    { "name": "Applicant name", "key": "applicantName", "type": "text" },
    { "name": "Event date", "key": "eventDate", "type": "date" },
    { "name": "Licence fee", "key": "licenceFee", "type": "money" },
    { "name": "Dangerous props", "key": "dangerousProps", "type": "yesNo" }
  ]
}
```

| Type | JSON value | Notes |
|---|---|---|
| `text` | a string | A line break stays a line break. A blank line starts a new paragraph |
| `date` | a string, ISO date `2027-06-19` | Printed in words in the render's language |
| `number` | a number | Printed with thousands separated |
| `money` | a number | Printed to two decimal places with no currency sign. The author writes the sign |
| `address` | an array of strings | One line each |
| `yesNo` | `true` or `false` | Never printed. Templates show or hide content on it. A string such as `"yes"` is refused |
| `list` | an array of objects | Never printed itself. It has `fields` of its own, and a table row that uses them is repeated for each item |

A list's fields are declared inside it, and each item in the data is an object with those keys:

```json
{ "name": "Contributions", "key": "contributions", "type": "list", "fields": [
    { "name": "Date", "key": "date", "type": "date" },
    { "name": "Amount", "key": "amount", "type": "money" } ] }
```

```json
{ "contributions": [ { "date": "2026-01-02", "amount": 12.5 }, { "date": "2026-02-02", "amount": 12.5 } ] }
```

Names are unique across the whole dictionary, lists included, so that an author never has to say
which "Date" they mean. Keys only need to be unique where the data holds them. A list cannot hold
another list. A problem with one item names it: "no value for "Amount" in item 2 of
"Contributions"".

A template can only ask for a value, show content on a yes or no field, or repeat a table row
over a list. It cannot calculate or compare. Work out totals, and anything a
letter should show "when not", in your system, and supply them as fields.

## A whole correspondence

A correspondence is one thing with a part for each channel: a document, an email and a text
message. Supply the parts you use. `Correspondence.Compile` compiles them together, and one
`Render` produces them all. This is the call to use unless you only ever need one part.

- A mistake in any part stops the whole correspondence. Every part's problems come back
  together, each saying which part it is in.
- When a recipient's data is wrong for any part, no part is produced, so nobody gets the text
  without the letter it refers to. A value missing from several parts is reported once.

The full example, which a test compiles and runs, is in the
[README](https://github.com/jonnymuir/Heliograph#in-code). The sections below describe each part
on its own.

## Compile, then render: the document part

```csharp
var dictionary = FieldDictionary.Parse(File.ReadAllText(dictionaryPath));

using var template = File.OpenRead(templatePath);
var compilation = DocumentTemplate.Compile(template, dictionary);
```

`Compile` reads the file, refuses anything unsafe, finds every tag, and rehearses the template
for every outcome of its conditions, with lists that are empty, of one item and of several. It returns:

- `Document`: a `CompiledDocument`, or null when any diagnostic is an error.
- `Diagnostics`: everything found. A template that compiled may still carry warnings.

Compile once and keep the `CompiledDocument`. It is immutable and safe to render from many
threads at once.

```csharp
var render = document.Render(recipient, new RenderSettings(CultureInfo.GetCultureInfo("en-GB")));
```

`Render` returns `Docx`, the finished file as bytes, or null with `Diagnostics` saying what is
wrong with the data. Only fields the template uses for this recipient are checked.

### Letting authors keep dictionaries

When your `IDictionarySource` is also an `IDictionaryStore`, authors keep dictionaries from the
workbench's Dictionaries screen, over the API (`PUT /dictionaries/{name}`, samples under
`/dictionaries/{name}/samples/{sample}`) and over MCP. A save is checked first (`HG3004`,
`HG3008`, `HG3010`), and the answer lists every correspondence that uses the dictionary with what
is now wrong with its draft. `FolderDictionarySource` and Heliograph.Umbraco's database source
are both stores. A source that is not shows dictionaries read-only and refuses changes (`HG3009`).

To offer ready-made dictionaries for "New dictionary", register an `IDictionaryStartingPoints`.
Heliograph does not know where they come from: the Heliograph.Umbraco reference site builds one
from each Wayfinder blueprint's fields.

## Stored correspondence

`Correspondence.Compile` works on files you hand it. `CorrespondenceLibrary` is for a system that
keeps correspondence for authors: it is what the API, the MCP server and Heliograph.Umbraco are
built on, so a person and an agent get the same operations.

```csharp
var library = new CorrespondenceLibrary(store, dictionaries, TimeProvider.System);

var save = await library.SaveDraftAsync("decision-letter", content, expectedRevision: 0);
var publish = await library.PublishAsync("decision-letter");          // refused if it does not compile
var render = await library.RenderAsync("decision-letter", recipient, settings);
// render.Version is the version this person was sent. Record it with what you send.
```

- **An author works on a draft.** A draft is saved even when it does not compile, with its
  diagnostics, so unfinished work is kept. `CheckAsync` compiles it again and `PreviewAsync`
  renders it with sample data.
- **Publishing makes a numbered version that never changes.** It keeps the dictionary as it was
  that day, so a later change to the dictionary cannot change what the version renders.
- **Only a published version is rendered for a recipient.** `RenderAsync` takes the newest, or
  the one you name, and says which it used.
- **A dictionary has a name and is shared.** Each correspondence names the one it uses.
  `CheckDictionaryAsync` compiles every draft that uses a dictionary, so whoever changes it sees
  what breaks before anything is published.
- **A save says the revision it started from.** If someone else saved first, nothing is saved
  and `HG3006` says so.

You supply two things. `ICorrespondenceStore` keeps drafts and versions: files, a database, a
document library. `IDictionarySource` supplies each dictionary as JSON, and its sample recipients
for previews, from wherever the data is known. Both only keep and return; the rules are in the
library. Put your own authorisation in front of every one of these operations.

For a small host, both come in the box and keep everything as ordinary files:

```csharp
var library = new CorrespondenceLibrary(
    new FolderCorrespondenceStore("data/correspondence"),   // <key>/draft/document.docx, email.md, sms.txt
    new FolderDictionarySource("data/dictionaries"),        // <name>.json, <name>.samples/<sample>.json
    TimeProvider.System);
```

`FolderCorrespondenceStore` is safe for many requests in one process. A site with several
servers keeps correspondence in a database instead.

Two operations make the document easier for an author:

- `StarterDocumentAsync(key)` gives a `.docx` to begin from: the title set, real headings, and a
  tag sheet listing every field in the dictionary. A template that still has the tag sheet is
  refused (`HG1021`).
- `AnnotatedDocumentAsync(key)` gives the draft's document back with a Word comment at each
  problem. Comments whose author is "Heliograph" are taken out when a document is compiled, so
  the author uploads it again without deleting them.

### Opening the document in a word processor

`MapCorrespondenceAuthoringApi` also maps the WebDAV routes Word on the desktop and LibreOffice use
to open a document from an address and save it back, under `/heliograph/webdav`. The workbench asks
`POST /correspondence/{key}/document/edit-link` for an address and opens it with `ms-word:` or
`vnd.libreoffice.command:`.

- **The address is the key.** A word processor cannot send your site's sign-in, so the WebDAV
  routes are open to anyone holding a link. Each link names one draft document, expires after
  eight hours, and is signed with ASP.NET Core Data Protection, so it cannot be made up or
  changed. Only a signed-in author is given one. Treat it like a password.
- **Share your Data Protection keys** across servers, as you already must for sign-in cookies, or
  a link made on one server will not open on another.
- **Use HTTPS** outside development. Word and LibreOffice must trust the certificate.
- A save is the next revision of the draft. Locks are granted but not held: two saves at once are
  caught by the revision, as everywhere else.

## PDF

The core library makes no PDF. It defines `IPdfConverter`, and a host chooses a route:

| Package | How | Use it when |
|---|---|---|
| `Heliograph.Pdf` | .NET alone, with built-in fonts; nothing to install. About 5 ms and a few MB a letter | You host anywhere .NET runs, an App Service included. New: what it lays out so far is in [docs/pdf-engine.md](../../pdf-engine.md) |
| `Heliograph.Pdf.LibreOffice` | Runs a LibreOffice installed on the same machine, with no window. About a second and 230 MB a letter | LibreOffice is installed, and a template uses something `Heliograph.Pdf` does not lay out yet |

```csharp
IPdfConverter converter = new HeliographPdfConverter();      // or new LibreOfficePdfConverter()
byte[] pdf = await converter.ConvertAsync(render.Docx!);
```

`Heliograph.Pdf`:

- **Fonts:** it draws the fonts a letter names, directly or through its theme, in built-in
  look-alikes with the same widths:
  - Arial in Liberation Sans;
  - Times New Roman in Liberation Serif;
  - Courier New in Liberation Mono;
  - Calibri in Carlito;
  - Cambria in Caladea.

  Any other font, Aptos (Word's default since 2024) included, is drawn in Liberation Sans for now,
  and is a warning at compile (`HG4013`).
- **Deterministic:** the same letter always gives the same bytes.
- **Limits:** a letter past `HeliographPdfOptions.MaxPages` (500) is refused, and a cancelled
  token stops the conversion.
- **Checked against LibreOffice:** its benchmark letters are laid out as LibreOffice lays them
  out, line for line, to within a point and a half, checked in CI.
- **It refuses what it cannot lay out, at compile.** `HeliographPdfConverter` is also an
  `IDocumentCheck`. Registered as the host's `IPdfConverter`, as `AddCorrespondenceLibrary`
  reads it, it makes every check, preview and publish report what a template has that it does
  not lay out yet: floating pictures, links in headers and small capitals, for example (`HG4001` to `HG4014`). So no
  PDF quietly loses anything. A version already published goes on rendering as it was. Without
  the library, pass it yourself:
  `DocumentTemplate.Compile(docx, dictionary, [new HeliographPdfConverter()])`.
- **Your own check** implements `IDocumentCheck`. `DocumentToCheck` gives it each part's XML, the
  settings, styles and list definitions, and `DocumentPart.Linked(id)` reads a file a part links
  to, such as a picture's.

Both routes:

- It asks for PDF/UA, the standard for accessible PDF: a tagged file with headings, lists and
  table structure that says its language. The benchmark letters pass
  [veraPDF](https://verapdf.org/)'s PDF/UA check in CI.
- **A PDF is only accessible if the template has a title** in its document properties. Compile
  warns when it is missing (`HG1020`) and stops nothing.
- A failure is a `PdfConversionException`. Its message never quotes the letter.

`Heliograph.Pdf.LibreOffice` gives each conversion a folder and a LibreOffice profile of its own,
removed afterwards, so conversions can run side by side. Starting LibreOffice takes a second or
more each time, and its PDF is not the same bytes on every run: it stamps a date and an
identifier.

## Emails

```csharp
var compilation = EmailTemplate.Compile(File.ReadAllText(emailPath), dictionary);   // first line "Subject: ..."
var compilation = EmailTemplate.Compile(subject, body, dictionary);                 // or the two apart
var render = compilation.Email!.Render(recipient, settings);
```

`Render` returns `Subject`, `Html` and `Text`. `Html` is a whole document with inline styles, in
the render's language. `Text` is the same body as plain text. Send both, as the two alternatives
of one message.

- The subject is always one line. Control characters from the data become spaces, so data cannot
  add a header.
- Everything is escaped. The only markup is what the template's author wrote.
- A value inside a link's address is percent-encoded, so it can fill in part of an address and
  never change where the link goes.

## Text messages

The SMS part is a string, compiled and rendered the same way.

```csharp
var compilation = SmsTemplate.Compile(File.ReadAllText(smsPath), dictionary);
var render = compilation.Sms!.Render(recipient, settings);
```

`Render` returns `Text`, with `Encoding` (`Gsm7` or `Ucs2`) and `Segments`: how many messages the
text will be sent, and charged, as. Heliograph does not shorten or split a message. Sending it is
your provider's job.

## Diagnostics

Every problem has a stable `Code`, a `Message` and a `Fix` in plain words, a `Severity` (`Error`
or `Warning`), and, for a problem in a template, a `Location`: the part, the paragraph number in
it, and the start of the paragraph's text. Show all of them to whoever uploaded the template.
They are written for that person. Every code is explained in [Diagnostics](../diagnostics/SKILL.md).

| Codes | When | Meaning |
|---|---|---|
| `HG0xxx` | Compile | The file is refused whole: unsafe, unreadable, or carrying something that would leak |
| `HG1xxx` | Compile | Something in the template is wrong, or (warnings) may be |
| `HG2xxx` | Render | One recipient's data is missing a value or has one of the wrong type |

Only `HG2xxx` can appear at render. Everything about the template is settled at compile.

## What you can rely on

- **The same template and data give the same bytes.** A render reads no clock, no machine
  setting and no network. The language and region for dates and numbers come from the
  `RenderSettings` you pass.
- **Data stays data.** A value is written as text. It cannot add a tag, markup or formatting,
  whatever it contains.
- **A rendered file is a valid Word document** for every recipient, or the template did not
  compile.
- **The names of the people who edited a template are not copied into letters.** Comments and
  tracked changes are refused at compile, not stripped.

## What you must do

- **Treat an uploaded template as untrusted** until it has compiled. Compile refuses macros,
  embedded objects, content loaded from outside the file, and files larger than 64 MB.
- **Never log data or rendered output.** They hold personal data. Diagnostics are safe to log:
  they quote the template, never the data.
- **Keep the record of what was sent.** Heliograph does not store anything.
- **Put authorisation in front of compile and render** in your own system. The library has no
  notion of a user.

## For an agent working in a consumer's repo

Everything above applies. In short:

1. Read the dictionary before writing or changing a template. Use field names from it exactly.
2. Compile after every change, and read every diagnostic. Each says where and how to fix it.
3. Do not put arithmetic, comparisons or formats into a template. Ask for a field instead.
4. Do not work round a refusal by editing the file's XML. Fix what the diagnostic names.
5. Never put real people's data into a template, a test or a log.
