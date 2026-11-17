using Heliograph.Library;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Heliograph.Api;

/// <summary>Keeping dictionaries: read the JSON, save it and see what it breaks, keep sample recipients.</summary>
internal static class DictionaryEditingEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/dictionaries/{name}/definition", async (string name, CorrespondenceLibrary library, CancellationToken ct) =>
            await library.Dictionaries.ReadAsync(name, ct) is { } json ? Results.Content(json, "application/json") : Results.NotFound());

        group.MapGet("/dictionary-settings", async (CorrespondenceLibrary library, CancellationToken ct) =>
            Results.Ok(new DictionarySettingsResponse(library.Dictionaries.Editing.CanEdit, await library.Dictionaries.Editing.StartingPointsAsync(ct))));

        group.MapPut("/dictionaries/{name}", async (string name, HttpRequest request, CorrespondenceLibrary library, CancellationToken ct) =>
        {
            var save = await library.Dictionaries.Editing.SaveAsync(name, await BodyAsync(request, ct), ct);
            return save.Saved
                ? Results.Ok(new DictionarySaveResponse(true, [], await library.CheckDictionaryAsync(name, ct)))
                : Refused(save);
        });

        group.MapPut("/dictionaries/{name}/samples/{sample}", async (string name, string sample, HttpRequest request, CorrespondenceLibrary library, CancellationToken ct) =>
            await library.Dictionaries.Editing.SaveSampleAsync(name, sample, await BodyAsync(request, ct), ct) is var save && save.Saved ? Results.NoContent() : Refused(save));

        group.MapDelete("/dictionaries/{name}/samples/{sample}", async (string name, string sample, CorrespondenceLibrary library, CancellationToken ct) =>
            await library.Dictionaries.Editing.DeleteSampleAsync(name, sample, ct) is var save && save.Saved ? Results.NoContent() : Refused(save));
    }

    // A dictionary is small; anything larger than this is not one.
    private const int MaxDictionary = 1024 * 1024;

    private static async Task<string> BodyAsync(HttpRequest request, CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var buffer = new char[MaxDictionary + 1];
        var read = await reader.ReadBlockAsync(buffer, ct);
        return read > MaxDictionary ? "" : new string(buffer, 0, read);
    }

    private static IResult Refused(DictionarySaveResult save) =>
        save.Problems.Any(p => p.Code == DiagnosticCodes.DictionariesReadOnly) ? Results.Json(save, statusCode: StatusCodes.Status405MethodNotAllowed) : Results.BadRequest(save);
}

/// <summary>Whether dictionaries can be changed here, and the ready-made ones a new dictionary can start from.</summary>
public sealed record DictionarySettingsResponse(bool CanEdit, IReadOnlyList<DictionaryStartingPoint> StartingPoints);

/// <summary>A dictionary saved, with what the change now means for every draft that uses it.</summary>
public sealed record DictionarySaveResponse(bool Saved, IReadOnlyList<Diagnostic> Problems, IReadOnlyList<DictionaryUse> Uses);
