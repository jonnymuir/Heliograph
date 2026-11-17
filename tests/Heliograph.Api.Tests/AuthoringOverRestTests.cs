using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Heliograph.Api.Tests;

/// <summary>What the workbench does, through the REST API: the same operations, and the same answers, as the MCP server.</summary>
public class AuthoringOverRestTests
{
    private const string Members = """{ "fields": [ { "name": "Member name", "key": "memberName", "type": "text" } ] }""";

    [Fact]
    public async Task A_correspondence_is_written_checked_previewed_with_a_sample_published_and_rendered()
    {
        await using var host = await ApiHost.StartAsync();
        host.Dictionaries["members"] = Members;
        host.Dictionaries.Sample("members", "alex", """{ "memberName": "Alex Applicant" }""");
        using var http = host.Http();

        (await Json(http.GetAsync("dictionaries/members"))).GetProperty("fields")[0].GetProperty("tag").GetString().Should().Be("((Member name))");
        (await Json(http.GetAsync("dictionaries/members/samples"))).EnumerateArray().Select(s => s.GetString()).Should().Equal("alex");

        var save = await Json(http.PutAsJsonAsync("correspondence/reminder", new { expectedRevision = 0, name = "Reminder", dictionary = "members", sms = "Hi ((Member name))." }));
        save.GetProperty("revision").GetInt32().Should().Be(1);

        var preview = await Json(http.PostAsJsonAsync("correspondence/reminder/preview", new { sample = "alex" }));
        preview.GetProperty("sms").GetProperty("text").GetString().Should().Be("Hi Alex Applicant.");

        (await Json(http.PostAsJsonAsync("correspondence/reminder/publish", new { }))).GetProperty("version").GetInt32().Should().Be(1);

        var render = await Json(http.PostAsJsonAsync("correspondence/reminder/render", new { data = new { memberName = "Sam" } }));
        (render.GetProperty("version").GetInt32(), render.GetProperty("sms").GetProperty("text").GetString()).Should().Be((1, "Hi Sam."));
    }

    [Fact]
    public async Task A_document_is_uploaded_as_a_file_and_comes_back_with_comments_where_it_is_wrong()
    {
        await using var host = await ApiHost.StartAsync();
        host.Dictionaries["members"] = Members;
        using var http = host.Http();
        await http.PutAsJsonAsync("correspondence/reminder", new { expectedRevision = 0, name = "Your reminder", dictionary = "members" });

        using var starter = await http.GetAsync("correspondence/reminder/document/starter");
        starter.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        using var upload = new ByteArrayContent(await starter.Content.ReadAsByteArrayAsync());
        var saved = await Json(http.PutAsync("correspondence/reminder/document?expectedRevision=1", upload));
        var annotated = await http.GetAsync("correspondence/reminder/document/annotated");

        saved.GetProperty("diagnostics")[0].GetProperty("code").GetString().Should().Be(DiagnosticCodes.TagSheetLeftIn);
        annotated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await http.GetAsync("correspondence/reminder")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(http.GetAsync("correspondence/reminder"))).GetProperty("hasDocument").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task A_save_from_an_out_of_date_copy_answers_conflict()
    {
        await using var host = await ApiHost.StartAsync();
        host.Dictionaries["members"] = Members;
        using var http = host.Http();
        await http.PutAsJsonAsync("correspondence/reminder", new { expectedRevision = 0, name = "Reminder", dictionary = "members", sms = "Hi." });

        var late = await http.PutAsJsonAsync("correspondence/reminder", new { expectedRevision = 0, sms = "Hello." });

        late.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_draft_with_errors_cannot_be_published_and_says_why()
    {
        await using var host = await ApiHost.StartAsync();
        host.Dictionaries["members"] = Members;
        using var http = host.Http();
        await http.PutAsJsonAsync("correspondence/reminder", new { expectedRevision = 0, name = "Reminder", dictionary = "members", sms = "Hi ((Member nam))." });

        var publish = await http.PostAsJsonAsync("correspondence/reminder/publish", new { });

        publish.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Json(Task.FromResult(publish))).GetProperty("diagnostics")[0].GetProperty("code").GetString().Should().Be(DiagnosticCodes.UnknownField);
    }

    [Fact]
    public async Task A_PDF_preview_says_plainly_when_the_site_has_no_route_to_PDF()
    {
        await using var host = await ApiHost.StartAsync();
        host.Dictionaries["members"] = Members;
        using var http = host.Http();
        await http.PutAsJsonAsync("correspondence/reminder", new { expectedRevision = 0, name = "Reminder", dictionary = "members", sms = "Hi." });

        (await http.PostAsJsonAsync("correspondence/reminder/preview/pdf", new { data = new { } })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("GET", "correspondence")]
    [InlineData("GET", "dictionaries")]
    [InlineData("PUT", "correspondence/reminder")]
    [InlineData("POST", "correspondence/reminder/render")]
    public async Task Nobody_reaches_any_route_without_signing_in(string method, string path)
    {
        await using var host = await ApiHost.StartAsync();
        using var anonymous = host.Http(signedIn: false);

        var response = await anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "GET" ? null : new StringContent("{}", MediaTypeHeaderValue.Parse("application/json")),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<JsonElement> Json(Task<HttpResponseMessage> call)
    {
        using var response = await call;
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
