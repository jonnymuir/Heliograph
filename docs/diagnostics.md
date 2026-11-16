# Diagnostics

Every problem Heliograph reports has a stable code, a message in plain words, a suggested fix
and, where it has one, a place in the file. A person and an agent get the same ones. The codes
are in `DiagnosticCodes`; a test fails if a code is missing from this page.

Each has a severity. An **error** stops the template compiling or the document rendering. A
**warning** is something for the author to look at: it may be a mistake, and it stops nothing.
A template that compiles can still come back with warnings.

## A file that is refused (HG0)

The file is not read any further. See the brief's constraint that
[uploaded files are untrusted](brief.md#constraints).

| Code | Meaning | What to do |
|---|---|---|
| `HG0001` | The file is not a Word document (`.docx`) that can be read: not a zip, not a Word document inside, malformed XML, or XML that declares a document type. An older `.doc` file or one protected with a password gets a message that says so. | Save it as a Word document (`.docx`), with no password, from a word processor. |
| `HG0002` | The document contains macros, or is in a format that allows them (`.docm`). | Save it as `.docx`. |
| `HG0003` | The document contains an embedded file, object or control. | Remove it, or paste it as a picture or text. |
| `HG0004` | The document loads content from outside the file, such as a linked picture or a template on a file share. A link a reader clicks is allowed. | Put the content in the document itself. |
| `HG0005` | The file has more than 2,000 parts, is larger than 64 MB, or unpacks to more than 64 MB. | Make it smaller, for example by compressing pictures. |
| `HG0006` | The document has tracked changes that have not been accepted or rejected. They would be sent to every recipient, deleted text included. | Accept or reject every change and upload it again. |
| `HG0007` | The document has comments in it. They would be sent to every recipient. | Delete every comment and upload it again. |
| `HG0008` | The document is digitally signed. Filling it in would break the signature. | Remove the signature and upload it again. |
| `HG0009` | The document is saved as "Strict Open XML Document", in which no tag would be found. | Save it as "Word Document (.docx)". |

## A template that is wrong (HG1)

Found when a template is compiled. Most have a location: the part (`document`, `header`,
`footer`, `footnotes` or `endnotes`), the paragraph number in that part, and the start of the
paragraph's text.

| Code | Meaning | What to do |
|---|---|---|
| `HG1001` | A tag names a field the dictionary does not have. The fix names the nearest field when one is close. | Use a field from the dictionary. |
| `HG1002` | A tag is opened with `((` and not closed in the same paragraph. | Add `))` after the field's name. |
| `HG1003` | A tag has nothing between `((` and `))`. | Put a field's name between the brackets. |
| `HG1004` | A condition, `((if Field))`, is not closed where it must be. On a line of its own, it needs an `((end if))` on a line of its own in the same part of the document: not outside the table cell or the row it starts in. Inside a sentence, it needs its `((end if))` in the same paragraph, and both inside a link or both outside it. | Add or move `((end if))` as the fix describes. |
| `HG1005` | An `((end if))` has no `((if ...))` before it to end, or sits inside a sentence with no condition open in that paragraph. | Remove it, or add the `((if ...))` it belongs to. |
| `HG1006` | A condition names a field that is not a yes or no field. | Use a yes or no field after `if`. |
| `HG1007` | A yes or no field is used as a value. It cannot be printed. | Wrap the content in `((if Field))` and `((end if))`, or have the words supplied as a text field. |
| `HG1008` | A tag is somewhere tags are not filled in: a picture's description, a field's code, the address of a link, the document's title, a chart. It would be left exactly as typed. An error when it names a real field or is a condition tag; a warning when it only looks like a tag. | Move it into the text of the document, or remove it. |
| `HG1009` | Warning. Text looks like a tag for a real field but is not written as one: the field's name with the wrong brackets, such as `{{Name}}` or `[[Name]]`, or with one bracket missing. It will be printed as it is. | Write it as `((Name))`. If it is ordinary text, leave it. |
| `HG1010` | Warning. The document has hidden text, which is sent to every recipient inside the file. Reported once for each part. | Delete it if recipients should not have it. |

## Data that is wrong for one recipient (HG2)

Found when a compiled template is rendered. No document is produced. Only fields the template
uses are checked, and a field used only inside a condition that is no for this recipient is not
needed.

| Code | Meaning | What to do |
|---|---|---|
| `HG2001` | The data has no value, or a null value, for a field the template uses. | Supply the value under the field's key. |
| `HG2002` | The value is not of the field's type: text that is not a string, a date that is not an ISO date (`2027-06-19`), an address that is not a list of text lines, a number or an amount of money that is not a number, a yes or no that is not `true` or `false`. | Supply the value in the form the fix describes. |
