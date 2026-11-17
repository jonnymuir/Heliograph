using Heliograph.Library;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Heliograph.Api;

/// <summary>Reading dictionaries and their sample recipients.</summary>
internal static class DictionaryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/dictionaries", async (CorrespondenceLibrary library, CancellationToken ct) =>
            Results.Ok(await library.Dictionaries.ListAsync(ct)));

        group.MapGet("/dictionaries/{name}", async (string name, CorrespondenceLibrary library, CancellationToken ct) =>
            await library.Dictionaries.SheetAsync(name, ct) is { } sheet ? Results.Ok(sheet) : Results.NotFound());

        group.MapGet("/dictionaries/{name}/samples", async (string name, CorrespondenceLibrary library, CancellationToken ct) =>
            Results.Ok(await library.Dictionaries.SamplesAsync(name, ct)));

        group.MapGet("/dictionaries/{name}/samples/{sample}", async (string name, string sample, CorrespondenceLibrary library, CancellationToken ct) =>
            await library.Dictionaries.SampleAsync(name, sample, ct) is { } json ? Results.Content(json, "application/json") : Results.NotFound());
    }
}
