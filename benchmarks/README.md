# Benchmark letters

Three fictional pieces of correspondence of rising difficulty. They are the acceptance test for
Heliograph (see the [brief](../docs/brief.md#how-we-will-know-it-worked)): each must be buildable
in a stock editor by a content designer, survive a save in every supported editor, compile, and
render correctly for every sample.

They are set in the world Wayfinder's reference apps already use: the National Juggling
Federation (NJF), which licenses juggling events and collects members' contributions. Every
person, address and reference is invented. Postcodes start `ZZ`, which no real postcode does.

| Letter | What it adds | What it proves |
|---|---|---|
| [Acknowledgement](acknowledgement/README.md) | Values only | The dictionary, typed formatting of dates, an address block, the three parts |
| [Decision letter](decision-letter/README.md) | Conditions | Showing and hiding paragraphs, a section that appears for only some recipients, free text from outside |
| [Annual statement](annual-statement/README.md) | Lists, money, shared parts | A table that runs over pages, totals, negative amounts, an empty list, a letterhead and footer swapped by a value |

## What is in each folder

- `README.md`: the words of each part, the dictionary the correspondence needs, and what makes
  it hard.
- `document.docx` and `dictionary.json`, once the letter has been built: the template as an
  editor saved it, and its dictionary. All three letters have both. The annual statement's
  letterhead and footer are written into its template until shared parts are built.
- `document.word-365.docx`: the same template after a person edited and saved it in Word in
  Microsoft 365. The same tests run over it. It differs in wording where the person changed it.
- `samples/`: data for one recipient per file. Dates are ISO dates and money is a plain number,
  because the dictionary's types decide how they are printed, not the data and not the author.
  The one thing an author does write is the currency sign, beside the tag.

The other two templates follow in [plan](../docs/plan.md) step 2.

## How the drafts are written

The notation below is for reading these drafts. It is not the template syntax, which is still an
open decision.

- **[Event name]** is a value from the dictionary.
- *Only when [condition]:* introduces content shown when a yes or no value is yes.
- *For each [list]:* introduces content repeated for every item.
- *Shared part [name]:* is content kept once and included by name.
