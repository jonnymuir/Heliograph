# Design brief

Draft. The maintainer owns every decision listed under [Open decisions](#open-decisions); agents
recommend, with the trade-off, and wait. [architecture.md](architecture.md) draws the design.

## The need

**Who it is for**

- A content designer who writes what an organisation sends to people: an acknowledgement, a
  reminder, a decision letter, an annual statement. They work in the word processor they already
  have.
- A service designer who decides when something is sent. In Wayfinder that is a moment at a
  stage or gateway in a service blueprint.
- A developer who has to add correspondence to an existing system without adopting a whole
  platform.
- An AI agent doing any of those three jobs on someone's behalf.
- The recipient, who needs a message that is accessible, plain and correct on whatever they read
  it with.

**The problem**

Professional correspondence is hard to change safely. Templates live in a developer's code or in
a vendor's proprietary designer, so a content designer cannot fix a sentence without help. Fields
are magic strings that fail silently at run time, when a real person gets a letter with a hole in
it. The letter, the email and the text about the same event are built separately, take different
data and are checked by nobody. And nobody can say exactly what a person was sent.

**What Heliograph does about it**

A piece of correspondence is one named thing with a part for each channel it is sent on: a
document for post and PDF, a body for email, a text for SMS. Every part draws on the same
dictionary of allowed data, is checked the same way before it can be used, and is rendered by the
same call. Authors use their own tools. Delivery stays with the host.

## Principles

1. **Standards first.** Templates and outputs are open, standard formats that existing tools
   already read and write. Heliograph invents a format only where no standard exists.
2. **Extension by design.** The engine names the seams (template format, output conversion,
   storage, the source of the dictionary) and ships one good implementation of each. No single
   product, vendor or library is assumed by the core.
3. **Any editor, unmodified.** A template can be written from start to finish in a stock editor:
   Word on the desktop, Word in Microsoft 365, LibreOffice, anything that saves the standard
   format. Helpers such as an add-in may make it nicer. Nothing may ever require one.
4. **Design time matters as much as run time.** Finding out what is wrong is the author's
   experience, not the recipient's. A template is compiled before it can be used, and every
   problem is reported in plain words, at the place it occurs, with what to do about it.
5. **Nothing to remember.** An author never types a field name from memory. A dictionary says
   what a template may use, and tooling offers it, checks against it and suggests the nearest
   match when something is wrong.
6. **AI native.** Whatever helps a person helps an agent, through the same operations with the
   same answers: read the dictionary, compile, preview, render. There is no agent-only or
   human-only path.
7. **Complex requirements, simple use.** Real letters carry tables of financial data, conditional
   paragraphs and letterheads that change by department. The complexity is absorbed by the
   dictionary and the engine, not by the author. The document carries the layout, the dictionary
   carries the data, and logic stays out of the template.

## The dictionary

The dictionary is the contract between the people who know the data and the people who write the
words. It is a closed list of everything a template is allowed to refer to:

- **Values**, each with a plain name, a description, a type (text, date, money, number, yes or
  no, address) and sample values. The type decides the formatting, so an author never writes a
  date or currency format.
- **Lists**, such as the rows of a statement, with their columns.
- **Conditions**, yes or no answers a template can show or hide content on.
- **Shared parts**, such as a letterhead, a footer or a signature block, that a template can
  include by name and that can be swapped by a value.

It is what makes the rest possible: the tooling offers what is in it, the compiler rejects what
is not, previews use its samples, the render call checks real data against it, and Wayfinder
checks a blueprint's bindings against it at design time. Where a host already knows its data (a
Wayfinder blueprint's fields), the dictionary is generated from that source, not typed again.

## Constraints

- **Three things an author can ask for, and no more:** put a value here, show this only when a
  condition holds, repeat this for each item in a list. Calculations, comparisons and formatting
  rules live upstream, in the system that supplies the data.
- **Layout uses the editor's own features.** Tables, headers, footers, styles, page breaks and
  repeated header rows are whatever the word processor already does. Heliograph adds no layout
  language.
- **Only compiled templates render.** A template that does not compile cannot be published, and
  the render call accepts nothing else.
- **A template is data, never code.** No scripts, no macros, no expressions a developer has to
  deploy.
- **Uploaded files are untrusted.** Macros, embedded objects, links to remote content and
  oversized archives are refused, each with a test.
- **Heliograph renders and never sends.** No provider is called and nothing leaves the process
  except through a seam the host supplies.
- **Rendering is deterministic** for the document, the email and the text: same template, same
  data, same output. A converted PDF is the exception, because converters stamp dates and
  identifiers into the file.

## Formats

| Part | The template is | The output is |
|---|---|---|
| Document (post, PDF, print) | A word-processor file in Office Open XML (`.docx`, ISO/IEC 29500) | The merged `.docx`, and a PDF through a converter the host chooses |
| Email | A subject and a body in CommonMark | Sanitised HTML in one accessible layout, with a plain-text alternative |
| SMS | Plain text | Plain text, with its length, encoding and number of segments |

Each is the first implementation behind a seam, not a limit. OpenDocument, a messaging app or
another PDF route can be added without changing the core.

## How we will know it worked

1. **The benchmark letters.** Three fictional letters of rising difficulty are kept in the repo
   as the acceptance test: an acknowledgement, a decision letter with conditional paragraphs, and
   an annual statement with a multi-page table of money, totals, and a letterhead and footer that
   change by department. A content designer builds each in a stock editor with no developer.
2. **Editor round trip.** Each benchmark letter still compiles and renders the same after an
   editor has saved it. LibreOffice is checked on every change. Word in Microsoft 365 is checked
   through files a person has saved there. Word on the desktop is expected to work and is
   unproven until someone with Word supplies a saved file.
3. **No silent failure.** Every way a template can be wrong (unknown field, misspelt field,
   unclosed section, a list used as a value, a missing shared part) has a diagnostic with a
   location and a suggested fix, and a test that proves it.
4. **Agent parity.** An agent given only the published docs and the MCP server writes a template
   that compiles, measured by an evaluation that runs when the guidance changes.
5. **One call, every part.** Wayfinder.Umbraco sends one correspondence as letter, email and
   text from a single render call, with no wording and no field names in host code.
6. **Standalone.** A second host with no Wayfinder and no Umbraco renders the benchmark letters
   by referencing only the published packages.
7. **Injection.** Merged data cannot add markup, fields or content to any part, proved by a test
   per format.
8. **Accessibility.** The compiler refuses a document without real headings, image descriptions
   and a language, and the email layout passes an automated accessibility check.

## Scope

| First release | Later | Never (the host's job) |
|---|---|---|
| Document, email and SMS parts | Messaging apps such as WhatsApp | Sending, retries, delivery receipts |
| The dictionary, the compiler and its diagnostics | An add-in that offers the dictionary inside the editor | Provider adapters |
| Upload, check, preview and publish, for people and for agents | Opening a template for editing straight from the browser | Scheduling and queues |
| Shared parts | OpenDocument templates | Recipient preferences, consent and contact details |
| PDF through one converter | Bilingual correspondence | Storing what was sent |

## Prior art to learn from

- [GOV.UK Notify](https://www.notifications.service.gov.uk/) templates: a plain formatting
  language, placeholders and optional content that content designers manage themselves.
- Mail merge, which word processors have had for decades: the idea that a document is the
  template is already familiar to the people Heliograph is for.
- Wayfinder's own validation, which returns coded, located, plain-language problems to people and
  agents alike.

## Decided

- **Scope:** Heliograph designs and renders. Delivery belongs to the host.
- **Who authors:** content designers and service designers, at design time, in their own tools.
- **Shape:** the Wayfinder model. An engine that runs on its own with a reference host, and
  [Heliograph.Umbraco](https://github.com/jonnymuir/Heliograph.Umbraco) to host it in Umbraco.
  Wayfinder starts a render at run time by calling Heliograph as a support system; a batch job
  can do the same.
- **One correspondence, a part per channel.** Parts share a dictionary, a version and a render
  call. They do not share a body: a good email is not derived from a letter.
- **Heliograph builds no editor.** Authors use existing word processors. Heliograph supplies the
  dictionary, the compiler and the preview around them.
- **LibreOffice is the first PDF converter and the reference editor for automated tests.** It
  sits behind the conversion seam like any other route. Its tagged PDF output is checked in
  [the plan](plan.md), step 1.
- **A field is marked with a text tag**: the plain dictionary name in double brackets, such as
  `((Member name))`, matched without regard to capitals or extra spaces. It is the only way of
  marking that an author can add in every editor and that survived a save in both LibreOffice
  and Microsoft 365. A tag an editor has stored in several pieces is joined before it is read.
- **A condition has a start and an end tag**: `((if Owes money))` and `((end if))`.
- **A table row repeats without a marker.** A row that uses values from a list repeats once for
  each item, because the dictionary already says those values belong to the list. The compiler
  reports which rows it will repeat and the preview shows them.
- **Content controls are not used.** A LibreOffice save drops repeating sections and shrinks a
  condition to its first few words. **Merge fields are not used in the first release**: they
  survive both editors, but Word in Microsoft 365 cannot insert one. The reader for markers sits
  behind a seam, so they could be recognised later.
- **A generated sheet lists every tag in the dictionary, ready to copy**, and the compiler names
  the nearest match for one that is misspelt. That is how an author avoids typing from memory in
  an unmodified editor.
- **The principles and constraints above.**

## Open decisions

1. **Where diagnostics appear.** A list in the workbench is the minimum. Returning the author's
   own document with a comment at each problem would put them where the work is.
2. **The dictionary's home and reach.** One per correspondence, one per service, or shared; who
   maintains it when it is not generated; and its file format.
3. **Shared parts in detail**: how a letterhead or footer is swapped by a value, and whether a
   shared part is itself a document.
4. **The Wayfinder join**: reference-app code only, or a small adapter package; and one
   support-system capability per correspondence, or one capability that takes it as an input.
5. **Versioning**: how a correspondence changes while journeys that will use it are running, and
   how a host records which version a person was sent.
