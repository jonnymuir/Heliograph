# Heliograph.Mcp

Correspondence authoring for AI agents. One call adds an
[MCP](https://modelcontextprotocol.io) endpoint to a site that hosts
[Heliograph](https://github.com/jonnymuir/Heliograph), so an agent does the job a content
designer does, through the same operations, and gets the same answers.

```csharp
builder.Services.AddSingleton<ICorrespondenceStore, YourStore>();
builder.Services.AddSingleton<IDictionarySource, YourDictionaries>();
builder.Services.AddCorrespondenceAuthoringMcp();

app.MapCorrespondenceAuthoringMcp();   // /heliograph/correspondence-authoring/mcp, signed-in callers only
```

- **Tools**: list and read correspondence and dictionaries, save a draft, check it, preview it
  with sample data, publish it.
- **Resources**: every guide in the repo's `docs/skills`, word for word. The file a person reads
  is the file the agent is given.
- **Prompts**: write a correspondence, fix one, review one before publishing.

The endpoint is closed unless the caller is signed in. Pass a policy name to narrow it, as you
would for any endpoint that can overwrite and publish.

The full guide is
[Authoring with an agent](https://github.com/jonnymuir/Heliograph/blob/main/docs/skills/authoring-with-an-agent/SKILL.md).
