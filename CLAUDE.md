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

Engineering standards (security, simplicity, testing, change flow, docs, writing style) are in
[jonnymuir/engineering-standards](https://github.com/jonnymuir/engineering-standards/blob/main/STANDARDS.md).
Follow them; this file adds only what is specific to this repo.

## Projects

| Project | Purpose |
|---|---|
| `src/Heliograph` | The core library. Empty until the first design decisions land. |
| `tests/Heliograph.Tests` | Behavioural tests through the public API (xUnit, AwesomeAssertions). |

## Build and test

```bash
dotnet build Heliograph.slnx
dotnet test Heliograph.slnx
node scripts/check-policies.mjs
node scripts/check-doc-links.mjs
```

## Releasing

Not yet released. The version lives in `src/Heliograph/Heliograph.csproj`. A publish workflow
using NuGet Trusted Publishing is added with the first release; until then `/release` has nothing
to publish.

## Rules specific to this repo

The principles and constraints are in [docs/brief.md](docs/brief.md). These are the ones that
decide day-to-day code.

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
