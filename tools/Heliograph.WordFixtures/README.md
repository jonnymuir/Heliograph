# Word fixtures

Has Microsoft Word lay out the documents in `tests/Heliograph.Tests/Fixtures/Word/inputs`, so the
built-in PDF converter can be checked against Word itself, not only LibreOffice. Never shipped; the
maintainer runs it by hand now and then.

For each `.docx` in `inputs`, it uploads the file to a folder of its own (`Heliograph Word
fixtures`) in the maintainer's OneDrive, downloads it again as PDF through Microsoft Graph
(`/content?format=pdf`, laid out by Word for the web), deletes the upload, and saves
`<name>.word.pdf` beside `inputs`. Commit the PDFs; `WordConformityTests` compares the converter
with each, line by line, as the measuring net compares with LibreOffice. Only the fictional
fixtures go up.

## Once: an app registration

1. Sign in to [entra.microsoft.com](https://entra.microsoft.com) with the Microsoft account whose
   OneDrive to use.
2. **Applications > App registrations > New registration**: name `Heliograph Word fixtures`,
   supported account types **Personal Microsoft accounts only**, no redirect URI.
3. **Authentication**: **Allow public client flows**, Yes.
4. **API permissions > Add a permission > Microsoft Graph > Delegated**: `Files.ReadWrite`.
5. Put the **Application (client) ID** in `Settings.ClientId`, or in `HELIOGRAPH_WORD_CLIENT_ID`.
   It is not a secret: the app has none.

## Each time

```bash
# Write the inputs (only when the probes, the benchmark letters or the generator change)
HELIOGRAPH_WRITE_WORD_INPUTS=$PWD/tests/Heliograph.Tests/Fixtures/Word dotnet test tests/Heliograph.Tests --filter "FullyQualifiedName~The_inputs_can_be_written_for_Word"

# Have Word lay them out: sign in with the code it shows
dotnet run --project tools/Heliograph.WordFixtures
```
