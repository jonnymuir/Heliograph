# Edge cases

Every way a document template can go wrong that has been thought of, what Heliograph does about
it, and when. It is a register: a case is added when it is found, and its status changes when the
engine does.

The aim is that nothing is discovered when a letter is rendered for a real person. A problem is
caught when the template is compiled, or it cannot happen. The only things left for render time
are faults in one recipient's data, which do not exist until then.

## The three camps

1. **A tag that is not recognised.** The author believes they wrote a tag. The engine sees
   ordinary text, so the letter goes out with `((Applicant name))` printed in it. This is the
   worst kind because nothing fails.
2. **A document that would be structurally broken.** Filling in a value, hiding content or
   repeating a row leaves a file that a word processor repairs, mangles or refuses to open.
3. **A letter that says more than the author meant.** The template carries something that is
   copied to every recipient: a comment, a tracked deletion, a member of staff's name.

## Strategies

Each case below is handled by one of these. "Compile" means the author is told at design time.

| | Strategy | When |
|---|---|---|
| S1 | **Fold what editors change.** Before a field name is matched, capitals, spacing, curly quotes, dashes and invisible characters are folded to one form. | Compile |
| S2 | **Join what editors split.** A paragraph's text is joined before tags are read, however many pieces and formats it is stored in. | Compile |
| S3 | **Spot the near miss.** Text that looks like a tag for a real field but is not one (wrong brackets, one bracket missing) is pointed out with a warning. It cannot be an error, because `(Applicant name)` may be exactly what the author meant to print. | Compile |
| S4 | **Leave no tag behind.** After compiling, every part of the file is searched, text and attributes alike. A tag found anywhere it was not processed is reported with the place. | Compile |
| S5 | **Refuse what cannot be honoured.** A file whose format or content cannot be handled safely is refused whole, with what to do. | Compile |
| S6 | **Check structure before accepting.** A condition must not cut through anything that has a start and an end of its own: a table cell, a link, a field, a bookmark, a section. | Compile |
| S7 | **Rehearse every outcome.** The compiler renders the template for every combination of its conditions, with the most awkward values each field's type allows, and checks each result. A template that would fail for some recipient fails here instead. | Compile |
| S8 | **Mend what is always safe to mend.** Where the format demands something trivial, such as a paragraph in an emptied table cell, the renderer adds it. | Render, proved by S7 |
| S9 | **Strip what must never travel.** Names of people recorded in the file are emptied. | Compile |
| S10 | **Check the checker.** In the tests, independent judges look at what the engine produces. See [How the register is tested](#how-the-register-is-tested). | Tests |

## Status

- **Works**: handled, and a test proves it.
- **Refused**: the template does not compile, and a test proves it.
- **Mended**: the renderer makes it valid, and a test proves it.
- **Planned**: the strategy is chosen and not built. Until it is, the case can reach a recipient.

A case that is pointed out with a warning counts as "Works": the author is told at compile, and
the template is not stopped, because the text may be what they meant.

## 1. A tag that is not recognised

### 1.1 The editor changed the tag's text

| Case | What happens | Strategy | Status |
|---|---|---|---|
| Capitals or spacing differ from the dictionary | Read as the same field | S1 | Works |
| A non-breaking space in the name | Read as the same field | S1 | Works |
| The editor turned `'` into `’`, `"` into `”` or `-` into `–` inside the name | Read as the same field | S1 | Works |
| An invisible character in the name (soft hyphen, zero-width space), usually from pasting | Read as the same field | S1 | Works |
| The tag is stored in several pieces | Joined and read | S2 | Works |
| Formatting changes half-way through the tag | Read. The value prints in the formatting the tag starts with | S2 | Works |
| A spelling or grammar mark, or a bookmark, sits inside the tag | Read | S2 | Works |
| A tab or a line break sits inside the tag | Reported as an unknown field, with the nearest field suggested | S2 | Refused |
| A new paragraph was started inside the tag | The first half is reported as a tag that is not closed | S3 | Refused |
| The name is misspelt | Reported, with the nearest field suggested | S3 | Refused |
| `((` with no `))`, or nothing between them | Reported | S3 | Refused |
| One bracket instead of two, or `))` with no `((`, around a real field's name | Pointed out as a likely tag, as a warning | S3 | Works |
| The wrong brackets around a real field's name: `{{ }}`, `[[ ]]`, `<< >>`, `« »` | Pointed out as a likely tag, as a warning | S3 | Works |
| The editor's autocorrect replaced part of a tag, for example `(c)` becoming `©` | Not detected unless what is left is near a field's name | S3 | Planned |
| The author means the text `((something))` literally | Reported as an unknown field. There is no way to write it yet | | Planned |

### 1.2 The tag is somewhere the engine does not look

| Case | What happens | Strategy | Status |
|---|---|---|---|
| In the body, a table, a header or a footer, including first-page and even-page ones | Read | | Works |
| In a text box | Read, in both copies an editor may store | | Works |
| In a footnote or an endnote | Read | S4 | Works |
| In a comment | The template is refused. See 3 | S5 | Refused |
| In a picture's alternative text or title | Reported: it would not be filled in | S4 | Refused |
| In a chart, a diagram or other drawing text | Reported: it would not be filled in | S4 | Refused |
| In a field's code, such as the address a link goes to | Reported: it would not be filled in | S4 | Refused |
| In the document's properties, such as its title | Reported: it would not be filled in | S4 | Refused |
| In text marked as deleted by tracked changes | Ignored, correctly. See 3 | S5 | Works |
| In text marked as moved away by tracked changes | The template is refused. See 3 | S5 | Refused |
| In hidden text | Read and filled in, and still hidden | | Works |

### 1.3 The file is not what it seems

| Case | What happens | Strategy | Status |
|---|---|---|---|
| Not a zip, or a zip that is not a Word document | Refused | S5 | Refused |
| An older `.doc`, or a file protected with a password | Refused, with a message that names both likely causes | S5 | Refused |
| A macro-enabled document or template | Refused | S5 | Refused |
| Saved as "Strict Open XML Document", which uses different names for everything | Refused, with how to save it instead | S5 | Refused |
| An OpenDocument file (`.odt`) | Refused as not a Word document | S5 | Refused |

## 2. A document that would be structurally broken

### 2.1 By hiding content

| Case | What happens | Strategy | Status |
|---|---|---|---|
| A condition around paragraphs, lists and whole tables | Removed cleanly with its tag lines | | Works |
| A condition inside a condition | Inner content needs both | | Works |
| A condition hides some items of a list | The rest stay a list | | Works |
| A condition hides everything in a table cell | The cell keeps an empty paragraph | S8 | Mended |
| A condition hides everything in a header, a footer, a text box or a footnote | The format requires a paragraph there | S8 | Planned |
| A sentence that was all condition is hidden | Its paragraph is removed, leaving no gap | | Works |
| A sentence holds several conditions and ends up empty | Its paragraph is removed, whichever order the conditions come in | | Works |
| A condition opens in one table cell or row and closes in another, or opens in a table and closes outside | Refused | S6 | Refused |
| A condition opens inside a link and closes outside it | Refused | S6 | Refused |
| A condition opens inside a content control and closes outside it | Refused, with a message that mentions a link | S6 | Refused |
| A condition opens in the body and closes in a header or footer | Each is reported as unpaired | S6 | Refused |
| A condition starts or ends in the middle of a field, such as a page number, a cross-reference or a table of contents | The field is cut in two and the file may not open | S6 | Planned |
| A condition contains the start of a bookmark and not its end, or the reverse | A cross-reference to it breaks | S6 | Planned |
| A condition contains a section break, or a tag line carries one | Sections merge, changing page size, margins and headers | S6 | Planned |
| A whole table row or column should appear only sometimes | Cannot be written: a tag lives in a paragraph, and a paragraph in a cell | | Planned with lists |
| An unpaired tag: an end with no start, or a start with no end | Reported once, where the mistake is | S3 | Refused |

### 2.2 By filling in a value

| Case | What happens | Strategy | Status |
|---|---|---|---|
| Text containing `<`, `&`, quotes or anything that looks like markup or a tag | Printed as text | | Works |
| Characters the format cannot hold, such as control characters | Dropped | | Works |
| Text with a blank line, in an ordinary paragraph, a heading, a list item or a table cell | New paragraphs in the same style | | Works |
| Text with a blank line, where the tag is inside a link | A blank line, not a new paragraph | | Works |
| Text with a blank line, where the tag is inside a field's result | The paragraph is split through the field | S7 | Planned |
| A paragraph that carries a section break is split by a value | The break stays on the last paragraph | | Works, untested |
| An empty value | Prints nothing. A paragraph that held only the tag stays as a blank line | | Works |
| A tag inside text a field recalculates, such as a table of contents | Filled in, then overwritten when the field updates | S6 | Planned |
| A content control bound to stored data holds a tag | The editor may replace the filled value with the stored one | S5 | Planned |

### 2.3 By repeating content

Not built. These are the cases the list work must answer.

| Case | Strategy |
|---|---|
| A list with no items, one item, many items | S7 |
| A repeated row that is part of a vertically merged cell | S6 |
| A repeated row marked as a header row | S6 |
| A repeated row that holds a bookmark, a picture or a footnote, each of which must be unique | S8 |
| A repeated row that holds a condition, or a nested table | S7 |
| Values from two different lists in one row | S6 |
| A list's value used outside any row | S6 |

### 2.4 By the file's own contents

| Case | What happens | Strategy | Status |
|---|---|---|---|
| The file is digitally signed | Refused | S5 | Refused |
| Pictures, links and footnotes that hidden content referred to | Left in the file unused, which is valid | | Works |
| Word and page counts stored in the file | Left as the template had them | | Works |

## 3. A letter that says more than the author meant

| Case | What happens | Strategy | Status |
|---|---|---|---|
| The author's name and the last person to save | Emptied | S9 | Works |
| The names of people who commented or tracked changes | Emptied | S9 | Works |
| A company or a manager recorded in the file | Emptied | S9 | Works |
| Comments | Refused | S5 | Refused |
| Tracked changes not yet accepted or rejected | Refused | S5 | Refused |
| Hidden text | Pointed out, as a warning. Sent, still hidden | S3 | Works |
| A picture or template loaded from outside the file | Refused | S5 | Refused |
| An embedded file or object | Refused | S5 | Refused |

## How the register is tested

A case in the register is one somebody thought of. These tests do not depend on that.

| Judge | What it does | What it cannot see |
|---|---|---|
| **A model of the letter** | Hundreds of templates are built at random: paragraphs and tables, conditions around and inside them to any depth, values everywhere, text chopped into runs. The generator works out from its own plan what each letter should say for each recipient, and that is compared with what the engine printed. A third of the templates are given one misplaced condition tag and must be refused. | Anything the generator does not build: links, fields, footnotes, pictures, lists |
| **Chopping** | Each benchmark letter is chopped into runs at random points, with random formatting, and must read the same. | Structure, which chopping does not change |
| **The format's schema** | Every rendered file is validated with Microsoft's Open XML validator, and must have no problem the template did not have. | Rules Word enforces beyond the schema. It accepts a table cell with no paragraph, which Word reports as an unreadable file |
| **Word's stricter rules** | Written by hand in the tests, apart from the engine: a cell ends with a paragraph, a table has rows, a header, footer, note or text box is not empty, a field that begins also ends, and nothing of the compiler's is left in the file. | Any rule nobody has written down. No copy of Word has checked them |
| **LibreOffice** | Opens and saves benchmark letters and rendered files. | It is more forgiving than Word |

A failure names a seed, so the document that caused it can be built again:
`new RandomTemplate(seed)`. The first run of the model found the case above about a sentence
with several conditions.

## What can only be known at render time

These depend on one recipient's data, so they cannot be found earlier. No document is produced,
and the diagnostics say what to supply.

- A value the template needs is missing or null.
- A value is not of its field's type.

A host can run the same check on sample data at design time, and on real data before it asks for
a render.
