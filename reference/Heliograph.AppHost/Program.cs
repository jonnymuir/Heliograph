// The reference app's orchestrator, as Wayfinder's is: one command starts it, and the Aspire dashboard links
// straight to the workbench and the MCP endpoint. Deliberately small: the reference app keeps everything in a folder,
// so there is nothing else to start.
#pragma warning disable CS0618 // ResourceUrlAnnotation.DisplayOrder is marked obsolete but still orders the links.
var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Heliograph_ReferenceApp>("referenceapp", launchProfileName: "https")
    .WithUrls(context =>
    {
        var https = context.Urls.Select(u => u.Url).FirstOrDefault(u => u?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true);
        var http = context.Urls.Select(u => u.Url).FirstOrDefault(u => u?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true);
        if (https is not null)
        {
            context.Urls.Add(new ResourceUrlAnnotation { Url = Root(https) + "/heliograph", DisplayText = "Heliograph workbench", DisplayOrder = 1 });
        }

        // Plain HTTP, for local MCP clients, most of which do not trust the development certificate.
        if (http is not null)
        {
            context.Urls.Add(new ResourceUrlAnnotation { Url = Root(http) + "/heliograph/correspondence-authoring/mcp", DisplayText = "Correspondence authoring MCP (HTTP)", DisplayOrder = 2 });
        }
    });

builder.Build().Run();

static string Root(string url) => new Uri(url).GetLeftPart(UriPartial.Authority);
