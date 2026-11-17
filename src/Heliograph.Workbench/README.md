# Heliograph.Workbench

The author's screen for [Heliograph](https://github.com/jonnymuir/Heliograph), as one web
component: write the email and the text, bring the document in from a word processor (a starter
document to begin from, drop to upload, the file back with a comment at each problem), see what
is wrong and where, preview every part with a sample recipient, and publish.

```csharp
app.MapStaticAssets();
app.MapCorrespondenceAuthoringApi();          // Heliograph.Api, which the workbench talks to
app.MapHeliographWorkbench();                 // the page, at /heliograph
```

To put it on a page of your own, load the script and give it the API's address:

```html
<script type="module" src="/_content/Heliograph.Workbench/heliograph-workbench.js"></script>
<heliograph-workbench api="/heliograph/api"></heliograph-workbench>
```

A host with its own endpoints (the Umbraco backoffice) sets the element's `source` property to
calls of its own instead. Colours follow `--heliograph-*` custom properties.
