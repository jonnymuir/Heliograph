# Plan

The order of work for both repos, and how each step is proved. The [brief](brief.md) says what
Heliograph is for; the [architecture](architecture.md) says what gets built. A step is done when
its proof passes in CI, not when the code is written.

## How it is tested

Tests are driven from outside, at four levels, and an agent can run every one of them without a
person.

| Level | What it proves | How |
|---|---|---|
| Behaviour | Compile and render do what the brief says | xUnit through the public API, over real `.docx` files kept in the repo |
| Editor round trip | A template survives being opened, edited and saved by an editor | A script drives LibreOffice with no window: open, change, save, then compile and render again and compare |
| What the recipient sees | The rendered letter looks right, not just parses | Render, convert to PDF, turn pages into images, compare with approved images. An agent can open the images and look |
| Whole system | The parts work together in a host | Playwright against the reference app, through the workbench and the API |

Two things keep this honest:

- **LibreOffice is the reference editor.** It runs on a developer's machine and on the CI runner,
  so the round trip and the PDF are checked on every change.
- **Word cannot be driven from a script here.** Microsoft 365 is covered by fixtures: a person
  saves each benchmark letter once in Word in the browser, the file is committed under a name
  that says which editor saved it, and the same tests run over it. Word on the desktop gets the
  same treatment when someone with Word can supply the files; until then it is unproven.

## Steps

### 1. Settle the decisions that block code

Done. The [benchmark letters](../benchmarks/README.md) are written, and a spike put three ways of
marking a field through LibreOffice and Microsoft 365. The choices are recorded in the
[brief](brief.md#decided). LibreOffice's PDF export carries headings, table structure, the
language and the PDF/UA marker.

### 2. The core, one letter at a time

Each letter is a vertical slice: dictionary, compile with diagnostics, render, tests first.

1. Acknowledgement: values only. Brings the dictionary, the compiler, the refusal of unsafe
   files, and the round-trip and image tests.
2. Decision letter: conditions.
3. Annual statement: lists, money, and a letterhead and footer swapped by a value.

#### Before the annual statement

Close the cases the [edge-case register](edge-cases.md) marks as planned. Tags that are not
recognised, what must not reach a recipient, the independent checks in the tests and structure
are done (strategies S1 to S10). What the register still marks as planned needs a decision, or
arrives with lists. Lists multiply every case, so the net goes in first.

### 3. Email and SMS parts

One correspondence, three parts, one render call.

### 4. PDF

Done. `Heliograph.Pdf.LibreOffice` sits behind the conversion seam and starts LibreOffice as a
local process. Calling it in a container is another implementation of the same seam, not built.
Each benchmark letter's PDF passes veraPDF's PDF/UA-1 check in CI.

### 5. API, MCP, reference app and workbench

The same operations for people and agents, the standalone host with its demo outbox, and the
evaluation that checks an agent can write a template that compiles. A .NET Aspire host starts the
reference app, a local mailbox and the PDF converter with one command, as Wayfinder's does, and
the whole-system tests boot the same definition. The DAST and performance
workflows return here, because there is now an app to scan and load.

### 6. First release

`0.1.0` to nuget.org through Trusted Publishing.

### 7. Heliograph.Umbraco

Storage and uSync, the backoffice screens, and the reference site where a Wayfinder blueprint
supplies the dictionary and a stage sends a letter by calling Heliograph as a support system,
started together by an Aspire host. Then its own first release.

### 8. Heliograph's own PDF engine

A pure .NET converter, `Heliograph.Pdf`, so PDFs need nothing installed. Stages, checks, risks
and where it is are in [pdf-engine.md](pdf-engine.md). It is in stage 1: the benchmark letters
convert, accessible and laid out as LibreOffice lays them out, and stages 2 to 8 are well under
way, with Word's own PDFs as the judge where Word and LibreOffice differ.

## Where we are

Steps 1 to 4 are done, apart from shared parts. Step 5 is done apart from the evaluation of an
agent and the whole-system tests. Prereleases (`0.1.0-alpha.N`) go to nuget.org as the engine
grows, and Heliograph.Umbraco restores from there. Step 8 is under way, brought ahead of step 6.

Built:

- **The core.** Values, conditions (whole sections and inside a sentence), rows repeated over a
  list, money and numbers by type, every way a template can be wrong reported at compile with a
  code, the place and a fix, and the refusal of unsafe files. The three benchmark letters compile
  and render for every sample, and survive a save in LibreOffice and in Word in Microsoft 365.
- **The SMS and email parts**, with the same tags, conditions and lists, and one call that
  compiles and renders all three parts.
- **Stored correspondence.** `CorrespondenceLibrary` holds drafts and numbered versions over a
  store and a dictionary source the host supplies.
- **The REST API, the MCP server and the workbench**, offering the same operations to people and
  agents. The workbench keeps dictionaries, starts a document, and opens it in Word or
  LibreOffice to be saved straight back. The guides in `docs/skills` are served to agents as
  skills.
- **The reference app** with the benchmark letters seeded, an outbox page showing the run-time
  call, and an Aspire host.
- **Two routes to PDF**: LibreOffice, and Heliograph's own converter, which is the default. Every
  benchmark letter passes veraPDF's PDF/UA-1 check by both. A document with no title is pointed
  out with a warning (`HG1020`) and still compiles. Where the converter stands is in
  [pdf-engine.md](pdf-engine.md).

Still to do:

- **Shared parts**: a letterhead, footer or signature block swapped by a value. This waits on the
  maintainer's design decision. Until then the annual statement's letterhead and footer are
  written into the template.
- **The evaluation of an agent** writing a template that compiles (step 5).
- **The whole-system tests** with Playwright, and the DAST and performance workflows (step 5).
- **Comparing page images automatically.** The PDF converter is measured line by line against
  LibreOffice and Word, but the LibreOffice route's pages are still checked by eye.
- **The first release**, `0.1.0` (step 6).

Known limits, each reported by a diagnostic or listed here until it is lifted:

- A whole table row or column cannot be shown or hidden by a condition, because a tag lives in a
  paragraph and a paragraph lives in a cell. A table, or the content of a cell, can.
- Supplied text that falls inside a link cannot start a new paragraph there, so a blank line in it
  prints as a blank line.
