# Skills

Each folder here is one guide, written once and read three ways.

| Guide | For |
|---|---|
| [writing-a-template](writing-a-template/SKILL.md) | Whoever writes the letters |
| [authoring-with-an-agent](authoring-with-an-agent/SKILL.md) | A content designer working with an AI agent, the agent, and the developer who connects it |
| [using-heliograph](using-heliograph/SKILL.md) | A developer hosting Heliograph |
| [diagnostics](diagnostics/SKILL.md) | Anyone holding a diagnostic code |

## Why they are called skills

- **A person** reads the file on GitHub. It is ordinary Markdown.
- **An agent's tooling** loads the folder as a skill. The block at the top of each file, with a
  `name` and a `description`, is the form Claude and other agents expect. The description says
  what the guide covers and when to reach for it.
- **An agent connected over MCP** reads the same file as a resource,
  `heliograph-skills://<folder>`. `Heliograph.Mcp` embeds this folder when it is built.

So there is no agent documentation to keep in step with the human documentation. They are the
same files.

## Adding or changing one

1. A new guide is a new folder with a `SKILL.md`. Start it with:

   ```text
   ---
   name: <the folder's name>
   description: <what it covers>. Use when <the moment to read it>.
   ---
   ```

   Keep the description on one line, with no colon followed by a space.
2. Write for a person first. An agent reads plain words as well as a person does.
3. Link to another guide with a relative link, `../diagnostics/SKILL.md`. Link to anything else
   in the repo the same way. An agent over MCP is told that such a link to a guide is the
   resource of the same name.
4. Add it to the table above. Nothing else needs registering: the tests fail if a folder here is
   not served to agents, word for word.
