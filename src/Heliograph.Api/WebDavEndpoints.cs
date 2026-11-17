using System.Globalization;
using System.Security;
using Heliograph.Library;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace Heliograph.Api;

/// <summary>
/// The few WebDAV methods Word on the desktop and LibreOffice use to open a document from an address and save it back:
/// OPTIONS, HEAD, GET, PUT, PROPFIND, PROPPATCH, LOCK and UNLOCK, on one document named by a signed edit-link token.
/// A save is a new revision of the draft, checked as any upload is.
/// </summary>
internal static class WebDavEndpoints
{
    public const string Prefix = "/heliograph/webdav";

    private const long MaxUpload = 64L * 1024 * 1024;

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // Anonymous on purpose, and only these routes: a word processor cannot send the author's sign-in, so the signed,
        // expiring token in the address is what lets it in. It opens one draft document and nothing else.
        var dav = endpoints.MapGroup(Prefix + "/{token}/{name}").AllowAnonymous();
        dav.MapMethods("", ["OPTIONS"], (HttpResponse response) => Options(response));
        dav.MapMethods("", ["GET", "HEAD"], GetAsync);
        dav.MapMethods("", ["PUT"], PutAsync);
        dav.MapMethods("", ["PROPFIND"], PropFindAsync);
        dav.MapMethods("", ["PROPPATCH"], (HttpRequest request) => MultiStatus(request, "<D:propstat><D:prop/><D:status>HTTP/1.1 200 OK</D:status></D:propstat>"));
        dav.MapMethods("", ["LOCK"], Lock);
        dav.MapMethods("", ["UNLOCK"], () => Results.NoContent());

        // Some clients ask about the folder before the file.
        endpoints.MapMethods(Prefix + "/{token}", ["OPTIONS"], (HttpResponse response) => Options(response)).AllowAnonymous();
    }

    private static IResult Options(HttpResponse response)
    {
        response.Headers["DAV"] = "1, 2";
        response.Headers["MS-Author-Via"] = "DAV";
        response.Headers.Allow = "OPTIONS, GET, HEAD, PUT, PROPFIND, PROPPATCH, LOCK, UNLOCK";
        return Results.Ok();
    }

    private static async Task<IResult> GetAsync(string token, EditLinks links, CorrespondenceLibrary library, CancellationToken ct) =>
        await DocumentAsync(token, links, library, ct) is { } document
            ? Results.File(document, DocumentEndpoints.WordType, lastModified: null)
            : Results.NotFound();

    private static async Task<IResult> PutAsync(string token, HttpRequest request, EditLinks links, CorrespondenceLibrary library, CancellationToken ct)
    {
        if (links.Read(token) is not { } key || await library.ReadDraftAsync(key, ct) is not { } draft)
        {
            return Results.NotFound();
        }

        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = MaxUpload;
        }

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, ct);
        if (buffer.Length == 0)
        {
            // Some editors make an empty file before writing it. There is nothing to keep yet.
            return Results.NoContent();
        }

        var content = new CorrespondenceContent
        {
            Name = draft.Content.Name, Dictionary = draft.Content.Dictionary, Email = draft.Content.Email, Sms = draft.Content.Sms,
            Document = buffer.ToArray(),
        };

        // The editor holds the document while it is open, so its save is taken as the next revision of what is there now.
        var save = await library.SaveDraftAsync(key, content, draft.Revision, ct);
        return save.Saved ? Results.NoContent() : Results.StatusCode(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> PropFindAsync(
        string token, string name, HttpRequest request, EditLinks links, CorrespondenceLibrary library, CancellationToken ct)
    {
        if (await DocumentAsync(token, links, library, ct) is not { } document)
        {
            return Results.NotFound();
        }

        var props =
            "<D:propstat><D:prop>" +
            $"<D:displayname>{SecurityElement.Escape(name)}</D:displayname>" +
            "<D:resourcetype/>" +
            $"<D:getcontenttype>{DocumentEndpoints.WordType}</D:getcontenttype>" +
            $"<D:getcontentlength>{document.Length.ToString(CultureInfo.InvariantCulture)}</D:getcontentlength>" +
            $"<D:getlastmodified>{DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture)}</D:getlastmodified>" +
            "<D:supportedlock><D:lockentry><D:lockscope><D:exclusive/></D:lockscope><D:locktype><D:write/></D:locktype></D:lockentry></D:supportedlock>" +
            "</D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat>";
        return MultiStatus(request, props);
    }

    /// <summary>
    /// Grants the lock an editor asks for. Heliograph does not hold locks itself: two people saving at once is caught by
    /// the draft's revision, as it is everywhere else.
    /// </summary>
    private static IResult Lock(string name, HttpResponse response)
    {
        var token = "opaquelocktoken:" + Guid.NewGuid().ToString("D");
        response.Headers["Lock-Token"] = $"<{token}>";
        var body =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:prop xmlns:D=\"DAV:\"><D:lockdiscovery><D:activelock>" +
            "<D:locktype><D:write/></D:locktype><D:lockscope><D:exclusive/></D:lockscope><D:depth>0</D:depth>" +
            "<D:timeout>Second-3600</D:timeout>" +
            $"<D:locktoken><D:href>{token}</D:href></D:locktoken>" +
            "</D:activelock></D:lockdiscovery></D:prop>";
        return Results.Content(body, "application/xml; charset=utf-8", statusCode: StatusCodes.Status200OK);
    }

    /// <summary>A WebDAV answer about the one document, addressed by the path it was asked for, as clients expect.</summary>
    private static IResult MultiStatus(HttpRequest request, string propstat) => Results.Content(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:multistatus xmlns:D=\"DAV:\"><D:response>" +
        $"<D:href>{SecurityElement.Escape(request.PathBase + request.Path.ToUriComponent())}</D:href>{propstat}</D:response></D:multistatus>",
        "application/xml; charset=utf-8", statusCode: StatusCodes.Status207MultiStatus);

    private static async Task<byte[]?> DocumentAsync(string token, EditLinks links, CorrespondenceLibrary library, CancellationToken ct) =>
        links.Read(token) is { } key && await library.ReadDraftAsync(key, ct) is { Content.Document: { } document } ? document : null;
}
