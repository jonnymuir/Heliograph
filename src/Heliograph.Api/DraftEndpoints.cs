using Heliograph.Library;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Heliograph.Api;

/// <summary>Listing, reading, saving, checking and publishing drafts.</summary>
internal static class DraftEndpoints
{
    private static readonly string[] Parts = ["document", "email", "sms"];

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/correspondence", async (CorrespondenceLibrary library, CancellationToken ct) => Results.Ok(await library.ListAsync(ct)));

        group.MapGet("/correspondence/{key}", async (string key, CorrespondenceLibrary library, CancellationToken ct) =>
            await library.ReadDraftAsync(key, ct) is { } draft
                ? Results.Ok(new DraftResponse(draft.Key, draft.Revision, draft.Content.Name, draft.Content.Dictionary, draft.Content.Email,
                    draft.Content.Sms, draft.Content.Document is not null, (await library.ReadVersionAsync(key, null, ct))?.Version))
                : Results.NotFound());

        group.MapPut("/correspondence/{key}", SaveAsync);

        group.MapPost("/correspondence/{key}/check", async (string key, CorrespondenceLibrary library, CancellationToken ct) =>
            await library.CheckAsync(key, ct) is var check && check.Diagnostics.Any(d => d.Code == DiagnosticCodes.CorrespondenceNotFound)
                ? Results.NotFound(check)
                : Results.Ok(check));

        group.MapGet("/dictionaries/{name}/uses", async (string name, CorrespondenceLibrary library, CancellationToken ct) =>
            Results.Ok(await library.CheckDictionaryAsync(name, ct)));

        group.MapPost("/correspondence/{key}/publish", async (string key, CorrespondenceLibrary library, CancellationToken ct) =>
            await library.PublishAsync(key, ct) is var publish && publish.Published
                ? Results.Ok(publish)
                : publish.Diagnostics.Any(d => d.Code == DiagnosticCodes.CorrespondenceNotFound) ? Results.NotFound(publish) : Results.UnprocessableEntity(publish));
    }

    private static async Task<IResult> SaveAsync(string key, SaveDraftRequest request, CorrespondenceLibrary library, CancellationToken ct)
    {
        var remove = request.Remove ?? [];
        if (remove.Except(Parts, StringComparer.Ordinal).FirstOrDefault() is { } unknown)
        {
            return Results.BadRequest($"\"{unknown}\" is not a part. The parts are \"document\", \"email\" and \"sms\".");
        }

        var existing = (await library.ReadDraftAsync(key, ct))?.Content;
        if (existing is null && (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Dictionary)))
        {
            return Results.BadRequest("A new correspondence needs a name and a dictionary.");
        }

        var content = new CorrespondenceContent
        {
            Name = request.Name ?? existing!.Name,
            Dictionary = request.Dictionary ?? existing!.Dictionary,
            Document = Kept("document", remove, null, existing?.Document),
            Email = Kept("email", remove, request.Email, existing?.Email),
            Sms = Kept("sms", remove, request.Sms, existing?.Sms),
        };
        return Saved(await library.SaveDraftAsync(key, content, request.ExpectedRevision, ct));
    }

    /// <summary>A part as it will be saved: removed if asked, replaced if given, otherwise as it was.</summary>
    private static T? Kept<T>(string part, IReadOnlyList<string> remove, T? given, T? was)
        where T : class =>
        remove.Contains(part) ? null : given ?? was;

    /// <summary>A save that was made answers 200 with its problems; one that someone else got in ahead of answers 409.</summary>
    internal static IResult Saved(SaveResult save) => save.Saved
        ? Results.Ok(save)
        : save.Diagnostics.Any(d => d.Code == DiagnosticCodes.ChangedElsewhere) ? Results.Conflict(save) : Results.BadRequest(save);
}
