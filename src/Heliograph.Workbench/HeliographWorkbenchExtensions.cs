using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Heliograph.Workbench;

/// <summary>Serves the workbench on a page of its own. The element itself is a static file any page can load.</summary>
public static class HeliographWorkbenchExtensions
{
    /// <summary>Where the workbench's script is served from, for a host that puts the element on its own page.</summary>
    public const string ScriptPath = "/_content/Heliograph.Workbench/heliograph-workbench.js";

    private const string StylePath = "/_content/Heliograph.Workbench/workbench-page.css";

    /// <summary>
    /// Maps a page that holds the workbench, talking to the Heliograph.Api at <paramref name="api"/>. Deny by default,
    /// like the API: the page is only a shell, but it is the author's screen and should sit behind the same door.
    /// The host serves static web assets (<c>MapStaticAssets</c> or <c>UseStaticFiles</c>).
    /// </summary>
    public static IEndpointConventionBuilder MapHeliographWorkbench(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/heliograph",
        string api = "/heliograph/api",
        string? authorizationPolicy = null,
        bool allowAnonymous = false)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (allowAnonymous && authorizationPolicy is not null)
        {
            throw new ArgumentException("authorizationPolicy and allowAnonymous: true contradict each other.", nameof(authorizationPolicy));
        }

        var page = Page(api);
        var endpoint = endpoints.MapGet(pattern, () => Results.Content(page, "text/html; charset=utf-8"));
        if (allowAnonymous)
        {
            endpoint.AllowAnonymous();
        }
        else if (authorizationPolicy is null)
        {
            endpoint.RequireAuthorization();
        }
        else
        {
            endpoint.RequireAuthorization(authorizationPolicy);
        }

        return endpoint;
    }

    private static string Page(string api) => $"""
        <!doctype html>
        <html lang="en-GB">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Heliograph workbench</title>
          <link rel="stylesheet" href="{StylePath}">
          <script type="module" src="{ScriptPath}"></script>
        </head>
        <body>
          <header class="page-header"><strong>Heliograph</strong><span>Correspondence workbench</span></header>
          <main><heliograph-workbench api="{WebUtility.HtmlEncode(api)}"></heliograph-workbench></main>
        </body>
        </html>
        """;
}
