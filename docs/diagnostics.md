# Diagnostics

Every problem Heliograph reports has a stable code, a message in plain words, a suggested fix
and, where it has one, a place in the file. A person and an agent get the same ones. The codes
are in `DiagnosticCodes`; a test fails if a code is missing from this page.

## A file that is refused (HG0)

The file is not read any further. See the brief's constraint that
[uploaded files are untrusted](brief.md#constraints).

| Code | Meaning | What to do |
|---|---|---|
| `HG0001` | The file is not a Word document (`.docx`) that can be read: not a zip, not a Word document inside, malformed XML, or XML that declares a document type. | Save it as a Word document (`.docx`) from a word processor. |
| `HG0002` | The document contains macros, or is in a format that allows them (`.docm`). | Save it as `.docx`. |
| `HG0003` | The document contains an embedded file, object or control. | Remove it, or paste it as a picture or text. |
| `HG0004` | The document loads content from outside the file, such as a linked picture or a template on a file share. A link a reader clicks is allowed. | Put the content in the document itself. |
| `HG0005` | The file has more than 2,000 parts or unpacks to more than 64 MB. | Make it smaller, for example by compressing pictures. |

## A template that is wrong (HG1)

Found when a template is compiled. Each has a location: the part (`document`, `header` or
`footer`), the paragraph number in that part, and the start of the paragraph's text.

| Code | Meaning | What to do |
|---|---|---|
| `HG1001` | A tag names a field the dictionary does not have. The fix names the nearest field when one is close. | Use a field from the dictionary. |
| `HG1002` | A tag is opened with `((` and not closed in the same paragraph. | Add `))` after the field's name. |
| `HG1003` | A tag has nothing between `((` and `))`. | Put a field's name between the brackets. |

## Data that is wrong for one recipient (HG2)

Found when a compiled template is rendered. No document is produced. Only fields the template
uses are checked.

| Code | Meaning | What to do |
|---|---|---|
| `HG2001` | The data has no value, or a null value, for a field the template uses. | Supply the value under the field's key. |
| `HG2002` | The value is not of the field's type: text that is not a string, a date that is not an ISO date (`2027-06-19`), an address that is not a list of text lines. | Supply the value in the form the fix describes. |
