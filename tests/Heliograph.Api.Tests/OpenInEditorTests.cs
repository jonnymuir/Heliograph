using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Heliograph.Api.Tests;

/// <summary>
/// A word processor opens a draft's document from a signed link and saves it straight back, through the few WebDAV
/// methods Word on the desktop and LibreOffice use. The link opens that one document and nothing else.
/// </summary>
public class OpenInEditorTests
{
    private const string Members = """{ "fields": [ { "name": "Member name", "key": "memberName", "type": "text" } ] }""";

    [Fact]
    public async Task An_author_gets_links_that_open_the_document_in_Word_and_in_LibreOffice()
    {
        await using var host = await StartWithDocumentAsync();
        using var http = host.Http();

        var link = await LinkAsync(http);

        link.GetProperty("word").GetString().Should().StartWith("ms-word:ofe|u|http");
        link.GetProperty("libreOffice").GetString().Should().StartWith("vnd.libreoffice.command:ofv|u|http");
        link.GetProperty("url").GetString().Should().Contain("/heliograph/webdav/").And.EndWith(".docx");
    }

    [Fact]
    public async Task The_editor_reads_the_document_and_what_it_needs_to_know_about_it()
    {
        await using var host = await StartWithDocumentAsync();
        using var http = host.Http();
        var url = Path(await LinkAsync(http));
        using var editor = host.Http(signedIn: false);

        using var options = await editor.SendAsync(new HttpRequestMessage(HttpMethod.Options, url));
        using var propfind = await editor.SendAsync(new HttpRequestMessage(new HttpMethod("PROPFIND"), url));
        using var get = await editor.GetAsync(url);
        using var locked = await editor.SendAsync(new HttpRequestMessage(new HttpMethod("LOCK"), url));

        options.Headers.GetValues("DAV").Should().ContainSingle().Which.Should().Contain("2");
        propfind.StatusCode.Should().Be((HttpStatusCode)207);
        (await propfind.Content.ReadAsStringAsync()).Should().Contain("<D:getcontentlength>");
        (await get.Content.ReadAsByteArrayAsync()).Take(2).Should().Equal((byte)'P', (byte)'K');
        locked.Headers.GetValues("Lock-Token").Should().ContainSingle();
    }

    [Fact]
    public async Task What_the_editor_saves_becomes_the_next_revision_of_the_draft_and_is_checked()
    {
        await using var host = await StartWithDocumentAsync();
        using var http = host.Http();
        var url = Path(await LinkAsync(http));
        using var editor = host.Http(signedIn: false);
        var starter = await http.GetByteArrayAsync("correspondence/reminder/document/starter");

        using var saved = await editor.PutAsync(url, new ByteArrayContent(starter));

        saved.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var draft = await http.GetFromJsonAsync<JsonElement>("correspondence/reminder");
        draft.GetProperty("revision").GetInt32().Should().Be(3);
        var check = await (await http.PostAsJsonAsync("correspondence/reminder/check", new { })).Content.ReadFromJsonAsync<JsonElement>();
        check.GetProperty("diagnostics")[0].GetProperty("code").GetString().Should().Be(DiagnosticCodes.TagSheetLeftIn);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("PROPFIND")]
    public async Task A_made_up_or_altered_token_opens_nothing(string method)
    {
        await using var host = await StartWithDocumentAsync();
        using var http = host.Http();
        var url = Path(await LinkAsync(http));
        var altered = url.Replace("/webdav/C", "/webdav/D", StringComparison.Ordinal);
        using var editor = host.Http(signedIn: false);

        foreach (var address in new[] { altered, "/heliograph/webdav/not-a-token/letter.docx" })
        {
            using var response = await editor.SendAsync(new HttpRequestMessage(new HttpMethod(method), address) { Content = new ByteArrayContent([1, 2, 3]) });
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, address);
        }
    }

    [Fact]
    public async Task Only_a_signed_in_author_is_given_a_link()
    {
        await using var host = await StartWithDocumentAsync();
        using var anonymous = host.Http(signedIn: false);

        (await anonymous.PostAsync("correspondence/reminder/document/edit-link", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task There_is_no_link_for_a_draft_with_no_document()
    {
        await using var host = await ApiHost.StartAsync();
        host.Dictionaries["members"] = Members;
        using var http = host.Http();
        await http.PutAsJsonAsync("correspondence/reminder", new { expectedRevision = 0, name = "Reminder", dictionary = "members", sms = "Hi." });

        (await http.PostAsync("correspondence/reminder/document/edit-link", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<ApiHost> StartWithDocumentAsync()
    {
        var host = await ApiHost.StartAsync();
        host.Dictionaries["members"] = Members;
        using var http = host.Http();
        await http.PutAsJsonAsync("correspondence/reminder", new { expectedRevision = 0, name = "Your reminder", dictionary = "members" });
        var starter = await http.GetByteArrayAsync("correspondence/reminder/document/starter");
        await http.PutAsync("correspondence/reminder/document?expectedRevision=1", new ByteArrayContent(starter));
        return host;
    }

    private static async Task<JsonElement> LinkAsync(HttpClient http) =>
        await (await http.PostAsync("correspondence/reminder/document/edit-link", null)).Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>The link's path, for a test client that has its own address.</summary>
    private static string Path(JsonElement link) => new Uri(link.GetProperty("url").GetString()!).PathAndQuery;
}
