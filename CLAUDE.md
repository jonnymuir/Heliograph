# Heliograph: Project Guide

## What this is

Heliograph lets content designers write outgoing correspondence in the word processor they
already use, and renders it for each channel it is sent on: a document for post and PDF, an email
and a text. A correspondence has a part per channel. Every part draws on one dictionary of
allowed data, is compiled before it can be used, and is rendered by one call. It is a .NET
library with no framework dependency, so it can stand alone or sit inside another system. Its
first consumer is [Wayfinder](https://github.com/jonnymuir/Wayfinder) and
[Wayfinder.Umbraco](https://github.com/jonnymuir/Wayfinder.Umbraco).

It deliberately does **not** deliver anything, and it does **not** include an editor. Sending,
retries, delivery receipts, provider adapters, scheduling and recipient preferences belong to the
host. Writing happens in Word, Microsoft 365, LibreOffice or anything else that saves the
standard format.

It follows the Wayfinder model: an engine, a reference host that proves it standalone, and
[Heliograph.Umbraco](https://github.com/jonnymuir/Heliograph.Umbraco) to host it in Umbraco.

The design is still being set. [docs/brief.md](docs/brief.md) holds the need, the principles,
the constraints, the measures of success and the open decisions;
[docs/architecture.md](docs/architecture.md) draws the system context and the planned packages.
Read both before proposing structure, and don't settle an open decision without the maintainer.
[docs/plan.md](docs/plan.md) gives the order of work, how each step is tested, and where we are.

Engineering standards (security, simplicity, testing, change flow, docs, writing style) are in
[jonnymuir/engineering-standards](https://github.com/jonnymuir/engineering-standards/blob/main/STANDARDS.md).
Follow them; this file adds only what is specific to this repo.

## Projects

| Project | Purpose |
|---|---|
| `src/Heliograph` | The core library: the field dictionary, diagnostics, and `Documents/`, which compiles a `.docx` template (values and conditions) and renders it. No package dependencies. |
| `tests/Heliograph.Tests` | Behavioural tests through the public API (xUnit, AwesomeAssertions). |
| `benchmarks/` | Three fictional letters with their dictionaries and sample data: the acceptance test. Not a project. |

## Build and test

```bash
dotnet build Heliograph.slnx
dotnet test Heliograph.slnx
node scripts/check-policies.mjs
node scripts/check-doc-links.mjs
```

The editor round-trip tests drive LibreOffice with no window. They find it in its usual install
place, or at the path in `HELIOGRAPH_SOFFICE`. Without LibreOffice they are skipped, unless
`HELIOGRAPH_REQUIRE_EDITOR=1`, which CI sets so they can never be skipped there. Never run the
Python that ships inside LibreOffice on macOS: the system kills it and shows a crash report.

## Releasing

Not yet released. The version lives in `src/Heliograph/Heliograph.csproj`. A publish workflow
using NuGet Trusted Publishing is added with the first release; until then `/release` has nothing
to publish.

## Rules specific to this repo

The principles and constraints are in [docs/brief.md](docs/brief.md). These are the ones that
decide day-to-day code.

- **Use the two words exactly.** A field is a thing in the dictionary that a template can use. A
  tag is how an author writes one, `((Member name))`. Code, diagnostics and docs keep to that.
- **A new diagnostic code gets a row in [docs/diagnostics.md](docs/diagnostics.md).** A test fails
  until it has one.
- **A rendered file must always be a valid Word document.** Where a tag's placement would break a
  table, a list or a link, the template is refused at compile with the place and a fix. Nothing
  is guessed and nothing broken is produced. Each awkward placement found gets a test in
  `AwkwardPlacementTests`.
- **Catch it at compile, not at render.** Only a fault in one recipient's data may surface at
  render. [docs/edge-cases.md](docs/edge-cases.md) is the register of every case and the strategy
  that handles it. A case you find goes in the register in the same change, and its status moves
  only when a test proves it.
- **The core takes no framework dependency and assumes no product.** No ASP.NET Core, no Umbraco,
  no Wayfinder reference in `src/Heliograph`. A file format, a PDF route, a store and a dictionary
  source each sit behind a seam with one implementation; adding another must not change the core.
- **Nothing may require a modified editor.** A template is writable from start to finish in stock
  Word, Microsoft 365 and LibreOffice. A helper may make it nicer; no feature may depend on one.
- **Every way a template can be wrong has a diagnostic**: a stable code, the place in the file,
  plain words and a suggested fix, with a test. A failure that only shows at render time is a
  defect in the compiler.
- **Person and agent parity.** An operation offered on a screen is offered over the API and MCP,
  and returns the same diagnostics. Don't add a capability to one and not the others.
- **A template is data, not code.** No scripts, macros or expressions. An author can insert a
  value, show content on a condition and repeat content over a list; anything more belongs
  upstream, in the data.
- **Uploaded files are untrusted.** Macros, embedded objects, remote links and oversized archives
  are refused before anything else reads the file, each with a test.
- **Rendering never sends.** No network calls, no provider SDKs, no queues. If a feature needs to
  talk to the outside world, it belongs in the host.
- **Rendering is deterministic.** The same template and data produce the same output. No clock,
  randomness, culture or network read inside a render; the caller passes them in. A converted PDF
  is the one exception.
- **Merged data is escaped for the target format by default.** Correspondence carries data from
  outside (names, addresses, free text), so document fields, HTML in email and markup in messaging
  formats are injection seams. Raw output is an explicit, named opt-in with a test.
- **Correspondence contains personal data.** Never log merged data or rendered output. Test
  fixtures and the benchmark letters use obviously fictional people and addresses.
