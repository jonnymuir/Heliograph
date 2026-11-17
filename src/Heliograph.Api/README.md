# Heliograph.Api

Correspondence authoring over REST for a site that hosts
[Heliograph](https://github.com/jonnymuir/Heliograph). It is what the workbench talks to, and it
offers the same operations as the MCP server.

```csharp
builder.Services.AddHeliographInFolder("data");          // correspondence and dictionaries as files
var app = builder.Build();
app.MapCorrespondenceAuthoringApi();                      // /heliograph/api, signed-in callers only
```

| Route | What it does |
|---|---|
| `GET /dictionaries`, `GET /dictionaries/{name}` | The dictionaries, and one laid out with a tag for each field |
| `GET /dictionaries/{name}/samples[/{sample}]` | Made-up recipients for previews |
| `GET /dictionaries/{name}/uses` | What a change to a dictionary breaks |
| `GET /correspondence`, `GET /correspondence/{key}` | The list, and one draft |
| `PUT /correspondence/{key}` | Save the name, dictionary, email or text. 409 if someone else saved first |
| `PUT /correspondence/{key}/document?expectedRevision=n` | Upload the `.docx` as the request body |
| `GET /correspondence/{key}/document[/starter\|/annotated]` | The document, a starter document, or the document with a comment at each problem |
| `POST /correspondence/{key}/check`, `/publish` | Check the draft; publish it as the next version |
| `POST /correspondence/{key}/preview[/document\|/pdf]` | Render the draft with a sample |
| `POST /correspondence/{key}/render` | Render a published version for a real recipient |

Every route is closed unless the caller is signed in. Pass `authorizationPolicy:` to narrow it.
