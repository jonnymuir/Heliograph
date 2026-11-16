# Design brief

Draft. The maintainer owns every decision listed under [Open decisions](#open-decisions); agents
recommend, with the trade-off, and wait.

## The need

**Who it is for**

- A service team that has to write to people at points in a journey: an acknowledgement, a
  reminder, a decision letter, a request for evidence. In Wayfinder these are moments at a stage
  or gateway in a service blueprint.
- A developer who has to add correspondence to an existing system without adopting a whole
  platform.
- The recipient, who needs a message that is accessible, plain and correct on whatever they read
  it with.

**The problem**

The same message usually gets built several times: an HTML email, a shorter SMS, a PDF letter for
post. Each copy is written and tested separately, so the wording drifts, the data merge is
duplicated, and accessibility depends on who built which version. Nobody can say exactly what a
person was sent.

**What Heliograph does about it**

One definition of a piece of correspondence, merged with data, renders to each channel it
supports. Delivery stays with the host.

## How we will know it worked

Proposed, to be agreed:

1. Wayfinder.Umbraco sends one real message (a decision letter, say) as email, SMS and PDF from a
   single definition, with no channel-specific wording in host code.
2. A second, non-Wayfinder host can render correspondence by referencing only the core package.
3. Rendering is deterministic: the same definition and data give byte-identical output, proved by
   snapshot tests.
4. Rendered output meets an accessibility bar per channel (PDF/UA for PDF, semantic HTML for
   email), checked in CI rather than by eye.
5. Merged data cannot inject markup into any channel, proved by a test per channel.

## Scope

| In | Out (the host's job) |
|---|---|
| Defining correspondence and its content | Sending, retries, delivery receipts |
| Merging data into it | Provider adapters (GOV.UK Notify, Twilio, SES, WhatsApp Business API) |
| Rendering per channel: email, SMS, messaging, PDF, print | Scheduling and queues |
| Channel rules: SMS length, PDF page layout, window-envelope address blocks | Recipient preferences, consent and contact details |
| Metadata a sender needs (subject, segments, page count) | Storing what was sent |

## Prior art to learn from

- [GOV.UK Notify](https://www.notifications.service.gov.uk/) templates: one plain formatting
  language for email, text and letter, with placeholders and conditional content. Notify delivers,
  so it is a likely target for a host, and a model for what authors find easy.
- The [GOV.UK Design System](https://design-system.service.gov.uk/) and the GDS guidance on
  writing for users, which Wayfinder already follows.

## Open decisions

1. **Who authors correspondence?** Developers in code, content designers in a CMS such as the
   Umbraco backoffice, or both. This decides most of the rest.
2. **The content model.** Structured blocks (heading, paragraph, list, address, table, call to
   action) rendered per channel, or a template language (Liquid, Razor, a Notify-style markdown).
3. **Channels for the first release**, and which are first-class versus derived (is SMS written
   separately, or cut down from the email?).
4. **PDF engine**, chosen with its licence and its PDF/UA support in view.
5. **The data contract**: what shape data arrives in, and whether conditions and calculations
   reuse Wayfinder's expression language or stay independent of it.
6. **Languages**, including bilingual correspondence (English and Welsh in the same letter).
7. **Package split**: one core package, or a core plus one package per heavy renderer (PDF) so
   hosts only take what they use.
