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
- **Word and Microsoft 365 cannot be driven from a script here.** They are covered by fixtures: a
  person saves each benchmark letter once in Word on the desktop and once in Microsoft 365, the
  files are committed under a name that says which editor saved them, and the same tests run
  over them. When a fixture is replaced, the PR says which editor version saved it.

## Steps

### 1. Settle the decisions that block code

By trying, not by argument. Output: the choices recorded in the brief, and the benchmark letters
in the repo.

1. Write the three benchmark letters as content: the words, the dictionary each one needs, and
   sample data. Fictional people and organisations only.
2. Spike each way of marking a field (tags in the text, merge fields, content controls) on the
   statement letter, the hardest one. For each: can it be inserted without typing from memory,
   does it survive a LibreOffice save, a Word save and a Microsoft 365 save, and can it express a
   repeat and a condition. The spike is thrown away.
3. Decide: field marking, how the dictionary reaches an author, where diagnostics appear, and the
   dictionary's format.
4. Check that LibreOffice produces a tagged, accessible PDF from the statement letter.

### 2. The core, one letter at a time

Each letter is a vertical slice: dictionary, compile with diagnostics, render, tests first.

1. Acknowledgement: values only. Brings the dictionary, the compiler, the refusal of unsafe
   files, and the round-trip and image tests.
2. Decision letter: conditions.
3. Annual statement: lists, money, and a letterhead and footer swapped by a value.

### 3. Email and SMS parts

One correspondence, three parts, one render call.

### 4. PDF

The LibreOffice converter as its own package, behind the conversion seam.

### 5. API, MCP, reference app and workbench

The same operations for people and agents, the standalone host with its demo outbox, and the
evaluation that checks an agent can write a template that compiles. The DAST and performance
workflows return here, because there is now an app to scan and load.

### 6. First release

`0.1.0` to nuget.org through Trusted Publishing.

### 7. Heliograph.Umbraco

Storage and uSync, the backoffice screens, and the reference site where a Wayfinder blueprint
supplies the dictionary and a stage sends a letter by calling Heliograph as a support system.
Then its own first release.

## Where we are

Step 1.1 is next.
