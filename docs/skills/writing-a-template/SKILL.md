---
name: writing-a-template
description: How to write or change a Heliograph correspondence template. Covers tags for values, conditions, table rows that repeat over a list, the email and text message parts, where tags work, and what stops a template being accepted. Use before writing or editing any part of a correspondence.
---

# Writing a template

For anyone who writes the letters: a content designer, a service designer, or an AI agent doing
the job for them. You need a word processor and nothing else. Word, Word in Microsoft 365 and
LibreOffice all work, with nothing installed.

A template is an ordinary Word document (`.docx`). Lay it out however you like: headings, tables,
lists, headers, footers, pictures, page numbers. Where the letter should change from one person
to the next, you write a **tag**.

## Start from a starter document

Where Heliograph is hosted with its workbench, **Download a starter document** gives you a Word
file to begin from instead of a blank page. It has the letter's title already set, real heading
styles, and on its second page a **tag sheet**: every field in the dictionary with the tag to
copy, brackets included.

Write the letter on the first page, copying tags from the sheet. Then delete the tag sheet, from
the line that starts "Heliograph tag sheet" to the end. If you forget, the upload is refused
(`HG1021`), so the sheet can never reach a recipient.

## The dictionary: which tags you can use

Every correspondence uses a **dictionary**: the list of fields it may use. Each field has a name,
which is what you write in a tag; a key, which is what the data calls it; and a type. The
**Tags** tab of a correspondence lists them with a button to copy each one.

Dictionaries are kept on the workbench's **Dictionaries** screen, in the left-hand list:

