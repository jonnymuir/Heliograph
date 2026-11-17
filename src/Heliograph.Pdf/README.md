# Heliograph.Pdf

Converts a letter rendered by [Heliograph](https://github.com/jonnymuir/Heliograph) to an
accessible, tagged PDF (PDF/UA-1) in .NET alone. There is nothing to install and no program to
start, so it runs wherever .NET runs, an Azure App Service included.

```csharp
IPdfConverter pdf = new HeliographPdfConverter();
byte[] file = await pdf.ConvertAsync(render.Docx!);
```

- **Accessible:** every letter is tagged for screen readers (headings, paragraphs, lists,
  tables), with its language and title.
- **Deterministic:** the same letter always gives the same bytes.
- **Fonts:** the fonts a letter names are drawn in built-in look-alikes with the same widths.
  - Arial in Liberation Sans;
  - Times New Roman in Liberation Serif;
  - Courier New in Liberation Mono.

  These fonts are under the SIL Open Font License; see `LICENSE-Liberation.txt` in the package.
- **Limits:** a letter that runs past 500 pages (`HeliographPdfOptions.MaxPages`) is refused.

It is new. What it lays out so far is listed in
[docs/pdf-engine.md](https://github.com/jonnymuir/Heliograph/blob/main/docs/pdf-engine.md);
`Heliograph.Pdf.LibreOffice` is the other route, behind the same `IPdfConverter` seam.
