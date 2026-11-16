---
name: authoring-with-an-agent
description: How an AI agent writes, checks, previews and publishes Heliograph correspondence over MCP, and how a developer adds the MCP endpoint to a host. Covers the tools, the resources, the prompts, what an agent cannot do, and how the endpoint is protected. Use when connecting an agent to Heliograph or working as one.
---

# Authoring with an agent

For a content designer who wants an AI agent to help with correspondence, for the agent, and for
the developer who turns it on. An agent connects over [MCP](https://modelcontextprotocol.io), an
open standard, so any agent that speaks it will do.

An agent gets the operations a person gets, and the same answers. It reads the same dictionary,
its drafts are checked by the same compiler, and it is given the same guides you are reading.

## What an agent can do

| Tool | What it does |
|---|---|
| `list_correspondence` | Every correspondence: key, name, dictionary, newest published version |
| `list_dictionaries` | The names of the dictionaries |
| `read_dictionary` | Every field in a dictionary, with the tag to write for it |
| `read_correspondence` | The draft: the email and the text message as text, and whether there is a document |
| `save_correspondence` | Saves the draft and returns everything wrong with it |
| `check_correspondence` | Compiles the draft again and returns everything wrong with it |
| `check_dictionary` | What a change to a dictionary breaks, across every correspondence that uses it |
| `preview_correspondence` | Renders the draft for one made-up recipient |
| `publish_correspondence` | Publishes the draft as the next numbered version |

Every problem a tool returns is a diagnostic from [Diagnostics](../diagnostics/SKILL.md): a code,
plain words, where it is, and how to fix it.

## How the work goes

1. **Read the dictionary.** A template may use only the tags it lists.
2. **Save a draft.** A draft is saved even when it has errors, so nothing is lost. The save
   returns the problems.
3. **Fix and save again**, passing the revision the last save returned. Parts not passed are
   kept. If someone else saved in between, nothing is saved and the agent is told to read again.
4. **Preview** with made-up data, once for each outcome that matters.
5. **Publish when the person asks.** Publishing makes a numbered version that never changes.
   Recipients are sent the published version, so a draft can be worked on safely.

[Writing a template](../writing-a-template/SKILL.md) has the rules for what goes in each part.

## What an agent cannot do

- **Write the document.** A document is a Word file laid out in a word processor. An agent can
  upload one it has been given, say what is wrong with one, and say what the letter should
  contain. It writes the email and the text message itself.
- **Send anything, or render for a real recipient.** Rendering for real people is the host's
  job at run time. Their data does not pass through an authoring conversation.
- **Add a field.** The dictionary belongs to whoever knows the data. An agent says what is
  missing.
- **Unpublish.** A version is permanent. A mistake is corrected by publishing another.

## Starting points

The server offers three prompts. In Claude Code they appear as slash commands.

| Prompt | For |
|---|---|
| `write-a-correspondence` | A new correspondence from a plain-language brief |
| `fix-a-correspondence` | Working through a draft's problems |
| `review-before-publishing` | Reading a draft as its recipients would, for every outcome |

## The guides are the agent's skills

Every folder in [docs/skills](../README.md) is one guide, written once:

- a person reads it on GitHub,
- an agent's own tooling can load the folder as a skill,
- an agent connected over MCP reads it as the resource `heliograph-skills://<folder>`.

A test fails if a guide is not served, or is served differently from the file.

## For the developer: turning it on

Add the `Heliograph.Mcp` package. Register your store and your dictionaries, then:

```csharp
builder.Services.AddSingleton<ICorrespondenceStore, YourStore>();
builder.Services.AddSingleton<IDictionarySource, YourDictionaries>();
builder.Services.AddCorrespondenceAuthoringMcp(
    instructions: "The \"licensing\" dictionary belongs to the event licence service.");

app.MapCorrespondenceAuthoringMcp();
```

- **The endpoint is closed by default.** It requires a signed-in caller. Pass
  `authorizationPolicy:` to narrow it to the people who may publish. Opening it takes
  `allowAnonymous: true`, written out, and you should not.
- **Sign-in is the host's.** Heliograph.Mcp has none of its own. In Umbraco,
  [Heliograph.Umbraco](https://github.com/jonnymuir/Heliograph.Umbraco) lets an agent sign in as
  the backoffice user who authorises it.
- **`instructions`** is for what is true of your site only. It is added to what every agent is
  told when it connects.
- **The store and the dictionary source must be safe to use from many requests at once.** The
  library that sits on them is registered once for the whole site.
