# Annual statement

Sent to every member each year: what they paid in contributions, and what they owe or are owed.
Adds lists, money and shared parts. This is the letter the field-marking spike is run on.

## Dictionary

| Name | Key | Type | Sample |
|---|---|---|---|
| Member name | `memberName` | Text | Alex Applicant |
| Member address | `memberAddress` | Address | 12 Tumble Lane, Clubtown, ZZ1 1ZZ |
| Member reference | `memberReference` | Text | M-0042170 |
| Membership tier | `membershipTier` | Text | Performer |
| Regional office | `regionalOffice` | Choice: `northern`, `southern` | southern |
| Statement start | `statementStart` | Date | 2025-12-01 |
| Statement end | `statementEnd` | Date | 2026-11-30 |
| Letter date | `letterDate` | Date | 2026-12-05 |
| Contributions | `contributions` | List | 12 rows |
| Total paid | `totalPaid` | Money | 150.00 |
| Amount due for the year | `amountDueForYear` | Money | 150.00 |
| Has contributions | `hasContributions` | Yes or no | yes |
| No contributions | `noContributions` | Yes or no | no |
| Amount owed | `amountOwed` | Money | 0.00 |
| Owes money | `owesMoney` | Yes or no | no |
| Payment due date | `paymentDueDate` | Date | 2027-01-04 |

Each row of `Contributions` has:

| Name | Key | Type | Sample |
|---|---|---|---|
| Date | `date` | Date | 2026-01-02 |
| Description | `description` | Text | Monthly contribution |
| Amount | `amount` | Money | 12.50 |
| Running total | `runningTotal` | Money | 12.50 |

Running totals, the total paid and the amount owed arrive already worked out. The template adds
nothing up.

## Shared parts

| Name | Chosen by | Contents |
|---|---|---|
| Letterhead | `Regional office` | The NJF name and the office's own address and phone number. Northern: NJF Northern Office, 1 Cascade Row, Upper Throwing, ZZ2 1ZZ. Southern: NJF Southern Office, 8 Mills Mess Way, Clubtown, ZZ1 3ZZ |
| Footer | `Regional office` | The office's opening hours, with "Page X of Y" |
| Continuation header | Nothing | On every page after the first: **[Member name]**, **[Member reference]**, "Statement continued" |

## Document

> *Shared part [Letterhead], for [Regional office]*
>
> **[Member name]**
> **[Member address]**
>
> **[Letter date]**
>
> Member reference: **[Member reference]**
>
> Dear **[Member name]**,
>
> # Your contributions statement
>
> This statement covers **[Statement start]** to **[Statement end]**. You are a
> **[Membership tier]** member.
>
> *Only when [Has contributions]:*
>
> > | Date | Description | Amount | Running total |
> > |---|---|---:|---:|
> > | *For each [Contributions]:* **[Date]** | **[Description]** | **[Amount]** | **[Running total]** |
> > | | **Total paid** | | **[Total paid]** |
> > | | **Amount due for the year** | | **[Amount due for the year]** |
>
> *Only when [No contributions]:*
>
> > We did not receive any contributions from you in this period.
>
> *Only when [Owes money]:*
>
> > ## You owe **[Amount owed]**
> >
> > Pay **[Amount owed]** by **[Payment due date]** to keep your membership. Quote your member
> > reference, **[Member reference]**.
>
> ## If something looks wrong
>
> Contact your regional office using the details at the top of this letter.
>
> Yours sincerely,
>
> Contributions Team
> National Juggling Federation
>
> *Shared part [Footer], for [Regional office]*

## Email

Subject: Your NJF contributions statement

> Dear **[Member name]**,
>
> Your contributions statement for **[Statement start]** to **[Statement end]** is attached.
>
> - Total paid: **[Total paid]**
> - Amount due for the year: **[Amount due for the year]**
>
> *Only when [Owes money]:*
>
> > You owe **[Amount owed]**. Pay by **[Payment due date]** to keep your membership.
>
> Contributions Team
> National Juggling Federation

## SMS

> NJF: your yearly contributions statement is on its way. *Only when [Owes money]:* You owe
> **[Amount owed]**, due by **[Payment due date]**.

## What makes it hard

- A table row repeats for each item, between a heading row and fixed total rows, inside a
  condition.
- The table runs over several pages (`samples/weekly-payer.json` has 60 rows). The heading row
  repeats on each page and a row never splits across pages. Both are the word processor's own
  table settings, not something Heliograph adds.
- Money is right-aligned, shows two decimal places, and a refund prints as "-£12.50" from the
  number -12.5.
- An empty list shows a sentence, not an empty table.
- The letterhead and footer change with a value, and live in the page header and footer, not
  the body.
- Values appear inside a header (the continuation header) and inside a heading.
- "Page X of Y" is the word processor's own page numbering and must survive the merge.
- The first page has a different header from the rest.
