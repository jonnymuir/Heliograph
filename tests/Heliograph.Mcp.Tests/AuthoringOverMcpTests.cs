using System.Net;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Heliograph.Mcp.Tests;

/// <summary>An agent connected over MCP does the same job a person does, and gets the same answers.</summary>
public class AuthoringOverMcpTests
{
    private const string Members = """
        { "fields": [
          { "name": "Member name", "key": "memberName", "type": "text" },
          { "name": "Owes money", "key": "owesMoney", "type": "yesNo" },
          { "name": "Payments", "key": "payments", "type": "list", "fields": [ { "name": "Payment date", "key": "date", "type": "date" } ] }
        ] }
        """;

    private static readonly string[] JustEmail = ["email"];
    private static readonly string[] NotAPart = ["pdf"];

    [Fact]
    public async Task An_agent_writes_a_correspondence_from_the_dictionary_to_a_published_version()
    {
        await using var host = await AuthoringHost.StartAsync();
        host.Dictionaries["members"] = Members;
        await using var agent = await host.ConnectAsync();

        var sheet = await agent.CallAsync("read_dictionary", new { name = "members" });
        var tags = sheet.GetProperty("fields").EnumerateArray().Select(field => field.TryGetProperty("tag", out var tag) ? tag.GetString() : null).ToList();
        tags.Should().Equal("((Member name))", "((if Owes money))", null, "((Payment date))");

        var first = await agent.CallAsync("save_correspondence", new
        {
            key = "reminder", expectedRevision = 0, name = "Reminder", dictionary = "members", sms = "Hi ((Member nam)).",
        });
        first.GetProperty("saved").GetBoolean().Should().BeTrue();
        first.GetProperty("canPublish").GetBoolean().Should().BeFalse();
        var problem = first.GetProperty("problems").EnumerateArray().Should().ContainSingle().Subject;
        problem.GetProperty("code").GetString().Should().Be(DiagnosticCodes.UnknownField);
        problem.GetProperty("fix").GetString().Should().Contain("Member name");

        var fixedDraft = await agent.CallAsync("save_correspondence", new
        {
            key = "reminder", expectedRevision = 1, sms = "Hi ((Member name)).", email = "Subject: A reminder\n\nDear ((Member name)),\n\n((if Owes money))\nYou owe us money.\n((end if))",
        });
        fixedDraft.GetProperty("canPublish").GetBoolean().Should().BeTrue();

        var preview = await agent.CallAsync("preview_correspondence", new
        {
            key = "reminder", dataJson = """{ "memberName": "Alex Applicant", "owesMoney": false }""",
        });
        preview.GetProperty("sms").GetProperty("text").GetString().Should().Be("Hi Alex Applicant.");
        preview.GetProperty("sms").GetProperty("segments").GetInt32().Should().Be(1);
        preview.GetProperty("email").GetProperty("subject").GetString().Should().Be("A reminder");
        preview.GetProperty("email").GetProperty("text").GetString().Should().Contain("Dear Alex Applicant,").And.NotContain("You owe");

        var published = await agent.CallAsync("publish_correspondence", new { key = "reminder" });
        published.GetProperty("version").GetInt32().Should().Be(1);

        var list = await agent.CallAsync("list_correspondence", new { });
        list.EnumerateArray().Should().ContainSingle().Which.GetProperty("latestVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task A_part_the_agent_does_not_pass_is_kept_and_one_it_removes_is_gone()
    {
        await using var host = await AuthoringHost.StartAsync();
        host.Dictionaries["members"] = Members;
        await using var agent = await host.ConnectAsync();
        await agent.CallAsync("save_correspondence", new { key = "reminder", expectedRevision = 0, name = "Reminder", dictionary = "members", sms = "Hi.", email = "Subject: Hi\n\nHello." });

        await agent.CallAsync("save_correspondence", new { key = "reminder", expectedRevision = 1, sms = "Hello." });
        await agent.CallAsync("save_correspondence", new { key = "reminder", expectedRevision = 2, remove = JustEmail });

        var draft = await agent.CallAsync("read_correspondence", new { key = "reminder" });
        draft.GetProperty("sms").GetString().Should().Be("Hello.");
        draft.TryGetProperty("email", out _).Should().BeFalse("a part that is not there is left out of what the agent is sent");
        draft.GetProperty("name").GetString().Should().Be("Reminder");
        draft.GetProperty("revision").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task A_document_travels_as_base64_and_is_checked_like_any_upload()
    {
        await using var host = await AuthoringHost.StartAsync();
        host.Dictionaries["licensing"] = File.ReadAllText(Benchmark("dictionary.json"));
        await using var agent = await host.ConnectAsync();

        var save = await agent.CallAsync("save_correspondence", new
        {
            key = "acknowledgement", expectedRevision = 0, name = "Acknowledgement", dictionary = "licensing",
            documentBase64 = Convert.ToBase64String(File.ReadAllBytes(Benchmark("document.docx"))),
        });
        save.GetProperty("canPublish").GetBoolean().Should().BeTrue();

        var preview = await agent.CallAsync("preview_correspondence", new
        {
            key = "acknowledgement", dataJson = File.ReadAllText(Benchmark(Path.Combine("samples", "typical.json"))), includeDocument = true,
        });
        Convert.FromBase64String(preview.GetProperty("documentBase64").GetString()!).Take(2).Should().Equal((byte)'P', (byte)'K');

        var notADocument = await agent.CallAsync("save_correspondence", new
        {
            key = "acknowledgement", expectedRevision = 1, documentBase64 = Convert.ToBase64String("Dear Alex"u8.ToArray()),
        });
        notADocument.GetProperty("problems").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("code").GetString().Should().Be(DiagnosticCodes.NotAWordDocument);
    }

    [Fact]
    public async Task A_save_from_an_out_of_date_copy_is_refused_with_what_to_do()
    {
        await using var host = await AuthoringHost.StartAsync();
        host.Dictionaries["members"] = Members;
        await using var agent = await host.ConnectAsync();
        await agent.CallAsync("save_correspondence", new { key = "reminder", expectedRevision = 0, name = "Reminder", dictionary = "members", sms = "Hi." });

        var late = await agent.CallAsync("save_correspondence", new { key = "reminder", expectedRevision = 0, sms = "Hello." });

        late.GetProperty("saved").GetBoolean().Should().BeFalse();
        late.GetProperty("problems")[0].GetProperty("code").GetString().Should().Be(DiagnosticCodes.ChangedElsewhere);
    }

    [Fact]
    public async Task A_mistake_in_how_a_tool_is_called_is_explained_to_the_agent()
    {
        await using var host = await AuthoringHost.StartAsync();
        host.Dictionaries["members"] = Members;
        await using var agent = await host.ConnectAsync();
        await agent.CallAsync("save_correspondence", new { key = "reminder", expectedRevision = 0, name = "Reminder", dictionary = "members", sms = "Hi." });

        (await agent.RefusalAsync("save_correspondence", new { key = "new-one", expectedRevision = 0, sms = "Hi." })).Should().Contain("needs a name and a dictionary");
        (await agent.RefusalAsync("read_dictionary", new { name = "nowhere" })).Should().Contain("list_dictionaries");
        (await agent.RefusalAsync("preview_correspondence", new { key = "reminder", dataJson = "not json" })).Should().Contain("not valid JSON");
        (await agent.RefusalAsync("preview_correspondence", new { key = "reminder", dataJson = "{}", culture = "xx-NOWHERE" })).Should().Contain("en-GB");
        (await agent.RefusalAsync("save_correspondence", new { key = "reminder", expectedRevision = 1, remove = NotAPart })).Should().Contain("not a part");
    }

    [Fact]
    public async Task The_tools_an_agent_is_offered_are_the_authoring_operations_and_no_run_time_render()
    {
        await using var host = await AuthoringHost.StartAsync();
        await using var agent = await host.ConnectAsync();

        var tools = await agent.ListToolsAsync();

        tools.Select(tool => tool.Name).Should().BeEquivalentTo(
            "list_correspondence", "list_dictionaries", "read_dictionary", "list_samples", "starter_document", "annotated_document",
            "read_dictionary_definition", "list_dictionary_starting_points", "save_dictionary", "save_sample",
            "read_correspondence", "save_correspondence",
            "check_correspondence", "check_dictionary", "preview_correspondence", "publish_correspondence");
        tools.Should().OnlyContain(tool => !string.IsNullOrWhiteSpace(tool.Description));
    }

    [Fact]
    public async Task An_agent_is_told_how_to_work_when_it_connects_and_what_this_host_adds()
    {
        await using var host = await AuthoringHost.StartAsync("The \"licensing\" dictionary belongs to the event licence service.");
        await using var agent = await host.ConnectAsync();

        agent.ServerInstructions.Should().Contain("heliograph-skills://writing-a-template").And.Contain("event licence service");
        (await agent.ListPromptsAsync()).Select(prompt => prompt.Name)
            .Should().BeEquivalentTo("write-a-correspondence", "fix-a-correspondence", "review-before-publishing");
    }

    [Fact]
    public async Task Nobody_reaches_the_endpoint_without_signing_in()
    {
        await using var host = await AuthoringHost.StartAsync();
        using var anonymous = host.Http(signedIn: false);

        var response = await anonymous.PostAsync(CorrespondenceAuthoringMcpExtensions.DefaultPattern,
            new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string Benchmark(string file) => Path.Combine(AppContext.BaseDirectory, "benchmarks", "acknowledgement", file);
}
