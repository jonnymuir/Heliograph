# Heliograph.Pdf.LibreOffice

Converts a letter rendered by [Heliograph](https://github.com/jonnymuir/Heliograph) to PDF, using
a LibreOffice installed on the same machine.

```csharp
IPdfConverter converter = new LibreOfficePdfConverter();
byte[] pdf = await converter.ConvertAsync(rendered.Docx!);
```

- **Accessible by default.** It asks LibreOffice for PDF/UA: a tagged file, with headings, lists
  and table structure a screen reader can follow, that says its language.
- **Nothing is left behind.** Each conversion has a working folder and a LibreOffice profile of
  its own, removed afterwards, so conversions can run side by side.
- **Nothing leaves the machine.** LibreOffice runs locally with no window.

It needs LibreOffice installed. `LibreOfficeOptions.ExecutablePath` says where, if it is not in a
usual place. A PDF, unlike the `.docx`, is not byte-for-byte the same on every run: converters
stamp a date and an identifier into the file.

This is one route to PDF. Heliograph's core defines `IPdfConverter` and assumes none.
