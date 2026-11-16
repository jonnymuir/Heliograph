# Acknowledgement

Sent when someone applies for a juggling event licence. Values only: no conditions, no lists.

## Dictionary

| Name | Key | Type | Sample |
|---|---|---|---|
| Applicant name | `applicantName` | Text | Alex Applicant |
| Applicant address | `applicantAddress` | Address | 12 Tumble Lane, Clubtown, ZZ1 1ZZ |
| Application reference | `applicationReference` | Text | NJF-2026-004217 |
| Event name | `eventName` | Text | Clubtown Summer Juggle |
| Event date | `eventDate` | Date | 2027-06-19 |
| Date received | `dateReceived` | Date | 2026-11-16 |
| Decision due date | `decisionDueDate` | Date | 2026-12-14 |
| Letter date | `letterDate` | Date | 2026-11-17 |

## Document

> *Shared part [Letterhead]*
>
> **[Applicant name]**
> **[Applicant address]**
>
> **[Letter date]**
>
> Our reference: **[Application reference]**
>
> Dear **[Applicant name]**,
>
> # We have received your application for a juggling event licence
>
> We received your application for **[Event name]**, taking place on **[Event date]**, on
> **[Date received]**.
>
> ## What happens next
>
> A caseworker will review your application. We will write to you with a decision by
> **[Decision due date]**.
>
> You do not need to do anything now. We will contact you if we need more information.
>
> ## If your plans change
>
> Tell us as soon as you can if the date, the venue or the number of jugglers changes. Quote
> your reference, **[Application reference]**.
>
> Yours sincerely,
>
> Licensing Team
> National Juggling Federation
>
> *Shared part [Footer]*

## Email

Subject: We have received your application: **[Application reference]**

> Dear **[Applicant name]**,
>
> # We have received your application
>
> We received your application for a juggling event licence for **[Event name]** on
> **[Date received]**.
>
> We will send you a decision by **[Decision due date]**. You do not need to do anything now.
>
> Your reference is **[Application reference]**.
>
> Licensing Team
> National Juggling Federation

## SMS

> NJF: we have your licence application for **[Event name]**. Ref
> **[Application reference]**. We will reply by **[Decision due date]**.

## What makes it hard

- The address must sit where a window envelope shows it, whatever its number of lines.
- Three dates come from the data as ISO dates and print as "19 June 2027". No author writes a
  format.
- The same value appears several times in one part and in all three parts.
- A long event name must not push the SMS into an unexpected number of segments without the
  render saying so.
- A name with an apostrophe or an ampersand (see `samples/awkward-characters.json`) must print
  as typed in all three parts.
