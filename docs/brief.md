# Design brief

Draft. The maintainer owns every decision listed under [Open decisions](#open-decisions); agents
recommend, with the trade-off, and wait. [architecture.md](architecture.md) draws the design.

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

## Decided

- **Scope:** Heliograph composes and renders. Delivery belongs to the host.
- **Who authors:** content designers and service designers, at design time, in an editor. A
  template is data, not code, in the way a Wayfinder blueprint is.
- **Shape:** the Wayfinder model. An engine that runs on its own with a reference host, and
  [Heliograph.Umbraco](https://github.com/jonnymuir/Heliograph.Umbraco) to host it in Umbraco.
  Wayfinder starts a render at run time by calling Heliograph as a support system; a batch job
  can do the same. See [architecture.md](architecture.md).

## Open decisions

1. **The content model.** Structured blocks (heading, paragraph, list, address, table, call to
   action) rendered per channel, or a plain formatting language in the style of GOV.UK Notify.
   Either way it is data a content designer can author.
2. **Channels for the first release**, and which are first-class versus derived (is SMS written
   separately, or cut down from the email?).
3. **PDF engine**, chosen with its licence and its PDF/UA support in view.
4. **The data contract**: how a template declares the data it needs, and whether conditions and
   calculations reuse Wayfinder's expression language or stay independent of it.
5. **Languages**, including bilingual correspondence (English and Welsh in the same letter).
6. **The Wayfinder join**: reference-app code only, or a small adapter package; and one
   support-system capability per template, or one capability that takes the template as an input.
7. **Versioning**: how a template changes while journeys that will use it are already running,
   and how a host records which version a person was sent.
