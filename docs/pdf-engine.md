# Heliograph's own PDF engine

A plan for making PDFs without LibreOffice: a pure .NET converter that turns a rendered letter
into a tagged, accessible PDF, so Heliograph runs anywhere .NET runs, an Azure App Service
included, with nothing else installed.

It follows [an experiment](https://github.com/jonnymuir/Heliograph/tree/experiment/pdf-routes/experiments).
About 1,300 lines laid out the six benchmark letters with these results:

- **Accessibility:** every one passed veraPDF's PDF/UA-1 check.
- **Look:** within one pixel of LibreOffice on most lines.
- **Text:** copied out word for word the same as LibreOffice's.
- **Speed and memory:** about 4 ms and a few MB per letter, against 820 ms and 230 MB through
  LibreOffice.

The open question is whether that holds for documents the experiment was not written for.
This plan is built to find out early and keep finding out.

## What it is

- **A package, `Heliograph.Pdf`**, behind the `IPdfConverter` seam that `Heliograph.Pdf.LibreOffice`
  already sits behind. It references the core only, takes no package dependencies, and starts no
  process.
- **It reads the rendered `.docx`.** The document stays the contract between the parts. An author
  can open exactly what was converted, and the LibreOffice route stays a drop-in alternative.
- **It knows its limits at compile time.** A template that uses something the engine cannot lay
  out is refused when it is compiled, with the place and a fix, for hosts that use this route.
  No PDF is ever quietly wrong. This is the rule that already governs tag placement.
- **It is deterministic.** The same letter gives the same bytes, which removes the one exception
  to "rendering is deterministic".
- **Heliograph.Umbraco takes it as it takes every engine package.** It becomes the default there
  and in both reference apps after stage 2, the measuring net, with LibreOffice one line away.

## Where it is

`Heliograph.Pdf` exists, at stage 1. It lays out:

- **Paragraphs:**
  - styles and their inheritance;
  - bold, italic, underline and size;
  - left, centred, right and justified text;
  - indents, tab stops, line breaks;
  - the three line-spacing modes, and space before and after.
- **Pages:** widow and orphan control, keep with next, keep together, page breaks.
- **Lists:** bullets and numbers.
- **Tables:** fixed column grids, cell padding and borders, column spans, and heading rows repeated
  on each page.
- **Headers and footers:** on every page, with a first page of its own, and page numbers and page
  counts.

All six benchmark letters pass PDF/UA, give the same bytes every time, and are laid out as
LibreOffice lays them out to within a point and a half on every line.

What it does not lay out yet is refused when a template is compiled (`HG4001` to `HG4013`; see
the [diagnostics guide](skills/diagnostics/SKILL.md)). The converter is an `IDocumentCheck`, and
registering it as the host's `IPdfConverter` is enough. So stage 1 is done; stage 2, the
measuring net, is next.

## How we will know it is right

Every check runs in CI, and an agent can run all of them without a person.

| Check | What it proves | How |
|---|---|---|
| Accessible | Each PDF is PDF/UA-1 | veraPDF in Docker, as today, on every benchmark letter and every generated document |
| Same words | Copying out and screen readers get the right text in the right order | Text extracted from our PDF equals text extracted from LibreOffice's for the same letter |
| Same layout | Lines break, pages break and things sit where LibreOffice puts them | Compare the position of every line in the two PDFs: same words on each line, same page, baseline within a tolerance. A pixel comparison of page images backs it up |
| Many documents | It works beyond the letters it was written for | The randomised template generator writes documents with every feature the engine claims; each goes through both routes and all the checks above. A failure names a seed |
| Word | It matches what authors see | The maintainer exports a PDF from Microsoft 365 for each benchmark letter once. It is committed and compared by the same layout measure, and reviewed by eye when it differs |
| Same bytes | Deterministic | Convert twice, compare |
| Fast and small | Fit for a small server | A budget test: time and memory per letter, and 50 letters at once |
| Robust | A strange document fails clearly and never hangs | Malformed and hostile documents, and generated ones pushed to extremes: each finishes within a time limit or is refused with a reason |

LibreOffice and the page rasteriser (Poppler) run only inside the tests, as separate programs,
as veraPDF does. Nothing that ships carries them.

## Order of work

Each stage ends with its checks green in CI and a PR merged. Each one also shortens the list of
things the compile-time check refuses.

### 1. Foundation

- Move the experiment into `src/Heliograph.Pdf`, split to the size budgets:
  - `Reading` (the Word document);
  - `Fonts`;
  - `Text` (line breaking and tabs);
  - `Layout` (paragraphs, tables, pages, headers and footers);
  - `Structure` (the tagging tree);
  - `Writing` (the PDF).
- Wire up every check in the table except Word, run over the benchmark letters.
- Add the compile-time check (`HG4xxx` codes, each with a row in the diagnostics guide). From day
  one it refuses what is not built: pictures, text boxes, footnotes, more than one section, and
  so on.
- Add guards: a page limit, a time limit honoured through cancellation, and no reading of any
  file except fonts.

*Done when* all six benchmark letters pass every check.

### 2. The measuring net

Teach the randomised generator the features stage 1 supports, so the tests run hundreds of
generated documents through both routes. Add the Word fixtures.

*Done when* the net runs in CI and its failures are triaged into fixes or limits.

### 3. Fonts

- **A font source.** The package carries free look-alike fonts (SIL OFL); a host can add its own
  files, the real Arial included if it holds a licence.
- **A mapping table** that Word and LibreOffice already agree on:
  - Arial → Liberation Sans;
  - Times New Roman → Liberation Serif;
  - Courier New → Liberation Mono;
  - Calibri → Carlito;
  - Cambria → Caladea.
- **Fallback per character.** A character the chosen font lacks is drawn from the next font
  that has it. A character no font has is a compile error.
- **More font files:** TrueType collections and OpenType with CFF outlines (subset, or embedded
  whole).

### 4. Text

- Colour, highlight and shading.
- Underline styles and strikethrough.
- Superscript and subscript, small capitals and capitals.
- Character spacing.
- Kerning, matching what LibreOffice and Word do by default.
- Non-breaking spaces and hyphens, and soft hyphens.
- Right, centre and decimal tabs with leaders. Statements line figures up with these.
- Hyperlinks, as link annotations tagged as links.

### 5. Paragraphs and pages

- Numbering in full: several levels, restarts, letters, Roman numerals and legal styles.
- Paragraph borders and shading, and contextual spacing.
- Every keep and break rule, including space before at the top of a page.
- Line spacing modes, checked against Word fixtures.

### 6. Tables

- Widths taken from contents (Word's autofit), percentage widths and centred or right-aligned
  tables.
- Vertically merged cells, and vertical alignment in a cell.
- Cell shading, and Word's rules for which border wins.
- Rows that split across pages, and nested tables.

### 7. Pictures

- PNG (transparency kept) and JPEG.
- Inline pictures first, then floating ones with square or top-and-bottom wrapping.
- Each picture is tagged as a figure with its alternative text. A picture without alternative
  text is a compile warning, which helps every route, not only this one.

### 8. Sections, headers and footers, notes

- Several sections with their own page size, orientation and margins.
- Even-page headers.
- Page-number formats, and section page counts.
- Footnotes and endnotes, tagged as notes.
- Columns.

### 9. Switch over (after stage 2, as decided)

- Make it the default in both reference apps and in Heliograph.Umbraco, with LibreOffice still
  one line away. Stages 3 to 8 then follow as normal releases.
- Update the developers' guide.
- Remove the converted-PDF exception from the determinism rule.

### Later, or refused for good

Each item stays refused at compile until it is built. Some may stay refused for good:

- text boxes, shapes, SmartArt, charts and equations;
- wrapping other than square or top-and-bottom;
- right-to-left and complex scripts;
- PDF/A alongside PDF/UA, for archives.

## Risks

- **The long tail of "it looks slightly different".** The net and the Word fixtures make it
  visible. The compile-time check keeps it from ever producing a wrong PDF silently.
- **Word is not LibreOffice.** The automated reference is LibreOffice, because it runs in CI.
  Word fixtures are the check on the reference.
- **Fonts and licences.** Only freely licensed fonts are built in. A host that wants Arial itself
  supplies the file and holds its licence.
- **Size.** Probably 15,000 to 30,000 lines in the end, held to the same size budgets and
  code-health rules as everything else.