- **Add, change or remove a field.** Give it a name (the key fills itself in) and choose its type.
  A **list** is a field that repeats, such as payments: its own fields (each payment's date and
  amount) sit indented beneath it, and **Add a field to …** adds one. See [Lists](#lists) for how
  a document repeats a row over it. Press **Save the dictionary**. The
  answer says which correspondence use the dictionary and whether any now have a problem, such as
  a tag for a field you removed. Versions already published keep the dictionary they were
  published with, so nothing already being sent changes.
- **Sample recipients** are made-up people the preview uses. Pick one to change it, or press
  **New sample** to start one with a value of the right shape for every field. Never use a real
  person.
- **New dictionary** starts from nothing or from a ready-made one the site offers. A site with
  Wayfinder offers one for each service blueprint, with the blueprint's fields already in it.

Some sites take dictionaries from the system that holds the data. There the screen shows them
but cannot change them.

## Tags

A tag is the name of a **field** in double round brackets:

```text
Dear ((Applicant name)),
```

The fields you can use are listed in the **dictionary** for your correspondence. Use the names
exactly as the dictionary gives them. Capitals and extra spaces do not matter, and it does not
matter if your word processor turns a straight quote or a hyphen into a curly quote or a dash.

A tag takes the formatting of the text it starts in. To print a name in bold, make the tag bold.

### How each kind of field is printed

You never write a date or number format. The field's type decides.

| Type | The data holds | The letter shows |
|---|---|---|
| Text | `Alex Applicant` | Alex Applicant |
| Date | `2027-06-19` | 19 June 2027 |
| Number | `12500` | 12,500 |
| Money | `45` | 45.00 |
| Address | a list of lines | each line under the last |
| Yes or no | `true` or `false` | nothing: see [Conditions](#conditions) |

**Write the currency yourself.** A money field prints the number and no sign, so that the
currency is never chosen for you: `£((Licence fee))` prints £45.00. In a table of amounts, put the
currency in the column heading.

**Text keeps its shape.** If the text supplied has a line break, the letter has one. If it has a
blank line, the letter starts a new paragraph there, in the same style as the paragraph your tag
is in.

## Conditions

To show something only to some people, put it between `((if ...))` and `((end if))`. The field
must be a yes or no field.

**Around whole paragraphs, lists and tables**, put each tag on a line of its own. Both lines
disappear from the finished letter, so nothing is left behind:

```text
((if Dangerous props))
Fire, knives and other dangerous props
Because your event includes dangerous props, you must:
  • have a trained fire marshal present
  • keep the audience 5 metres away
((end if))
```

**Inside a sentence**, put both tags in the same paragraph:

```text
Pay £((Licence fee)) by ((Fee due date))((if Dangerous props)), and bring your fire plan((end if)).
```

A condition can go inside another. The inner content is shown only when both are yes.

A template never says "not". If you need to show something when a thing is *not* so, ask for a
yes or no field that says it directly, such as `Licence refused` beside `Licence approved`.

A field used only inside a condition does not need a value for people the condition hides it
from.

## Lists

Some fields belong to a **list**, such as the payments on a statement. The dictionary shows them
under their list. To print a list, make a table and put the list's fields in one row:

```text
| Date     | Description     | Amount (£) |     <- an ordinary row
| ((Date)) | ((Description)) | ((Amount)) |     <- this row is repeated for each payment
|          | Total paid      | £((Total paid)) | <- an ordinary row
```

There is nothing else to write. A row that uses a list's fields is repeated once for each item,
in order, and the rows above and below it stay as they are. Set the table up as you normally
would: mark the first row as a heading row so it repeats on each page, and stop rows breaking
across pages.

- Fields that are not part of the list can go in the repeated row too, and print the same in
  every copy.
- A condition inside the row is decided item by item: `((Description))((if Is refund)) (refund)((end if))`.
- If the list is empty, the repeated row disappears. A table left with no rows at all is removed.
  To say something in its place, put the table in a condition and the sentence in another.
- A list's fields only work in a table row. The row cannot be a heading row, be merged with the
  row above or below, or hold a footnote, and it cannot use fields from two lists.

## Emails

The email part is a subject and a body. Kept as one file, the first line is the subject:

```text
Subject: Your licence for ((Event name))

Dear ((Applicant name)),

# Your licence has been approved

Your licence number is **((Licence number))**. Pay £((Licence fee)) by ((Fee due date)).

- Bring this email
- Bring your [risk assessment](https://www.example.org/risk)

Licensing Team
National Juggling Federation
```

The body uses a small, fixed set of Markdown, chosen because every email program shows it
properly and screen readers can use it:

| You write | You get |
|---|---|
| `# Heading` and `## Smaller heading` | Real headings |
| `- item` or `* item` | A bulleted list |
| `1. item` | A numbered list. The numbers are worked out for you |
| `**words**` | Bold |
| `[words](https://...)` | A link |
| `---` on a line of its own | A rule across the page |
| A blank line | A new paragraph |
| A new line | A line break in the same paragraph |

- Tags and conditions work as everywhere else. A subject can use them too, but stays one line.
- A line that uses a list's fields is repeated for each item, so `- ((Date)): £((Amount))` makes
  a bullet for each.
- A link's address must start with `https://`, `http://`, `mailto:` or `tel:`, written by you. A
  tag can fill in the rest: `[Pay](https://pay.example.org/((Reference)))`.
- Nothing else is Markdown. Anything a recipient's data holds is printed as plain text, whatever
  it looks like.
- Every email is also produced as plain text, for programs that want it.

## Text messages

The SMS part of a correspondence is plain text. Tags, and conditions inside a sentence, work as
they do in a document:

```text
NJF: your licence for ((Event name)) is approved. Pay £((Licence fee)) by ((Fee due date)).((if Dangerous props)) Bring your fire plan.((end if))
```

- A condition's tags on lines of their own show or hide the lines between them.
- A line that uses a list's fields is repeated for each item. There is no table to put it in.
- When a message is rendered you are told how many messages it will be sent, and charged, as. A
  message in the basic character set holds 160 characters. One with any character outside that
  set, such as a curly quote or an accent the set lacks, holds 70. You are warned when the
  template itself has such a character. A recipient's name can still push one message over.

## Give the document a title

Set a title in the document's properties: **File > Properties > Description** in LibreOffice,
**File > Info** in Word. A PDF made from the letter needs it to meet the accessibility standard,
because a screen reader announces the title first. Write it for the recipient ("Your annual
statement"), and do not put a tag in it. If you leave it out you are told when you upload
(`HG1020`), and the template still works.

## Describe each picture

Give every picture alternative text that says what it shows ("The National Juggling Federation
logo"): right-click it, then **Edit Alt Text** in Word, or **Properties > Options > Alternative
(Text only)** in LibreOffice. A screen reader reads it out from the PDF. A picture that is only
decoration can be marked as decorative in Word, and is then passed over. A picture with neither
is a warning when you upload (`HG1022`), and a screen reader skips it.

## Where tags work

| Place | Works |
|---|---|
| The body, tables, lists, text boxes | Yes |
| Headers and footers, including first-page and even-page ones | Yes |
| Footnotes and endnotes | Yes |
| Inside a link's visible text | Yes |
| A picture's description, a link's address, the document's title, a chart | No. You are told, because the tag would be left exactly as typed |

## What stops a template being accepted

When you upload a template it is **compiled**: checked against the dictionary and against the
rules below. You get back every problem at once, each with where it is and what to do. The full
list is in [Diagnostics](../diagnostics/SKILL.md).

The ones you are most likely to meet:

| You did this | You are told |
|---|---|
| Misspelt a field's name | `HG1001`, with the nearest real field |
| Opened `((` and never closed it | `HG1002` |
| Forgot an `((end if))`, or put it in a different table cell from its `((if ...))` | `HG1004` |
| Used a yes or no field as a value | `HG1007` |
| Put a condition's tags so that they cut a page number or a table of contents in half | `HG1011` |
| Put a section break inside a condition | `HG1012` |
| Used a list's field outside a table row | `HG1014` |
| Used fields from two lists in one row | `HG1015` |
| Left tracked changes or comments in the document | `HG0006`, `HG0007`. They would be sent to every recipient |

**Warnings** do not stop anything. They point at something that may be a mistake: `{{Name}}` or
`(Name))` where you may have meant `((Name))`, hidden text, which travels inside the file, or a
character that makes every text message cost more.

## When the PDF cannot be made yet

Some sites make PDFs with Heliograph's own converter, which is new. It does not yet lay out
everything a word processor can. Where it cannot, the workbench says so when you upload, with
the place and a fix (codes `HG4001` to `HG4014`). The main ones are floating pictures, text boxes, links to a place in the document or in a header,
a continuous section break next to text in columns, and a tab in a centred or
right-aligned paragraph. Sections that start a new page are laid out, each with its
own page size, orientation, margins, headers and footers (different on the first page or on even
pages if you ask), and page numbers. Footnotes are laid out at the foot of the page, in the body or in a table, and endnotes after
the letter, numbered as you set them in Word. Tab stops aligned left, right, centred or on the decimal point, with or without a
leader, are laid out, and links in the body to a web page, an email address or a telephone number
can be clicked. Coloured, highlighted and shaded text, strikethrough, superscript, subscript, capitals
and character spacing are all drawn, and so are borders and shading round paragraphs in the body
of the letter. Pictures set in line with the text, or in front of or behind it, are drawn (PNG
and JPEG), and in the body of the letter text can wrap **Square** or **Top and Bottom** round a
picture, on one side of it; text wrapped **Tight** or **Through**, or on both sides, is not yet. Lists can have several levels, numbered with numbers, letters or Roman
numerals, with Word's ordinary bullets; a picture used as a bullet, or a number style such as
"1st, 2nd", cannot be drawn yet. Change the template, or ask whoever runs the site to make that letter's
PDFs another way for now.

## What you cannot do yet

- Repeat anything other than a table row for each item in a list: a bullet, say, or a paragraph.
- Reuse one letterhead or footer across many templates.
- Show or hide a whole table row or column.
- Write the characters `((something))` so that they print as they are.

## Fixing what is wrong, in your own document

When a document has problems, **Download with comments** gives you your own file back with a Word
comment on each paragraph that has one, saying what is wrong and what to do. Fix them in your
word processor and upload the file again. You do not need to delete these comments: Heliograph's
own comments, whose author is "Heliograph", come out by themselves. Anyone else's comments are
still refused, because they would be sent to every recipient.

## Changing a document without downloading it

Once a correspondence has a document, **Open in Word** or **Open in LibreOffice** opens it in your
word processor straight from the workbench. Change it and save as you normally would: the save goes
back to Heliograph as a new draft, checked like any upload. Come back to the workbench to see what
is now wrong, if anything. The link works for eight hours; open it again after that.

This works with Word on the desktop and with LibreOffice. Word in the browser (Microsoft 365)
cannot open it this way: download the document and upload it again instead.

**LibreOffice on a Mac** cannot take a link from the browser: it opens to its start screen and
not the document, a known LibreOffice problem
([bug 151224](https://bugs.documentfoundation.org/show_bug.cgi?id=151224)). On a Mac, **Open in
LibreOffice** copies the document's address instead. In LibreOffice, choose **File › Open**, paste
the address into the file name box and press **Open**. If the Mac's own Open window appears, turn
on **LibreOffice › Settings › LibreOffice › General › Use LibreOffice dialogs** once, then try
again. Saving goes straight back to Heliograph as before.

## Before you upload

1. Accept or reject every tracked change, and turn tracking off.
2. Delete every comment except Heliograph's.
3. Save as "Word Document (.docx)". Not `.doc`, not `.docm`, not "Strict Open XML".
