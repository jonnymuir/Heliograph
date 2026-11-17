---
name: reference-app
description: How to run the Heliograph reference app and try every part of Heliograph with the benchmark letters already in it, from the workbench to an agent over MCP, and the smallest code a site needs to host Heliograph. Use when trying Heliograph locally or starting to host it in another system.
---

# The reference app

A small web app in this repo that hosts Heliograph and nothing else. It does two jobs:

- **Try it.** Press Run and you have the workbench with three letters already in it, each with an
  email and a text message, sample recipients to preview with, and a published version.
- **See how little hosting takes.** Its `Program.cs` is the whole of what a site writes to give
  authors the workbench, an API and an MCP server. Everything else in the app is marked as demo.

It never ships. It keeps everything as files in `reference/Heliograph.ReferenceApp/App_Data`.
Delete that folder to start again from the benchmark letters.

## Run it

You need the .NET 10 SDK and Node.js 20 or later. LibreOffice is optional; with it, the preview
shows each letter as a PDF.

| How | What to do |
|---|---|
| VS Code | Open the repo, choose **C#: Heliograph.ReferenceApp (standalone)** in Run and Debug, press play. The workbench opens in your browser. |
| VS Code, with Aspire | Choose **C#: Aspire (Full Stack)**. The Aspire dashboard opens, with links to the workbench and the MCP endpoint. |
| A terminal | `cd src/Heliograph.Workbench.Client && npm install && npm run build && cd ../..`, then `dotnet run --project reference/Heliograph.ReferenceApp`, and open `https://localhost:7296/heliograph`. |

The workbench client is built before the app because it ships inside `Heliograph.Workbench` as a
static file. The VS Code configurations do that for you.

## Try the workbench

The three benchmark letters are on the left: an acknowledgement, a decision letter with
conditions, and an annual statement with a table of contributions.

1. **Preview.** Open the decision letter, choose **Preview**, pick a sample recipient
   (`approved`, `refused`, `approved-dangerous-props`) and press **Preview**. You see the email
   as it arrives, the text message with how many messages it will be charged as, and the letter.
2. **Change the text message.** Choose **Text message**, change a word, and **Save**. Click a
   tag on the right to insert it. Misspell one, `((Applicant nam))`, and save: the problem shows
   at the top with where it is and what to do, and **Publish** is greyed out until you fix it.
3. **Start a document.** Choose **New correspondence**, give it a name and the dictionary
   `acknowledgement`, then on **Letter** press **Download a starter document**. Open it in your
   word processor: the title is set, and the second page lists every tag to copy.
4. **Upload it.** Write a line or two with a tag in it, delete the tag sheet, save, and drop the
   file on the page. Leave the tag sheet in, or misspell a tag, to see it refused.
5. **Fix it where you work.** With a problem showing, press **Download with comments**. Your own
   document comes back with a comment on each paragraph that has a problem. Fix it and drop it
   back on the page; Heliograph's comments come out by themselves.
6. **Change it where it is.** Press **Open in LibreOffice** (or **Open in Word** on the desktop).
   The document opens in your word processor; change it, save, and come back to the browser: the
   workbench shows what is now wrong, if anything. The first time, macOS asks whether LibreOffice
   may reach devices on the local network; allow it, because the reference app is on your
   machine.
7. **Publish.** With no problems left, **Publish** makes version 1. A change after that is a
   draft until you publish again.
8. **What your system sends.** Open `https://localhost:7296/outbox/decision-letter/refused`. This
   is the run-time call your system makes for a real person: it renders the published version
   and says which version it was.

## Connect an agent

The same operations are offered over MCP. With Claude Code:

```bash
claude mcp add --transport http heliograph http://localhost:5296/heliograph/correspondence-authoring/mcp
claude
```

Then ask, for example: *"Using the heliograph tools, write a text message for the decision
letter that also says when the event is, check it, and preview it for the refused sample. Do not
publish."* The agent reads the same guides you do, as resources; see
[Authoring with an agent](../authoring-with-an-agent/SKILL.md).

Plain HTTP is used because most MCP clients do not trust the local development certificate.
The endpoint is open only because this is a local app with made-up people.

## Host it yourself: the whole of it

This is the reference app's `Program.cs` without its demo lines:

```csharp
using Heliograph.Api;
using Heliograph.Documents;
using Heliograph.Mcp;
using Heliograph.Pdf.LibreOffice;
using Heliograph.Workbench;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHeliographInFolder("App_Data");                        // where correspondence and dictionaries live
builder.Services.AddSingleton<IPdfConverter, LibreOfficePdfConverter>();  // optional: a route to PDF
builder.Services.AddCorrespondenceAuthoringMcp();                         // optional: agents

var app = builder.Build();
app.MapStaticAssets();
app.MapCorrespondenceAuthoringApi(authorizationPolicy: "Authors");        // the REST API the workbench uses
app.MapCorrespondenceAuthoringMcp(authorizationPolicy: "Authors");        // the same, for agents
app.MapHeliographWorkbench(authorizationPolicy: "Authors");               // the author's screen, at /heliograph
app.Run();
```

Add the packages `Heliograph.Api`, `Heliograph.Workbench`, and the optional `Heliograph.Mcp` and
`Heliograph.Pdf.LibreOffice`. Register your own sign-in and an `Authors` policy; every endpoint
is closed until you do. Put dictionaries in `App_Data/dictionaries/<name>.json`, with sample
recipients in `App_Data/dictionaries/<name>.samples/<sample>.json`.

At run time your code renders a published version for a real person and sends it:

```csharp
var render = await library.RenderAsync("decision-letter", recipientData, new RenderSettings(culture));
// render.Version is what this person was sent. Keep it with your record of what was sent.
```

With more than one server, keep correspondence in a database: implement `ICorrespondenceStore`
and `IDictionarySource`. See [Using Heliograph](../using-heliograph/SKILL.md).
