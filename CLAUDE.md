# Heliograph: Project Guide

## What this is

Heliograph lets content designers write outgoing correspondence in the word processor they
already use, and renders it for each channel it is sent on: a document for post and PDF, an email
and a text. A correspondence has a part per channel. Every part draws on one dictionary of
allowed data, is compiled before it can be used, and is rendered by one call. It is a .NET
library with no framework dependency, so it can stand alone or sit inside another system. Its
first consumer is [Wayfinder](https://github.com/jonnymuir/Wayfinder) and
[Wayfinder.Umbraco](https://github.com/jonnymuir/Wayfinder.Umbraco).

It deliberately does **not** deliver anything, and it does **not** include a word processor.
Sending, retries, delivery receipts, provider adapters, scheduling and recipient preferences
belong to the host. A document is written in Word, Microsoft 365, LibreOffice or anything else
that saves the standard format. Heliograph does have a UI, the workbench, and the email and the
text message are written in it, because they are plain text. Getting a document from the author's
word processor into Heliograph and back must be as simple as it can be made.

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
| `src/Heliograph` | The core library: `Correspondence` (one compile and one render for every part), the field dictionary, diagnostics, and `Documents/`, which compiles a `.docx` template (values, conditions and rows that repeat over a list) and renders it. No package dependencies. |
| `src/Heliograph/Library` | Part of the core library: `CorrespondenceLibrary`, the operations on stored correspondence (draft, check, preview, publish, render a version), over two seams the host supplies, `ICorrespondenceStore` and `IDictionarySource`. |
| `src/Heliograph/Messages` | Part of the core library: plain-text templates. The SMS part, the email part, and the line-by-line engine they share. |
| `src/Heliograph.Mcp` | The authoring operations as MCP tools over HTTP, three prompts, and every guide in `docs/skills` as a resource. One `MapCorrespondenceAuthoringMcp()` call, closed unless the caller is signed in. The only project that references ASP.NET Core. |
| `src/Heliograph.Pdf.LibreOffice` | One route to PDF behind `IPdfConverter`: runs a local LibreOffice with no window and asks for PDF/UA. References the core only. |
| `tests/Heliograph.Mcp.Tests` | The MCP server through a real MCP client against an in-memory host: what an agent is offered, told and refused. |
| `tests/Heliograph.Tests` | Behavioural tests through the public API (xUnit, AwesomeAssertions). The Open XML SDK is referenced here only, as an independent validator of rendered files. |
| `benchmarks/` | Three fictional letters with their dictionaries and sample data: the acceptance test. Not a project. |

## Build and test

```bash
dotnet build Heliograph.slnx
dotnet test Heliograph.slnx
node scripts/check-policies.mjs
node scripts/check-doc-links.mjs
```

`RandomisedTests` builds templates at random and compares each rendered letter with what the
generator says it should be. A failure names a seed; `new RandomTemplate(seed)` builds that
document again. When the engine gains something an author can write, teach the generator to write
it, or the net has a hole there.

The editor round-trip tests drive LibreOffice with no window. They find it in its usual install
place, or at the path in `HELIOGRAPH_SOFFICE`. Without LibreOffice they are skipped, unless
`HELIOGRAPH_REQUIRE_EDITOR=1`, which CI sets so they can never be skipped there. The PDF tests
also run veraPDF in Docker, and are skipped the same way where Docker is not running. Never run the
Python that ships inside LibreOffice on macOS: the system kills it and shows a crash report.

## Releasing

Every package under `src/` is released together at one version, which each `.csproj` carries.
A tag `v<version>` runs `.github/workflows/package-release.yml`: it checks the tag against every
project, runs the tests with a real LibreOffice, and publishes to nuget.org through Trusted
Publishing. A version with a hyphen is a prerelease.

Until `0.1.0`, prereleases (`0.1.0-alpha.N`) go out as the engine grows, so that
Heliograph.Umbraco can restore from nuget.org. To publish one: bump the version in every project
in a PR, merge it, then tag the merge commit. Nothing stable has been released.

## Rules specific to this repo

The principles and constraints are in [docs/brief.md](docs/brief.md). These are the ones that
decide day-to-day code.

- **Use the two words exactly.** A field is a thing in the dictionary that a template can use. A
  tag is how an author writes one, `((Member name))`. Code, diagnostics and docs keep to that.
- **A new diagnostic code gets a row in [docs/skills/diagnostics](docs/skills/diagnostics/SKILL.md).** A test fails
  until it has one.
- **A change an author or a developer would notice changes their guide in the same PR.**
  [docs/skills/writing-a-template](docs/skills/writing-a-template/SKILL.md) is for authors and
  [docs/skills/using-heliograph](docs/skills/using-heliograph/SKILL.md) for developers. Both list what is not built
  yet: take a line out when you build it. The code in the README is compiled and run from
  `tests/Heliograph.Tests/Examples/Quickstart.cs`, and a test fails if the two differ.
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
- **A guide is a skill.** Guides for people who use Heliograph live in `docs/skills/<name>/SKILL.md`,
  with a `name` and a `description` at the top. People read them, agents load them as skills, and
  `Heliograph.Mcp` serves them as resources: one file. Never write a second copy for agents. See
  [docs/skills/README.md](docs/skills/README.md).
- **Person and agent parity.** An operation offered on a screen is offered over the API and MCP,
  and returns the same diagnostics. Don't add a capability to one and not the others.
- **A template is data, not code.** No scripts, macros or expressions. An author can insert a
  value, show content on a condition and repeat content over a list; anything more belongs
  upstream, in the data.
- **Uploaded files are untrusted.** Macros, embedded objects, remote links and oversized archives
  are refused before anything else reads the file, each with a test.
- **Nothing that ships carries someone else's program.** LibreOffice and veraPDF are run as
  separate programs and never referenced, bundled or copied from. A package a shipped project
  references gets a row in [docs/third-party.md](docs/third-party.md) with its licence; a test
  fails until it has one. Nothing under the GPL or AGPL is referenced by a package that ships.
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
