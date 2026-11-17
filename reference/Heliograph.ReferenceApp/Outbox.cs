using System.Globalization;
using System.Net;
using Heliograph.Library;

namespace Heliograph.ReferenceApp;

/// <summary>
/// Demo only: what a host does at run time, shown on a page. It renders the published version of a correspondence
/// for one of the dictionary's sample recipients, the way your system would for a real person just before sending
/// it, and shows what would be sent. Nothing is sent: delivery is always the host's own code.
/// </summary>
internal static class Outbox
{
    public static void MapOutbox(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/outbox/{key}/{sample}", ShowAsync).AllowAnonymous(); // demo only, as Program.cs explains
    }

    private static async Task<IResult> ShowAsync(string key, string sample, CorrespondenceLibrary library, CancellationToken ct)
    {
        if (await library.ReadVersionAsync(key, null, ct) is not { } published
            || await library.Dictionaries.SampleAsync(published.Content.Dictionary, sample, ct) is not { } json)
        {
            return Results.NotFound("Publish the correspondence first, and choose one of its dictionary's samples.");
        }

        // The run-time call. Your system passes the recipient's real data and records the version it was given.
        using var data = System.Text.Json.JsonDocument.Parse(json);
        var render = await library.RenderAsync(key, data.RootElement, new RenderSettings(CultureInfo.GetCultureInfo("en-GB")), cancellationToken: ct);
        return Results.Content(Page(key, sample, render), "text/html; charset=utf-8");
    }

    private static string Page(string key, string sample, LibraryRender render)
    {
        Func<string?, string?> e = WebUtility.HtmlEncode;
        var body = render.Rendered is not { } parts
            ? $"<p>Not rendered: {e(string.Join(" ", render.Diagnostics.Select(d => d.Message)))}</p>"
            : $"""
               <p>Version <strong>{render.Version}</strong> was rendered. Your system records that with what it sends.</p>
               {(parts.Email is { } email ? $"<h2>Email</h2><p>Subject: <strong>{e(email.Subject!)}</strong></p><pre>{e(email.Text!)}</pre>" : "")}
               {(parts.Sms is { } sms ? $"<h2>Text message</h2><pre>{e(sms.Text!)}</pre><p>{sms.Segments} message(s)</p>" : "")}
               {(parts.Docx is not null ? "<h2>Letter</h2><p>A Word document was rendered, ready to print or convert to PDF.</p>" : "")}
               """;
        return $"""
            <!doctype html><html lang="en-GB"><head><meta charset="utf-8"><title>Outbox</title>
            <link rel="stylesheet" href="/_content/Heliograph.Workbench/workbench-page.css"></head>
            <body><header class="page-header"><strong>Outbox</strong><span>What would be sent: {e(key)} to sample “{e(sample)}”</span></header>
            <main>{body}<p><a href="/heliograph?correspondence={Uri.EscapeDataString(key)}">Back to the workbench</a></p></main></body></html>
            """;
    }
}
