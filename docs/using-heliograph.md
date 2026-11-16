# Using Heliograph

For a developer, or an agent writing code for one, putting Heliograph into a system. It is a .NET
library with no package dependencies. It turns a template and one person's data into a finished
document. It does not send anything: delivery is your system's job.

It is not on nuget.org yet. Until it is, reference the project from a checkout of this repo.

## The three things you supply

| | What | Who writes it |
|---|---|---|
| **Dictionary** | The closed list of fields a template may use | Whoever knows the data. Generate it where you can |
| **Template** | A `.docx` with tags in it | A content designer. See [writing-a-template.md](writing-a-template.md) |
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
They are written for that person. Every code is explained in [diagnostics.md](diagnostics.md).

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
