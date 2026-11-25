---
name: diagnostics
description: Every diagnostic code Heliograph reports, HG0 to HG3, with what it means and what to do about it. Use when a compile, a save, a publish or a render comes back with a code.
---

# Diagnostics

Every problem Heliograph reports has a stable code, a message in plain words, a suggested fix
and, where it has one, a place in the file. A person and an agent get the same ones. The codes
are in `DiagnosticCodes`; a test fails if a code is missing from this page.

Each has a severity. An **error** stops the template compiling or the document rendering. A
**warning** is something for the author to look at: it may be a mistake, and it stops nothing.
A template that compiles can still come back with warnings.

## A file that is refused (HG0)

The file is not read any further. See the brief's constraint that
[uploaded files are untrusted](../../brief.md#constraints).

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
paragraph's text. In a message the part is `sms`, `email` or `email subject`, and the number is
the line.

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
| `HG1011` | A condition starts or ends in the middle of a field, such as a page number, a cross-reference or a table of contents. Hiding it would leave half a field. | Move the tags so the whole field is inside the condition, or outside it. |
| `HG1012` | A condition contains a section break, or one of its tags is on the line that carries one. Hiding it would merge two sections, changing page size, margins, headers and footers. | Move the section break outside the condition, and off the tags' lines. |
| `HG1013` | The compiler rehearsed the template for every outcome of its conditions, and one would produce a document that cannot be opened. The message says which outcome and what is wrong. This is a fault in Heliograph, caught before any letter is made. | Report it with the template. Moving the nearest condition may work round it. |
| `HG1014` | A field that belongs to a list is used outside a table row, or a list itself is used as a value. A list's fields can only go in a row of a table, which is repeated for each item. | Put the field in a table row. |
| `HG1015` | A table row uses fields from two different lists. A row can repeat for one list only. | Give each list a row, or a table, of its own. |
| `HG1016` | A row that would repeat cannot: it is marked as a heading row, one of its cells is merged with a cell above or below, or it holds a footnote or an endnote. | Use an ordinary row for the list's fields. |
| `HG1018` | An email has no subject, or its subject is more than one line. An email kept in one file must start with a line `Subject: ...`. | Give it a subject of one line. |
| `HG1019` | A link in an email does not plainly go to a web page, an email address or a phone number: its address does not start with `https://`, `http://`, `mailto:` or `tel:` written out in the template, or it has a condition inside it. Such a link could be made to go anywhere, or to run script. | Write the start of the address in the template. A value can fill in the rest. Put a condition round a whole link, not inside its address. |
| `HG1017` | Warning. A text message template has a character outside the basic set for text messages, such as a curly quote. Every message from it will hold 70 characters, not 160, so most will be charged as more than one. Reported once, at the first such character. | Replace it with a plain character unless you need it. |
| `HG1020` | Warning. The document has no title in its properties. A PDF made from it will not meet the standard for accessible PDF, because a screen reader announces the title first. | Set a title that says what the letter is: **File > Properties > Description** in LibreOffice, **File > Info** in Word. |
| `HG1021` | The tag sheet from the starter document is still in the document. Its tags would be filled in and sent to every recipient. Reported on its own, because the sheet would set off many other diagnostics. | Delete the page that starts "Heliograph tag sheet", and everything after it. |
| `HG1022` | Warning. A picture has no alternative text, so a screen reader skips it in an accessible PDF. Reported once in each part. | Describe the picture: right-click it, then **Edit Alt Text** in Word or **Properties > Options > Alternative (Text only)** in LibreOffice. If it is only decoration, mark it as decorative in Word. |
| `HG1010` | Warning. The document has hidden text, which is sent to every recipient inside the file. Reported once for each part. | Delete it if recipients should not have it. |

## Data that is wrong for one recipient (HG2)

Found when a compiled template is rendered. No document is produced. Only fields the template
uses are checked, and a field used only inside a condition that is no for this recipient is not
needed.

| Code | Meaning | What to do |
|---|---|---|
| `HG2001` | The data has no value, or a null value, for a field the template uses. | Supply the value under the field's key. |
| `HG2002` | The value is not of the field's type: text that is not a string, a date that is not an ISO date (`2027-06-19`), an address that is not a list of text lines, a number or an amount of money that is not a number, a yes or no that is not `true` or `false`. | Supply the value in the form the fix describes. |

## Stored correspondence (HG3)

Reported by `CorrespondenceLibrary`, which keeps drafts and published versions in the host's
store. None of these has a place in a file.

| Code | Meaning | What to do |
|---|---|---|
| `HG3001` | There is no correspondence with this key. | List the correspondence to see the keys that exist. |
| `HG3002` | The correspondence has no published version, or not the version asked for. A draft is never rendered for a recipient. | Publish it, or ask for a version that exists. |
| `HG3003` | The correspondence names a dictionary that does not exist. | Choose one of the dictionaries that exist. |
| `HG3004` | The dictionary the correspondence names cannot be read. The message says why. | Whoever maintains the dictionary corrects it. |
| `HG3005` | The correspondence has no parts. It is saved as a draft and cannot be published. | Add a document, an email or a text message. |
| `HG3006` | Someone else saved the draft after this copy was read, so this copy was not saved. | Read it again, make the change to that copy, and save it. |
| `HG3008` | A dictionary or sample name cannot be used: it is not letters, digits and hyphens, starting with a letter or digit. | Use a name such as `licensing`. |
| `HG3009` | Dictionaries cannot be changed here: the site takes them from a system that knows its own data. | Ask whoever maintains that data to change the dictionary. |
| `HG3010` | A sample recipient is not a JSON object. | Write it as `{ "fieldKey": "value", ... }`, with the dictionary's keys. |
| `HG3007` | The key cannot be used: it is not lower-case letters, digits and hyphens, starting with a letter. | Use a key such as `decision-letter`. |

## What the PDF route cannot lay out (HG4)

Reported only where the site makes its PDFs with Heliograph's own converter (`Heliograph.Pdf`).
Each one names something in the template that the converter does not lay out yet, so a PDF
would be missing it or would look wrong. Each is reported once in each part, at the first place
it occurs. They apply while a letter is written and published. A version already published goes
on rendering as it was.

The fix is always one of two things. Change the template, or ask whoever runs the site to make
that letter's PDFs with LibreOffice (`Heliograph.Pdf.LibreOffice`) until the converter supports
it. The list shrinks as the converter grows; see [docs/pdf-engine.md](../../pdf-engine.md).

| Code | Meaning | What to do |
|---|---|---|
| `HG4001` | A picture the converter cannot draw yet: one with text wrapped tight to its outline, wrapped round it in a header or footer, or with room for text on both sides of it; one floating in a table; an old-style picture; or a file that is not a PNG or JPEG, is damaged, or has more than 25 million pixels. | Set the picture in line with text, in front of or behind it, or wrap text Square or Top and Bottom on one side of it in the body of the letter. |
| `HG4002` | A text box, shape or chart; a paragraph in a frame or a drop capital; content imported from another file; text spread across the whole line; or a section break inside a content control. | Put the text in an ordinary paragraph or table, aligned left, right, centred or justified. |
| `HG4003` | A note with a mark of its own, notes numbered other than through the letter or in a format other than figures, letters or Roman numerals, footnotes placed other than at the foot of the page or endnotes other than at the end, sections that number notes differently, or a table or floating picture in a note. | Put the note's text in the body of the letter, or make it an ordinary footnote or endnote there. |
| `HG4004` | A page setting it does not lay out yet: a section break that does not start a new page next to text in columns, a border round the page, line numbers, text set in the middle of the page, page numbers in another format or with chapter numbers, or a document grid, or a page break in a table, header, footer or note. | Use a section break that starts a new page, with the page settings Word gives a new document otherwise. |
| `HG4005` | Text set in columns of different widths, or footnotes, endnotes or floating pictures in a letter set in columns. Columns of equal width are laid out. | Use columns of equal width, and keep notes and floating pictures out of a letter in columns, or use a table. |
| `HG4006` | A field other than a page number or a count of pages in the letter or its section, or one of those with a switch other than figures, letters or Roman numerals, or a count of pages in the body or a page field in a table or note, which would not print as it should. | Type the text in, or use a tag from the dictionary. |
| `HG4007` | A link the converter cannot make: one in a header or footer, one to a place in the document, or one to an address that is not a web page, email address or telephone number. Links in the body to those are made. | Write the address out as plain text, or link to a web page, email address or telephone number. |
| `HG4008` | Formatting or run content the converter cannot draw yet: a symbol from a symbol font such as Wingdings, an optional hyphen, a phonetic guide, a date block, an equation, a border round text, a dotted, dashed or wavy underline, patterned shading, shading behind a paragraph in a table, header or footer, or behind a whole table, stretched or squeezed characters, text raised or lowered by a set amount, embossed, engraved, outlined or shadowed text, emphasis marks, text fitted to a width, a border round a paragraph in a table, header or footer, a bar beside a paragraph, spacing set to automatic as in text pasted from a web page, or automatic hyphenation. It would print as plain text. | Take that formatting off, or use the LibreOffice route for this letter. |
| `HG4009` | Right-to-left text, or text running up or down the page. | Set the text left to right. |
| `HG4010` | A table the converter cannot lay out yet: a table inside another whose cells are merged down a column, one that floats with text around it, or one whose style formats some rows or columns differently (a shaded heading row or banded rows) where the table turns that on. Cells merged down a column and cell shading are laid out. | Take the table out of the other, take the conditional formatting off (Table Design in Word), or set the table in line with the text. |
| `HG4011` | A tab stop drawn as a bar, or a tab in a centred or right-aligned paragraph, so text after it would not line up. Right, centred and decimal tab stops, and leaders, are laid out. | Use a left, right, centred or decimal tab stop in a left-aligned paragraph, or a table with the column aligned as you need. |
| `HG4012` | Margins mirrored on facing pages, or pages printed as a booklet. | Set the margins to Normal, not Mirrored. |
| `HG4013` | A warning, not an error: a font the converter does not have, named in the text or the styles, directly or through the document's theme (Aptos, the default in Word since 2024, for one). It is drawn in Liberation Sans, so lines may break differently. | Use Arial, Times New Roman, Courier New, Calibri or Cambria, which have look-alikes with the same widths, or ask whoever runs this site to give the converter the font. |
| `HG4014` | A list numbered in a style the converter does not write (only numbers, letters and Roman numerals, with or without leading zeros), a picture used as a bullet, or a bullet character it has no shape for. | Use a numbered, lettered or Roman-numeral list, or an ordinary bullet. |
| `HG4015` | A character in the template none of the built-in fonts can draw (text in a script they do not cover, such as Chinese or Arabic, an emoji, or a symbol font's private characters). It would print as an empty box. The same in a recipient's data makes that letter's conversion fail, saying so. | Use characters from the Latin, Greek or Cyrillic alphabets and ordinary punctuation, or use the LibreOffice route for this letter. |
