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
- **Heliograph.Umbraco takes it as it takes every engine package.** It is the default there and
  in the reference app, with LibreOffice one line away. The maintainer chose to switch during
  alpha rather than wait for stage 2 to finish.

## Where it is

`Heliograph.Pdf` exists, at stage 1. It lays out:

- **Paragraphs:**
  - styles and their inheritance;
  - bold, italic, underline and size;
  - colour, highlight and shading, single and double strikethrough, superscript and subscript,
    capitals, small capitals (lowercase letters as capitals at four fifths of the size, as
    LibreOffice draws them; a Word probe will say whether Word's are the same) and character
    spacing; hidden text is not drawn;
  - left, centred, right and justified text;
  - indents and line breaks;
  - tab stops aligned left, right, centred or on the decimal point, with leaders of dots,
    hyphens or a line;
  - links in the body to web pages, email addresses and telephone numbers, clickable and tagged
    as links (a link that wraps onto a second line is two links, each to the same address);
  - the three line-spacing modes, and space before and after;
  - borders and shading round paragraphs in the body, paragraphs with the same frame sharing one
    box, with a line between them if asked.
- **Pages:** widow and orphan control, keep with next, keep together, page breaks.
- **Lists:** as Word numbers them:
  - several levels, each in its own style: numbers, with or without a leading zero, letters and
    Roman numerals;
  - labels that show higher levels ("1.2."), legal numbering, restarts and set values, and lists
    that count on from another with the same definition;
  - labels aligned left, right or centred, followed by a tab, a space or nothing;
  - Word's bullets in Symbol, Wingdings and Courier New, drawn as the same shapes;
  - a list inside a list tagged as one, for screen readers.
- **Tables:** fixed column grids, cell padding and borders, column spans, and heading rows repeated
  on each page, and a table inside a table's cell, laid out across the cell's text as one block
  that keeps together, the empty paragraph Word requires after it at a cell's end taking no room
  (its cells merged down a column, or footnotes in it, are refused):
  - through their style (Word's Table Grid, say): borders, padding, indent and alignment, the
    style's outer borders on the edges and inside borders between cells, and the paragraph and
    run settings it gives text in its cells;
  - centred or to the right, rows at least or exactly a set height, and text at the top, middle
    or foot of a cell (at its top in a row broken across pages, as LibreOffice lays it out);
  - cells merged down a column, the merged cell drawn once down its rows and the last of them
    growing to fit its text, and cell shading;
  - a row breaks across pages, each cell at its own last line that fits, unless it is marked not
    to. A row with a set height that breaks across a page can still differ from LibreOffice, and
    rows joined by a merged cell are kept together on a page where LibreOffice may break them.
- **Headers and footers:** on every page, with a first page of its own, tables in them laid out
  as a table in a cell is (a letterhead set out as a table, say; until this was built they were
  left out without a word), and page numbers and counts
  of pages in the letter (`NUMPAGES`) and in its section (`SECTIONPAGES`), each in figures, letters
  or Roman numerals as its switch says, its instruction read whole though Word may split it across
  runs. LibreOffice is no guide to the counts: it prints a section's as last saved, and counts a
  blank page it would add before a page numbered 1 again; a Word probe is.
- **Sections** that start a new page, each with its own page size, orientation and margins, its
  headers and footers (each one it does not set carried on from the section before, as Word does),
  a first page of its own, different headers and footers on even pages (even and odd by the page's
  number, as Word and LibreOffice both count), and page numbers restarted where it says, in
  figures, letters or Roman numerals. A continuous section break goes on down the same page, with
  the new section's margins across it, as Word does it (LibreOffice keeps the page's first
  section's margins); one on a page of another size starts a new page. A section that starts on
  an odd or even page has a blank page put before it when the next page's number is the other,
  as Word decides it. As in Word's own PDF, the blank page has nothing on it, not even a header
  or footer, but it is counted in the page numbers. LibreOffice counts it too but leaves it out
  of its PDF. Columns before a continuous break are balanced. A border round the page, line
  numbers, text set in the middle of the page, a gutter, and margins mirrored on facing pages are
  refused.
- **Page breaks** typed in the text (Ctrl+Enter in Word), and a column break in a single column:
  the rest of the paragraph goes on at the top of the next page; a break that ends its paragraph
  keeps the paragraph mark with it, so what follows starts the next page, as Word and LibreOffice
  lay it out. Until this was built a page break was read as a line break, without a word. A page
  break in a table, header, footer or note is refused.
- **Beyond text in a run:** a position tab (as Word's own headers and footers use) lining text up
  on the left, the middle or the right of the margins or the indents, with its leader; a
  non-breaking hyphen, drawn as a hyphen that no line breaks at; an old-style page number; and a
  page number in the body, as the page its paragraph starts on. Until these were built each was
  left out without a word. LibreOffice takes a position tab for an ordinary one and leaves the
  old page number out, so a Word probe is the judge of those. A symbol from a symbol font, an
  optional hyphen, a phonetic guide and a date block are refused, and so is a count of pages in
  the body or a page field in a table or a note.
- **Content Word wraps in something else:** paragraphs in custom XML, and table rows and cells in
  content controls (as a repeating section wraps rows), all of which were left out before; and a
  row that starts further along the grid (gridBefore), whose cells were drawn from the first
  column. A paragraph in a frame or a drop capital, content imported from another file, text
  spread across the whole line, and a section break inside a content control are refused.
- **Underlines:** single, double, thick, and under the words only, each in the underline's own
  colour if it has one; before, every kind was drawn as one line in the text's colour. Dotted,
  dashed and wavy underlines, a border round text, and an equation are refused.
- **Columns** in a section, of equal width half an inch apart unless it says, or each with its
  own width and space, with a line between them if it asks: text flows down each column and on
  to the next page, and a paragraph that goes on in a column of another width is broken again
  there; a column break goes on in the next column and a page break on the next page. Before a
  continuous section break the columns of the section's last page are balanced, as Word and
  LibreOffice balance them: as short as they can be and still hold the text, with the next
  section going on below the deeper one. A section in columns can also start part way down a
  page, after a continuous break. Footnotes, endnotes or floating pictures in a letter set in columns are
  refused.
- **Footnotes** referred to from the body or a table: numbered 1, 2, 3 in the order they are referred to, at
  the foot of the page under the separator, each tagged as a note. As LibreOffice lays out a Word
  document, the separator takes one single line of the default style, whatever the document's
  own separator says, and the body stops above it; a line that refers to a note goes on a page
  only with its note. A note is not split across pages, where Word and LibreOffice may carry the
  rest of a long one over. A table row's footnotes take room on its page. A row whose text would
  fit but not the space after it goes to the next page whole, as Word lays it out (LibreOffice
  leaves a gap a row high instead). Notes numbered or placed some other way are still refused.
- **Endnotes**, numbered i, ii, iii in the order they are referred to, after the letter's text:
  one single line of the default style below the foot of its last line, with the separator drawn
  across it, as for footnotes, then the notes, carrying on to further pages as they need. Notes of
  both kinds are numbered as the document's settings, and its sections' over them, say: from where
  they say, in figures, letters or Roman numerals.
- **Pictures** set in line with the text, in the body, tables, headers and footers:
  - PNG of every colour type and bit depth, interlaced or not, with its transparency;
  - JPEG, grey, colour or CMYK, written into the PDF as it is;
  - each standing on the baseline, its line as tall as it needs; line spacing over single adds a
    share of the text's height, not the picture's;
  - or floating in front of or behind the text, with no text wrapped round it (a letterhead logo,
    say), anchored to the page, the margins or its paragraph, by an offset or aligned;
  - or, in the body, with text wrapped round its box: beside it (Square), on the side it says or
    the wider, or above and below it, with the space it keeps clear; a paragraph on that page is
    broken line by line, each as wide as the room at its height, and a table starts below any
    picture it would meet, as LibreOffice lays them out. Wrapped tight to its outline, in a header
    or footer, or with room for text on both sides, is refused;
  - a described picture is a figure with its description; a decorative one, one with no
    description, or one in a header or footer is passed over by a screen reader.

All six benchmark letters pass PDF/UA, give the same bytes every time, and are laid out as
LibreOffice lays them out to within a point and a half on every line.

What it does not lay out yet is refused when a template is compiled (`HG4001` to `HG4013`; see
the [diagnostics guide](skills/diagnostics/SKILL.md)). The converter is an `IDocumentCheck`, and
registering it as the host's `IPdfConverter` is enough. Stage 1 is done, and it is the reference
app's converter (stage 9, brought forward).

Stage 2, the measuring net, is under way. `RandomLayout` builds documents from everything the
converter claims, and `MeasuringNetTests` compares each one with LibreOffice line for line. The
net is a ratchet: it records the first line at which each of 30 seeds still differs, and fails if
a change makes any seed differ sooner. Its first rounds found, and fixed:

- the first page's text area must follow the first page's own header;
- a paragraph keeps its space before at the top of a page;
- a line may break after a hyphen between letters;
- a word too wide for a line is split where the line is full;
- an exact or at-least line carries its extra height above its text;
- a row at least a set height is that height below its top border, and a table's bottom border
  adds its width after the last row;
- a tab past the end of a line starts the next line and still takes effect;
- the default tab interval comes from the document's settings;
- table rows break across pages;
- a paragraph kept with the next is kept with its first two lines when widow and orphan control is on;
- a paragraph that would leave a lone first line at the foot of a page, once its last two lines
  are kept together, moves to the next page whole;
- a line is as tall as its tallest font reaches above the baseline plus its deepest below, plus
  the largest gap;
- a tab with a word straight after it that does not fit moves down with the word, and takes
  effect on the next line;
- a paragraph kept with the next is kept with all of it when the next keeps its lines together;
- a paragraph a keep rule moves to a new page starts at its top, without its space before;
- a font's descent is the one Windows gives, and its line gap goes above the text, as Word puts
  it (LibreOffice puts it below); in an at-least line the extra goes above too, and in an exact
  line the baseline is four fifths of the way down, as both place it. Word fixtures in Calibri,
  whose gap is large, settled this;
- a document with no page settings is laid out on US Letter, as Word does;
- superscript and subscript are drawn at 58% of the size, their top or foot meeting the full-size
  text's, and a line is as tall as it would be without them;
- a paragraph kept with the next counts its space after, which comes between them;
- Don't add space between paragraphs of the same style, as LibreOffice reads it: a paragraph
  that says so drops its space after before one of its style, and its space before when the one
  above says so too, or, where the larger spacing is kept, whenever it says so itself (with
  Word's List Paragraph the style says so, and every reading agrees);
- the extra space under a line set wider than single may hang below the foot of a page.

It also found something to refuse: a tab in a centred or right-aligned paragraph (`HG4011`).

Ten more seeds are documents made mostly of lists with levels, with a ratchet of their own.
LibreOffice on a Mac makes a line with a Symbol bullet a little taller than the text alone; on
Linux it does not. The converter does not, for now, and a Word fixture with bullets will settle
what Word does.

What the net still shows is mostly in four places: where a word split across lines breaks, a
few wrap points, some page breaks, and drift from rounding. LibreOffice rounds some line heights
to a twip in a way that differs by font, size and platform (a line of Times New Roman at 10pt is
11.55pt there, 11.5pt here). Chasing that would mean matching one build of LibreOffice, so it is
left until Word fixtures show what Word does.

Where Word and LibreOffice lay a document out differently, the converter follows Word, the maintainer's
decision, and the net's recorded numbers for the seeds affected are set again. So far: space between paragraphs of
one style (each paragraph that says so drops only its own space, and with the larger spacing the overlap is worked
out from the space after as it was); and where text sits in a line (a font's line gap above the text, not below, and
in an at-least line the extra above too). In an exact line both put the baseline four fifths of the way down. The net
compares a Calibri document with LibreOffice allowing for Calibri's gap, the one gap large enough to tell.

Word fixtures are made by `tools/Heliograph.WordFixtures` (see its README), and compared by
`WordConformityTests`. The first Word fixture is in (`tests/Heliograph.Tests/Fixtures/Word`): a document made from a
blank one in Word for the web, and the PDF Word exported from it. A new Word document leaves out
`doNotUseHTMLParagraphAutoSpacing`, which LibreOffice and the benchmark letters write. Without it,
Word puts the larger of a paragraph's space after and the next one's space before between them,
not their sum, and so does LibreOffice. The converter follows, and half the net's seeds are now
saved that way.

## How we will know it is right

Every check runs in CI, and an agent can run all of them without a person.

| Check | What it proves | How |
|---|---|---|
| Accessible | Each PDF is PDF/UA-1 | veraPDF in Docker, as today, on every benchmark letter and every generated document |
| Same words | Copying out and screen readers get the right text in the right order | Text extracted from our PDF equals text extracted from LibreOffice's for the same letter |
| Same layout | Lines break, pages break and things sit where LibreOffice puts them | Compare the position of every line in the two PDFs: same words on each line, same page, baseline within a tolerance. A pixel comparison of page images backs it up |
| Many documents | It works beyond the letters it was written for | The randomised template generator writes documents with every feature the engine claims; each goes through both routes and all the checks above. A failure names a seed |
| Word | It matches what authors see | `tools/Heliograph.WordFixtures` has Word for the web lay out a set of rendered letters (the benchmark letters, some of the net's documents, and a probe for each rule taken from LibreOffice where Word may differ) through Microsoft Graph and the maintainer's OneDrive. The PDFs are committed, and `WordConformityTests` compares the converter with each by the same layout measure, with a ratchet of its own |
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

- **A font source.** The package carries free look-alike fonts (SIL OFL and Apache 2.0); a host
  can add its own files, the real Arial included if it holds a licence (done: TrueType files in
  `HeliographPdfOptions.Fonts`, read by family, bold and italic from the font itself, refused when
  the font may not be embedded).
- **A mapping table** that Word and LibreOffice already agree on (done, with theme fonts resolved
  and the same files LibreOffice ships):
  - Arial → Liberation Sans;
  - Times New Roman → Liberation Serif;
  - Courier New → Liberation Mono;
  - Calibri → Carlito;
  - Cambria → Caladea.
- **Fallback per character.** A character the chosen font lacks is drawn from the next font
  that has it. A character no font has is a compile error. Done: the built-in fonts are tried in
  turn (Liberation Sans, Serif and Mono, Carlito, Caladea); one none has is refused in a template
  (`HG4015`) and, in a recipient's data, fails that letter's conversion without quoting it.
- **More font files:** TrueType collections and OpenType with CFF outlines (subset, or embedded
  whole).

### 4. Text

- Colour, highlight and shading. Done (shading behind a whole paragraph is stage 5).
- Underline styles and strikethrough. Strikethrough done.
- Superscript and subscript, small capitals and capitals. Done.
- Character spacing. Done.
- Kerning, matching what LibreOffice and Word do by default.
- Non-breaking spaces and hyphens, and soft hyphens.
- Right, centre and decimal tabs with leaders. Statements line figures up with these. Done (a
  bar tab, and tabs in a centred or right-aligned paragraph, are still refused).
- Hyperlinks, as link annotations tagged as links. Done for links in the body to web pages, email
  and telephone; links to a place in the document, and in headers and footers, are still refused.

### 5. Paragraphs and pages

- Numbering: done early (several levels, restarts, letters, Roman numerals, legal styles). Still
  to come: picture bullets and the rarer number styles ("1st", "one", other scripts).
- Paragraph borders and shading, and contextual spacing. Done in the body (contextual spacing
  everywhere); in a table cell, header or footer, still refused.
- Every keep and break rule, including space before at the top of a page.
- Line spacing modes, checked against Word fixtures.

### 6. Tables

- Widths taken from contents (Word's autofit), percentage widths, and centred or right-aligned
  tables (done).
- Vertically merged cells (done, kept together on a page), and vertical alignment in a cell (done). Table styles' own borders,
  padding and paragraph settings (done); their formatting of some rows or columns, still refused.
- Cell shading (done), and Word's rules for which border wins.
- Rows that split across pages, and nested tables (both done; a table inside another is laid out
  as one block in its cell, which does not break across pages).

### 7. Pictures

- PNG (transparency kept) and JPEG. Done.
- Inline pictures first (done), then floating ones: in front of or behind the text (done), then
  with square or top-and-bottom wrapping (done, in the body, text on one side).
- Each picture is tagged as a figure with its alternative text. A picture without alternative
  text is a compile warning, which helps every route, not only this one.

### 8. Sections, headers and footers, notes

- Several sections with their own page size, orientation and margins (done, starting a new page,
  the next odd or even one, or carrying on down the same one).
- Even-page headers (done).
- Page-number formats, and section page counts (both done, with the format switches Word offers).
- Footnotes, tagged as notes (done, in the body); endnotes (done).
- Columns (done, of equal width or each of its own; balancing columns above a continuous break
  is still refused).

### 9. Switch over (done early: the maintainer chose to switch while still in alpha)

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
