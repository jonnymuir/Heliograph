using Heliograph.Api;
using Heliograph.Documents;
using Heliograph.Mcp;
using Heliograph.Pdf.LibreOffice;
using Heliograph.ReferenceApp;
using Heliograph.Workbench;

// Everything a host needs to offer Heliograph to its authors. The lines marked "demo" are this app's own.
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults(); // demo: Aspire's telemetry and health checks

var data = builder.Configuration["Heliograph:Folder"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");
builder.Services.AddHeliographInFolder(data);                              // where correspondence and dictionaries live
builder.Services.AddSingleton<IPdfConverter, LibreOfficePdfConverter>();  // optional: a route to PDF
builder.Services.AddCorrespondenceAuthoringMcp();                         // optional: agents, over MCP

var app = builder.Build();
await ReferenceSeed.RunAsync(app.Services, data); // demo: the benchmark letters, on first start

app.MapStaticAssets();

// Open to anyone, on purpose and only here: a local reference app with made-up people, as Wayfinder's is. A real
// host passes authorizationPolicy: "..." to each of these and leaves allowAnonymous out.
app.MapCorrespondenceAuthoringApi(allowAnonymous: true);
app.MapCorrespondenceAuthoringMcp(allowAnonymous: true);
app.MapHeliographWorkbench(allowAnonymous: true);

app.MapOutbox(); // demo: what your system does at run time, rendering a published version for one person
app.MapGet("/", () => Results.Redirect("/heliograph")).AllowAnonymous();
app.MapDefaultEndpoints();

app.Run();

/// <summary>The reference app's entry point, named so its tests can start it.</summary>
public partial class Program;
