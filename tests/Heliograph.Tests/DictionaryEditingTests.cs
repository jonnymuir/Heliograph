using Heliograph.Library;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>Keeping dictionaries from the workbench: what is saved, what is refused, and what a change breaks.</summary>
public sealed class DictionaryEditingTests : IDisposable
{
    private const string Members = """{ "fields": [ { "name": "Member name", "key": "memberName", "type": "text" }, { "name": "Town", "key": "town", "type": "text" } ] }""";
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("heliograph-dictionaries-");

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public async Task A_dictionary_saved_from_the_workbench_is_read_back_and_shows_what_it_breaks()
    {
        var library = Library();
        (await library.Dictionaries.Editing.SaveAsync("members", Members)).Saved.Should().BeTrue();
        await library.SaveDraftAsync("welcome", new CorrespondenceContent { Name = "Welcome", Dictionary = "members", Sms = "Hi ((Member name)) of ((Town))." }, 0);

        await library.Dictionaries.Editing.SaveAsync("members", """{ "fields": [ { "name": "Member name", "key": "memberName", "type": "text" } ] }""");

        (await library.Dictionaries.SheetAsync("members"))!.Fields.Select(f => f.Name).Should().Equal("Member name");
        var use = (await library.CheckDictionaryAsync("members")).Should().ContainSingle().Subject;
        use.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.UnknownField);
    }

    [Fact]
    public async Task A_published_version_keeps_rendering_after_its_dictionary_loses_a_field()
    {
        var library = Library();
        await library.Dictionaries.Editing.SaveAsync("members", Members);
        await library.SaveDraftAsync("welcome", new CorrespondenceContent { Name = "Welcome", Dictionary = "members", Sms = "Hi ((Member name)) of ((Town))." }, 0);
        await library.PublishAsync("welcome");

        await library.Dictionaries.Editing.SaveAsync("members", """{ "fields": [ { "name": "Member name", "key": "memberName", "type": "text" } ] }""");
        var render = await library.RenderAsync("welcome", Benchmark.Data("""{ "memberName": "Alex", "town": "Clubtown" }"""), Benchmark.British);

        render.Rendered!.Sms!.Text.Should().Be("Hi Alex of Clubtown.");
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "fields": [ { "name": "A", "key": "a", "type": "text" }, { "name": "A", "key": "b", "type": "text" } ] }""")]
    [InlineData("""{ "fields": [ { "name": "Things", "key": "things", "type": "list" } ] }""")]
    public async Task A_dictionary_that_does_not_read_as_one_is_refused_and_nothing_is_written(string json)
    {
        var library = Library();

        var save = await library.Dictionaries.Editing.SaveAsync("members", json);

        save.Saved.Should().BeFalse();
        save.Problems.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.DictionaryNotValid);
        (await library.Dictionaries.ListAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("a/b")]
    [InlineData("")]
    public async Task A_name_that_could_leave_the_folder_is_refused(string name)
    {
        var library = Library();

        (await library.Dictionaries.Editing.SaveAsync(name, Members)).Problems.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.DictionaryNameNotValid);
        (await library.Dictionaries.Editing.SaveSampleAsync("members", name, "{}")).Saved.Should().BeFalse();
        Directory.GetFileSystemEntries(_folder.FullName, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Sample_recipients_are_saved_read_and_deleted_and_must_be_objects()
    {
        var library = Library();
        await library.Dictionaries.Editing.SaveAsync("members", Members);

        (await library.Dictionaries.Editing.SaveSampleAsync("members", "alex", "[1, 2]")).Problems.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.SampleNotValid);
        (await library.Dictionaries.Editing.SaveSampleAsync("members", "alex", """{ "memberName": "Alex" }""")).Saved.Should().BeTrue();
        (await library.Dictionaries.SamplesAsync("members")).Should().Equal("alex");
        (await library.Dictionaries.SampleAsync("members", "alex")).Should().Contain("Alex");

        await library.Dictionaries.Editing.DeleteSampleAsync("members", "alex");
        (await library.Dictionaries.SamplesAsync("members")).Should().BeEmpty();
    }

    [Fact]
    public async Task A_site_whose_dictionaries_come_from_elsewhere_cannot_change_them_here()
    {
        var library = new CorrespondenceLibrary(new InMemoryCorrespondenceStore(), new ReadOnlyDictionaries(), TimeProvider.System);

        library.Dictionaries.Editing.CanEdit.Should().BeFalse();
        (await library.Dictionaries.Editing.SaveAsync("members", Members)).Problems.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.DictionariesReadOnly);
    }

    [Fact]
    public async Task The_host_offers_starting_points_for_a_new_dictionary()
    {
        var points = new FixedStartingPoints(new DictionaryStartingPoint("Licence application", "From the blueprint's fields", Members));
        var library = new CorrespondenceLibrary(new InMemoryCorrespondenceStore(), new FolderDictionarySource(_folder.FullName), TimeProvider.System, points);

        (await library.Dictionaries.Editing.StartingPointsAsync()).Should().ContainSingle().Which.Name.Should().Be("Licence application");
        (await Library().Dictionaries.Editing.StartingPointsAsync()).Should().BeEmpty("a host that offers none has none");
    }

    private CorrespondenceLibrary Library() => new(new InMemoryCorrespondenceStore(), new FolderDictionarySource(_folder.FullName), TimeProvider.System);

    private sealed class ReadOnlyDictionaries : IDictionarySource
    {
        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string?> ReadAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class FixedStartingPoints(params DictionaryStartingPoint[] points) : IDictionaryStartingPoints
    {
        public Task<IReadOnlyList<DictionaryStartingPoint>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DictionaryStartingPoint>>(points);
    }
}
