# Decision letter

Sent when a caseworker approves or refuses a juggling event licence. Adds conditions: whole
sections appear for some recipients and not others.

## Dictionary

| Name | Key | Type | Sample |
|---|---|---|---|
| Applicant name | `applicantName` | Text | Alex Applicant |
| Applicant address | `applicantAddress` | Address | 12 Tumble Lane, Clubtown, ZZ1 1ZZ |
| Application reference | `applicationReference` | Text | NJF-2026-004217 |
| Event name | `eventName` | Text | Clubtown Summer Juggle |
| Event date | `eventDate` | Date | 2027-06-19 |
| Number of jugglers | `numberOfJugglers` | Number | 24 |
| Letter date | `letterDate` | Date | 2026-12-10 |
| Licence approved | `licenceApproved` | Yes or no | yes |
| Licence refused | `licenceRefused` | Yes or no | no |
| Dangerous props | `dangerousProps` | Yes or no | no |
| Licence number | `licenceNumber` | Text | JL-27-00381 |
| Licence fee | `licenceFee` | Money | 45.00 |
| Fee due date | `feeDueDate` | Date | 2027-01-09 |
| Reason for refusal | `reasonForRefusal` | Text, several paragraphs | The risk assessment does not cover the audience area. |
| Review deadline | `reviewDeadline` | Date | 2027-01-07 |

`Licence approved` and `Licence refused` are both supplied, and exactly one is yes. The template
never works one out from the other.

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
> *Only when [Licence approved]:*
>
> > # Your juggling event licence has been approved
> >
> > We have approved a licence for **[Event name]** on **[Event date]**, for up to
> > **[Number of jugglers]** jugglers.
> >
> > Your licence number is **[Licence number]**.
> >
> > ## What you need to do
> >
> > Pay the licence fee of £**[Licence fee]** by **[Fee due date]**. Your licence is not valid
> > until we receive it.
> >
> > *Only when [Dangerous props]:*
> >
> > > ## Fire, knives and other dangerous props
> > >
> > > Because your event includes dangerous props, you must:
> > >
> > > - have a trained fire marshal present for the whole event
> > > - keep the audience at least 5 metres from the performance area
> > > - show this letter to the venue before the event
>
> *Only when [Licence refused]:*
>
> > # We have refused your application for a juggling event licence
> >
> > We cannot license **[Event name]** on **[Event date]**.
> >
> > ## Why
> >
> > **[Reason for refusal]**
> >
> > ## If you think this decision is wrong
> >
> > You can ask for a review. Write to us by **[Review deadline]**, quoting
> > **[Application reference]**, and tell us why you think the decision is wrong.
> >
> > You can also apply again at any time.
>
> Yours sincerely,
>
> Licensing Team
> National Juggling Federation
>
> *Shared part [Footer]*

## Email

Subject, *only when [Licence approved]:* Your juggling event licence has been approved

Subject, *only when [Licence refused]:* A decision on your juggling event licence application

> Dear **[Applicant name]**,
>
> We have made a decision on your application **[Application reference]** for
> **[Event name]**.
>
> *Only when [Licence approved]:*
>
> > Your licence has been approved. Your licence number is **[Licence number]**.
> >
> > Pay the fee of £**[Licence fee]** by **[Fee due date]**. The attached letter has the details.
>
> *Only when [Licence refused]:*
>
> > We have refused your application. The attached letter explains why and how to ask for a
> > review.
>
> Licensing Team
> National Juggling Federation

## SMS

> *Only when [Licence approved]:* NJF: your licence for **[Event name]** is approved. Pay
> £**[Licence fee]** by **[Fee due date]**. Details are in your letter.
>
> *Only when [Licence refused]:* NJF: we have made a decision on your licence application
> **[Application reference]**. Please read the letter we have sent you.

## What makes it hard

- A condition wraps headings, paragraphs and a list, not just a sentence.
- A condition sits inside another condition.
- A hidden section must leave no gap, no empty heading and no stray page break.
- Values that only exist for one outcome (`Licence number`, `Reason for refusal`) are absent
  from the data for the other. That must compile and render, because the content using them is
  hidden. Missing data for content that is shown must fail.
- `Reason for refusal` is free text written by a caseworker, with several paragraphs. Each must
  become a real paragraph in the letter's own style, and the text must not be able to add fields,
  markup or formatting.
- A subject line, and an SMS, need a condition too.
- Money prints as "45.00" from the number 45. The author writes the £ sign in the template, so the
  currency is never chosen for them.
