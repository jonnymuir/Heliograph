# PDF without LibreOffice: an experiment

Not shipped, not in the solution, not tested in CI. It asks one question: could Heliograph make
its PDFs itself, so it runs anywhere (an Azure App Service included) with no external program?

Two routes were tried against today's route, `Heliograph.Pdf.LibreOffice`:

- **Own engine** (`Heliograph.PdfExperiment/OwnEngine`, about 1,300 lines):
  - a reader for the Word document;
  - TrueType reading and subsetting;
  - line and page breaking, tables, headers and footers;
  - a tagged PDF writer.
- **MigraDoc**: the same Word reader, laid out and written by MigraDoc and PDFsharp 6.2.4.

## How it was judged

- **Letters:** the six benchmark letters the PDF tests use, plus the acknowledgement as saved by
  Microsoft 365.
- **Accessibility:** veraPDF 1.30.4, the same pinned image the tests use, as PDF/UA-1.
- **Look:** pages rasterised at 100 dpi with macOS PDFKit. The figure is the share of
  LibreOffice's ink that differs from each route's at 30% fuzz. A shift of one pixel counts as a
  difference, so small numbers mean near-identical.
- **Text:** the text extracted from each PDF is compared word for word with LibreOffice's.
- **Speed and memory:** median of 20 runs (3 for LibreOffice) on an M-series Mac, plus 16 letters
  converted at once. LibreOffice's memory comes from `/usr/bin/time -l` on one conversion.

## Results

| | LibreOffice | Own engine | MigraDoc 6.2.4 |
|---|---|---|---|
| PDF/UA-1 (veraPDF) | 5 of 6 (the untitled Word 365 letter has no `dc:title`) | 6 of 6 | 0 of 6: no tagging at all |
| Looks like LibreOffice | (reference) | 0–3% of ink differs on letters; 9–12% on the 3-page statement (one-pixel shifts) | not measured further |
| Same text when copied | (reference) | identical on all 6 | not measured |
| Same bytes twice | no (not needed of it) | yes | no |
| Time per letter | 800–840 ms | 3–6 ms | 1–7 ms |
| 16 letters at once | 6.0 s | 13 ms | 25 ms |
| Memory | about 230 MB per conversion (separate process) | 3–8 MB allocated | 2–8 MB allocated |
| Runs on Windows App Service | no | yes (pure .NET) | yes |
| Licence | MPL, separate program | ours | MIT only up to 6.2.4; 6.5.0 and later are AGPL-3.0 or paid, with a licence check at run time |

The own engine's 6 of 6 includes the untitled letter only because it falls back to the title
"Letter". That is a choice, not an advantage: HG1020 already warns the author.

## What the own engine does not do yet

Each of these is either a feature to build or a document to refuse at compile with a
diagnostic. Which is which is the maintainer's decision.

- **Fonts:** every font is drawn as Liberation Sans (the four faces); a host would need to supply
  font files.
- **Text styling:** colour (all text is black), shading, highlight, superscript and subscript,
  strike, character spacing, kerning and hyphenation.
- **Pictures and objects:** pictures, text boxes, shapes and anything floating with text wrapped
  round it.
- **Document parts:** footnotes, hyperlinks (and their tagging as links), more than one section,
  columns, and even-page headers.
- **Tables:** vertically merged cells, a row too tall for a page, nested tables, and widths taken
  from the contents (only fixed grids are read).
- **Fields:** any field other than PAGE and NUMPAGES.
- **Scripts:** right-to-left and complex scripts.

## What the numbers do not prove

- **The letters are simple.** Six letters, all written in LibreOffice or saved by Microsoft 365
  from simple content. The randomised template generator would be the real test: thousands of
  documents, each compared with LibreOffice on the same measures.
- **The reference is LibreOffice, not Word.** Word lays out slightly differently from both.
- **The figures come from one Mac.** Timings on a small App Service plan will be slower, but the
  ratio should hold.

## Running it

```bash
dotnet run --project experiments/Heliograph.PdfExperiment -- <output folder>
```

It reads Liberation Sans from LibreOffice's fonts folder on a Mac, or from `HELIOGRAPH_FONTS`.
The fonts are licensed separately from LibreOffice (SIL OFL).
