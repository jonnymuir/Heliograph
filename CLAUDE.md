# Heliograph: Project Guide

## What this is

Heliograph composes outgoing correspondence once and renders it for any channel: email, SMS,
messaging apps such as WhatsApp, PDF and print. It is a .NET library with no framework
dependency, so it can stand alone or sit inside another system. Its first consumer is
[Wayfinder](https://github.com/jonnymuir/Wayfinder) and
[Wayfinder.Umbraco](https://github.com/jonnymuir/Wayfinder.Umbraco).

It deliberately does **not** deliver anything. Sending, retries, delivery receipts, provider
adapters (Notify, Twilio, SES and so on), scheduling and recipient preferences belong to the host.
Heliograph's output is the rendered artefact plus the metadata a sender needs.

The design is still being set. [docs/brief.md](docs/brief.md) holds the need, the measures of
success and the open decisions. Read it before proposing structure, and don't settle an open
decision without the maintainer.

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

- **The core takes no framework dependency.** No ASP.NET Core, no Umbraco, no Wayfinder reference
  in `src/Heliograph`. Integrations go in their own projects or in the host repo, so the library
  stays usable on its own.
- **Rendering never sends.** No network calls, no provider SDKs, no queues. If a feature needs to
  talk to the outside world, it belongs in the host.
- **Rendering is deterministic.** The same definition and data produce the same output. No clock,
  randomness, culture or network read inside a render; the caller passes them in. This is what
  makes snapshot tests and "exactly what was sent" records possible.
- **Merged data is escaped for the target channel by default.** Correspondence carries data from
  outside (names, addresses, free text), so HTML in email and PDF, and markup in messaging
  formats, are injection seams. Raw output is an explicit, named opt-in with a test.
- **Correspondence contains personal data.** Never log merged data or rendered output. Test
  fixtures use obviously fictional people and addresses.
