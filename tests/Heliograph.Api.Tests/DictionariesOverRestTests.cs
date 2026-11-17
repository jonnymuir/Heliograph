using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Heliograph.Api.Tests;

/// <summary>The Dictionaries screen's calls: read the JSON, save it, see what it breaks, keep sample recipients.</summary>
public class DictionariesOverRestTests
{
    [Fact]
    public async Task A_dictionary_is_saved_and_the_answer_says_which_drafts_it_breaks()
    {
        await using var host = await ApiHost.StartAsync();
        host.Dictionaries["members"] = """{ "fields": [ { "name": "Member name", "key": "memberName", "type": "text" }, { "name": "Town", "key": "town", "type": "text" } ] }""";
        using var http = host.Http();
        await http.PutAsJsonAsync("correspondence/welcome", new { expectedRevision = 0, name = "Welcome", dictionary = "members", sms = "Hi ((Member name)) of ((Town))." });

        using var saved = await http.PutAsync("dictionaries/members", Json("""{ "fields": [ { "name": "Member name", "key": "memberName", "type": "text" } ] }"""));
        var body = await saved.Content.ReadFromJsonAsync<JsonElement>();

        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var use = body.GetProperty("uses")[0];
        use.GetProperty("key").GetString().Should().Be("welcome");
        use.GetProperty("diagnostics")[0].GetProperty("code").GetString().Should().Be(DiagnosticCodes.UnknownField);
        (await http.GetStringAsync("dictionaries/members/definition")).Should().NotContain("Town");
    }

    [Fact]
    public async Task A_broken_dictionary_is_refused_with_why()
    {
        await using var host = await ApiHost.StartAsync();
        using var http = host.Http();

        using var refused = await http.PutAsync("dictionaries/members", Json("{ not json"));

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("problems")[0].GetProperty("code").GetString().Should().Be(DiagnosticCodes.DictionaryNotValid);
    }

    [Fact]
    public async Task The_settings_say_whether_dictionaries_can_be_changed_here()
    {
        await using var host = await ApiHost.StartAsync();
        using var http = host.Http();

        var settings = await http.GetFromJsonAsync<JsonElement>("dictionary-settings");

        settings.GetProperty("canEdit").GetBoolean().Should().BeTrue();
        settings.GetProperty("startingPoints").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Nobody_changes_a_dictionary_without_signing_in()
    {
        await using var host = await ApiHost.StartAsync();
        using var anonymous = host.Http(signedIn: false);

        (await anonymous.PutAsync("dictionaries/members", Json("""{ "fields": [] }"""))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
