using Heliograph.Library;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// Stored correspondence: an author works on a draft, publishing makes a numbered version that never changes, and
/// only a published version is rendered for a recipient.
/// </summary>
public class LibraryTests
{
    private const string Members = """{ "fields": [ { "name": "Member name", "key": "memberName", "type": "text" } ] }""";
    private const string MembersAndTowns = """{ "fields": [ { "name": "Member name", "key": "memberName", "type": "text" }, { "name": "Town", "key": "town", "type": "text" } ] }""";
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 30, 0, TimeSpan.Zero);

    private readonly InMemoryDictionarySource _dictionaries = new() { ["members"] = Members };
    private readonly InMemoryCorrespondenceStore _store = new();
    private readonly CorrespondenceLibrary _library;

    public LibraryTests() => _library = new CorrespondenceLibrary(_store, _dictionaries, new FixedClock(Now));

    [Fact]
    public async Task A_benchmark_letter_is_saved_published_and_rendered_with_every_part()
    {
        _dictionaries["licensing"] = File.ReadAllText(Benchmark.PathTo("acknowledgement", "dictionary.json"));
        var content = new CorrespondenceContent
        {
            Name = "Acknowledgement",
            Dictionary = "licensing",
            Document = File.ReadAllBytes(Benchmark.PathTo("acknowledgement", "document.docx")),
            Email = File.ReadAllText(Benchmark.PathTo("acknowledgement", "email.md")),
            Sms = File.ReadAllText(Benchmark.PathTo("acknowledgement", "sms.txt")),
        };

        (await _library.SaveDraftAsync("acknowledgement", content, 0)).Diagnostics.Should().BeEmpty();
        (await _library.PublishAsync("acknowledgement")).Version.Should().Be(1);
        var render = await _library.RenderAsync("acknowledgement", Benchmark.Sample("acknowledgement", "typical"), Benchmark.British);

        render.Diagnostics.Should().BeEmpty();
        render.Version.Should().Be(1);
        TestDocx.Paragraphs(render.Rendered!.Docx!).Should().Contain("Dear Alex Applicant,");
        render.Rendered.Email!.Subject.Should().Be("We have received your application: NJF-2026-004217");
        render.Rendered.Sms!.Text.Should().StartWith("NJF: we have your licence application");
    }

    [Fact]
    public async Task A_draft_that_does_not_compile_is_still_saved_and_says_what_is_wrong()
    {
        var save = await _library.SaveDraftAsync("welcome", Sms("Hi ((Member nam))."), 0);

        save.Saved.Should().BeTrue();
        save.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.UnknownField);
        (await _library.ReadDraftAsync("welcome"))!.Content.Sms.Should().Be("Hi ((Member nam)).");
    }

    [Fact]
    public async Task A_draft_that_does_not_compile_cannot_be_published()
    {
        await _library.SaveDraftAsync("welcome", Sms("Hi ((Member nam))."), 0);

        var publish = await _library.PublishAsync("welcome");

        publish.Published.Should().BeFalse();
        publish.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.UnknownField);
        (await _library.ReadVersionAsync("welcome")).Should().BeNull();
    }

    [Fact]
    public async Task A_draft_is_never_rendered_for_a_recipient()
    {
        await _library.SaveDraftAsync("welcome", Sms("Hi ((Member name))."), 0);

        var render = await _library.RenderAsync("welcome", Alex, Benchmark.British);

        render.Rendered.Should().BeNull();
        render.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.NotPublished);
    }

    [Fact]
    public async Task A_preview_renders_the_draft_and_names_no_version()
    {
        await _library.SaveDraftAsync("welcome", Sms("Hi ((Member name))."), 0);

        var preview = await _library.PreviewAsync("welcome", Alex, Benchmark.British);

        preview.Rendered!.Sms!.Text.Should().Be("Hi Alex.");
        preview.Version.Should().BeNull();
    }

    [Fact]
    public async Task Each_publish_makes_the_next_version_and_an_earlier_one_still_renders_as_it_was()
    {
        await _library.SaveDraftAsync("welcome", Sms("Hi ((Member name))."), 0);
        await _library.PublishAsync("welcome");
        await _library.SaveDraftAsync("welcome", Sms("Hello ((Member name))."), 1);
        (await _library.PublishAsync("welcome")).Version.Should().Be(2);

        var newest = await _library.RenderAsync("welcome", Alex, Benchmark.British);
        var first = await _library.RenderAsync("welcome", Alex, Benchmark.British, version: 1);

        (newest.Version, newest.Rendered!.Sms!.Text).Should().Be((2, "Hello Alex."));
        (first.Version, first.Rendered!.Sms!.Text).Should().Be((1, "Hi Alex."));
    }

    [Fact]
    public async Task A_change_to_the_draft_does_not_reach_recipients_until_it_is_published()
    {
        await _library.SaveDraftAsync("welcome", Sms("Hi ((Member name))."), 0);
        await _library.PublishAsync("welcome");
        await _library.SaveDraftAsync("welcome", Sms("Hello ((Member name))."), 1);

        (await _library.RenderAsync("welcome", Alex, Benchmark.British)).Rendered!.Sms!.Text.Should().Be("Hi Alex.");
    }

    [Fact]
    public async Task A_published_version_keeps_the_dictionary_it_was_published_with()
    {
        _dictionaries["members"] = MembersAndTowns;
        await _library.SaveDraftAsync("welcome", Sms("Hi ((Member name)) of ((Town))."), 0);
        await _library.PublishAsync("welcome");

        _dictionaries["members"] = Members;
        var render = await new CorrespondenceLibrary(_store, _dictionaries, new FixedClock(Now))
            .RenderAsync("welcome", Benchmark.Data("""{ "memberName": "Alex", "town": "Clubtown" }"""), Benchmark.British);

        render.Rendered!.Sms!.Text.Should().Be("Hi Alex of Clubtown.");
        (await _library.ReadVersionAsync("welcome"))!.PublishedAt.Should().Be(Now);
    }

    [Fact]
    public async Task A_change_to_a_dictionary_shows_which_drafts_it_breaks()
    {
        _dictionaries["members"] = MembersAndTowns;
        _dictionaries["other"] = Members;
        await _library.SaveDraftAsync("welcome", Sms("Hi ((Member name)) of ((Town))."), 0);
        await _library.SaveDraftAsync("reminder", Sms("Hi ((Member name))."), 0);
        await _library.SaveDraftAsync("unrelated", new CorrespondenceContent { Name = "Unrelated", Dictionary = "other", Sms = "Hi." }, 0);

        _dictionaries["members"] = Members;
        var uses = await _library.CheckDictionaryAsync("members");

        uses.Select(use => use.Key).Should().Equal("reminder", "welcome");
        uses[0].Diagnostics.Should().BeEmpty();
        uses[1].Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.UnknownField);
    }

    [Fact]
    public async Task A_save_made_from_an_out_of_date_copy_is_refused_and_changes_nothing()
    {
        await _library.SaveDraftAsync("welcome", Sms("Hi ((Member name))."), 0);
        await _library.SaveDraftAsync("welcome", Sms("Hello ((Member name))."), 1);

        var late = await _library.SaveDraftAsync("welcome", Sms("Dear ((Member name))."), 1);

        late.Saved.Should().BeFalse();
        late.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.ChangedElsewhere);
        (await _library.ReadDraftAsync("welcome")).Should().BeEquivalentTo(new { Revision = 2, Content = new { Sms = "Hello ((Member name))." } });
    }

    [Theory]
    [InlineData("Decision Letter")]
    [InlineData("../etc/passwd")]
    [InlineData("-letter")]
    [InlineData("")]
    public async Task A_key_that_could_not_be_a_file_name_or_part_of_an_address_is_refused(string key)
    {
        var save = await _library.SaveDraftAsync(key, Sms("Hi."), 0);

        save.Saved.Should().BeFalse();
        save.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.KeyNotValid);
    }

    [Fact]
    public async Task A_draft_that_names_a_dictionary_that_does_not_exist_is_told_so()
    {
        var save = await _library.SaveDraftAsync("welcome", new CorrespondenceContent { Name = "Welcome", Dictionary = "nowhere", Sms = "Hi." }, 0);

        save.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.DictionaryNotFound);
    }

    [Fact]
    public async Task A_dictionary_that_cannot_be_read_is_reported_and_not_thrown()
    {
        _dictionaries["members"] = "{ not json";
        await _library.SaveDraftAsync("welcome", Sms("Hi."), 0);

        var check = await _library.CheckAsync("welcome");

        check.CanPublish.Should().BeFalse();
        check.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.DictionaryNotValid);
    }

    [Fact]
    public async Task A_draft_with_no_parts_is_saved_and_cannot_be_published()
    {
        await _library.SaveDraftAsync("welcome", new CorrespondenceContent { Name = "Welcome", Dictionary = "members" }, 0);

        (await _library.PublishAsync("welcome")).Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.NoParts);
    }

    [Fact]
    public async Task Asking_for_a_correspondence_that_does_not_exist_says_so()
    {
        (await _library.CheckAsync("nothing")).Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.CorrespondenceNotFound);
        (await _library.PublishAsync("nothing")).Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.CorrespondenceNotFound);
        (await _library.PreviewAsync("nothing", Alex, Benchmark.British)).Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.CorrespondenceNotFound);
    }

    [Fact]
    public async Task Data_that_is_wrong_for_a_recipient_renders_nothing_and_names_no_version()
    {
        await _library.SaveDraftAsync("welcome", Sms("Hi ((Member name))."), 0);
        await _library.PublishAsync("welcome");

        var render = await _library.RenderAsync("welcome", Benchmark.Data("{}"), Benchmark.British);

        render.Rendered.Should().BeNull();
        render.Version.Should().BeNull();
        render.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.ValueMissing);
    }

    [Fact]
    public async Task The_list_shows_each_correspondence_with_its_newest_published_version()
    {
        await _library.SaveDraftAsync("welcome", Sms("Hi."), 0);
        await _library.SaveDraftAsync("reminder", Sms("Hi."), 0);
        await _library.PublishAsync("welcome");

        (await _library.ListAsync()).Should().Equal(
            new CorrespondenceSummary("reminder", "Welcome", "members", null),
            new CorrespondenceSummary("welcome", "Welcome", "members", 1));
        (await _library.Dictionaries.ListAsync()).Should().Equal("members");
    }

    private static System.Text.Json.JsonElement Alex => Benchmark.Data("""{ "memberName": "Alex" }""");

    private static CorrespondenceContent Sms(string text) => new() { Name = "Welcome", Dictionary = "members", Sms = text };
}
