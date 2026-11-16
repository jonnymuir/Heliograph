# Spike: how a field is marked in a document

Throwaway. It lives on the `spike/field-marking` branch and is never merged; what it finds goes
into the brief.

```sh
python3 build.py out/original                      # the statement letter, marked three ways
soffice --headless --convert-to 'docx:MS Word 2007 XML' --outdir out/libreoffice out/original/*.docx
python3 inspect.py out/original/*.docx out/libreoffice/*.docx
```

Files saved by Word on the desktop go in `out/word-desktop/` and by Microsoft 365 in
`out/word-365/`, then `python3 inspect.py` runs over them the same way.
