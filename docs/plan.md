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
language and the PDF/UA marker; it has not been run through a PDF/UA validator, which belongs to
step 4.

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

The LibreOffice converter as its own package, behind the conversion seam. Decide here whether it
starts LibreOffice as a local process, calls it in a container, or supports both.

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

## Where we are

Steps 2.1 and 2.2 are done for the document part. The decision letter compiles and renders for
all three samples: whole sections shown or hidden on a yes or no field, one condition inside
another, money and numbers printed by type, and data for the other outcome left out. A condition
can also sit inside a sentence.

Known limits, each reported by a diagnostic or listed here until it is lifted:

- A whole table row or column cannot be shown or hidden by a condition, because a tag lives in a
  paragraph and a paragraph lives in a cell. A table, or the content of a cell, can.
- Supplied text that falls inside a link cannot start a new paragraph there, so a blank line in it
  prints as a blank line.

In step 2.1, the acknowledgement letter compiles against its
dictionary, renders for both samples, refuses unsafe files, and survives a LibreOffice save,
checked in CI. A copy saved by Word in Microsoft 365 compiles and renders too. Comparing page images automatically is not built yet; the rendered pages were
checked by eye. Step 2.3, the annual statement with lists and shared parts, is next.
