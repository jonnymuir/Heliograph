using Heliograph.Library;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace Heliograph.Api;

/// <summary>The document part as a file: upload it, download it, start from a starter, get it back with comments.</summary>
internal static class DocumentEndpoints
{
    public const string WordType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    // The core refuses anything that unpacks to more; the upload is capped at the same size before it is read.
    private const long MaxUpload = 64L * 1024 * 1024;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/correspondence/{key}/document", async (string key, CorrespondenceLibrary library, CancellationToken ct) =>
            await library.ReadDraftAsync(key, ct) is { Content.Document: { } document } draft
                ? Results.File(document, WordType, FileName(draft.Content.Name))
                : Results.NotFound());

        group.MapPut("/correspondence/{key}/document", UploadAsync);

        group.MapGet("/correspondence/{key}/document/starter", async (string key, CorrespondenceLibrary library, CancellationToken ct) =>
            await library.StarterDocumentAsync(key, ct) is { } starter ? Results.File(starter, WordType, $"{key}-starter.docx") : Results.NotFound());

        group.MapPost("/correspondence/{key}/document/edit-link", async (string key, HttpRequest request, EditLinks links, CorrespondenceLibrary library, CancellationToken ct) =>
        {
            if (await library.ReadDraftAsync(key, ct) is not { Content.Document: not null } draft)
            {
                return Results.NotFound();
            }

            var url = $"{request.Scheme}://{request.Host}{WebDavEndpoints.Prefix}/{Uri.EscapeDataString(links.Issue(key))}/{Uri.EscapeDataString(FileName(draft.Content.Name))}";
            return Results.Ok(new EditLinkResponse(url, "ms-word:ofe|u|" + url, "vnd.libreoffice.command:ofv|u|" + url, DateTimeOffset.UtcNow.Add(EditLinks.Lifetime)));
        });

        group.MapGet("/correspondence/{key}/document/annotated", async (string key, CorrespondenceLibrary library, CancellationToken ct) =>
            await library.AnnotatedDocumentAsync(key, ct) is { } annotated ? Results.File(annotated, WordType, $"{key}-with-comments.docx") : Results.NotFound());
    }

    /// <summary>The request body is the .docx itself. It is saved even when it has problems, which come back with it.</summary>
    private static async Task<IResult> UploadAsync(string key, int expectedRevision, HttpRequest request, CorrespondenceLibrary library, CancellationToken ct)
    {
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = MaxUpload;
        }

        if (await library.ReadDraftAsync(key, ct) is not { } draft)
        {
            return Results.NotFound();
        }

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, ct);
        var content = new CorrespondenceContent
        {
            Name = draft.Content.Name, Dictionary = draft.Content.Dictionary, Email = draft.Content.Email, Sms = draft.Content.Sms,
            Document = buffer.Length == 0 ? null : buffer.ToArray(),
        };
        return DraftEndpoints.Saved(await library.SaveDraftAsync(key, content, expectedRevision, ct));
    }

    /// <summary>A file name made from what people call the correspondence, keeping only safe characters.</summary>
    internal static string FileName(string name)
    {
        var safe = new string([.. name.Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' ? c : '-')]).Trim();
        return (safe.Length == 0 ? "correspondence" : safe) + ".docx";
    }
}
