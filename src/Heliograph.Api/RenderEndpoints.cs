using System.Globalization;
using System.Text.Json;
using Heliograph.Documents;
using Heliograph.Library;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Heliograph.Api;

/// <summary>Previews of a draft with sample data, and the run-time render of a published version.</summary>
internal static class RenderEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/correspondence/{key}/preview", async (string key, PreviewRequest request, CorrespondenceLibrary library, CancellationToken ct) =>
            await PreviewAsync(key, request, library, ct) is var (render, problem) && problem is not null ? problem : Results.Ok(PartsResponse.From(render!, false)));

        group.MapPost("/correspondence/{key}/preview/document", async (string key, PreviewRequest request, CorrespondenceLibrary library, CancellationToken ct) =>
            await PreviewAsync(key, request, library, ct) is var (render, problem) && problem is not null ? problem
            : render!.Rendered?.Docx is { } docx ? Results.File(docx, DocumentEndpoints.WordType, $"{key}-preview.docx")
            : Results.UnprocessableEntity(render.Diagnostics));

        group.MapPost("/correspondence/{key}/preview/pdf", async (
            string key, PreviewRequest request, CorrespondenceLibrary library, IServiceProvider services, CancellationToken ct) =>
        {
            if (services.GetService<IPdfConverter>() is not { } converter)
            {
                return Results.NotFound("This site has no route to PDF.");
            }

            var (render, problem) = await PreviewAsync(key, request, library, ct);
            if (problem is not null)
            {
                return problem;
            }

            return render!.Rendered?.Docx is { } docx
                ? Results.File(await converter.ConvertAsync(docx, ct), "application/pdf")
                : Results.UnprocessableEntity(render.Diagnostics);
        });

        // The run-time call, for a host that renders from another process. It takes a real recipient's data, so it
        // carries the same policy as everything else here, and nothing about the data is logged.
        group.MapPost("/correspondence/{key}/render", async (string key, RenderRequest request, CorrespondenceLibrary library, CancellationToken ct) =>
            Culture(request.Culture) is not { } culture ? Results.BadRequest("Unknown culture.")
            : await library.RenderAsync(key, request.Data, new RenderSettings(culture), request.Version, ct) is var render && render.Rendered is null
                ? Results.UnprocessableEntity(PartsResponse.From(render, false))
                : Results.Ok(PartsResponse.From(render, request.IncludeDocument)));
    }

    private static async Task<(LibraryRender? Render, IResult? Problem)> PreviewAsync(
        string key, PreviewRequest request, CorrespondenceLibrary library, CancellationToken ct)
    {
        if (Culture(request.Culture) is not { } culture)
        {
            return (null, Results.BadRequest("Unknown culture. Use a name such as \"en-GB\"."));
        }

        if (await library.ReadDraftAsync(key, ct) is not { } draft)
        {
            return (null, Results.NotFound());
        }

        var json = request.Data is { ValueKind: JsonValueKind.Object } data ? data.GetRawText()
            : request.Sample is { } sample ? await library.Dictionaries.SampleAsync(draft.Content.Dictionary, sample, ct)
            : null;
        if (json is null)
        {
            return (null, Results.BadRequest("Give data as a JSON object, or the name of one of the dictionary's samples."));
        }

        using var parsed = JsonDocument.Parse(json);
        return (await library.PreviewAsync(key, parsed.RootElement.Clone(), new RenderSettings(culture), ct), null);
    }

    private static CultureInfo? Culture(string? name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name ?? "en-GB", predefinedOnly: true);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
