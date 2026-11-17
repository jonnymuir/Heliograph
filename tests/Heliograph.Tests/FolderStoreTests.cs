using Heliograph.Library;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>The folder store and dictionary source a small host uses: correspondence as ordinary files.</summary>
public sealed class FolderStoreTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("heliograph-folder-");

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public async Task A_benchmark_letter_is_kept_as_files_and_renders_from_them_after_a_restart()
    {
        CopyBenchmark("acknowledgement");
        var content = new CorrespondenceContent
        {
            Name = "Acknowledgement",
            Dictionary = "acknowledgement",
            Document = File.ReadAllBytes(Benchmark.PathTo("acknowledgement", "document.docx")),
            Email = File.ReadAllText(Benchmark.PathTo("acknowledgement", "email.md")),
            Sms = File.ReadAllText(Benchmark.PathTo("acknowledgement", "sms.txt")),
        };
        using (var store = Store())
        {
            var library = new CorrespondenceLibrary(store, Dictionaries(), TimeProvider.System);
            (await library.SaveDraftAsync("acknowledgement", content, 0)).Diagnostics.Should().BeEmpty();
            (await library.PublishAsync("acknowledgement")).Version.Should().Be(1);
        }

        using var restarted = Store();
        var render = await new CorrespondenceLibrary(restarted, Dictionaries(), TimeProvider.System)
            .RenderAsync("acknowledgement", Benchmark.Sample("acknowledgement", "typical"), Benchmark.British);

        render.Version.Should().Be(1);
        render.Rendered!.Sms!.Text.Should().StartWith("NJF: we have your licence application");
        File.Exists(Path.Combine(_folder.FullName, "correspondence", "acknowledgement", "draft", "email.md")).Should().BeTrue();
        File.Exists(Path.Combine(_folder.FullName, "correspondence", "acknowledgement", "versions", "1", "dictionary.json")).Should().BeTrue();
    }

    [Fact]
    public async Task A_part_that_is_taken_out_of_the_draft_is_gone_from_the_folder()
    {
        using var store = Store();
        await store.SaveDraftAsync("welcome", new CorrespondenceContent { Name = "Welcome", Dictionary = "members", Sms = "Hi.", Email = "Subject: Hi\n\nHello." }, 0);

        await store.SaveDraftAsync("welcome", new CorrespondenceContent { Name = "Welcome", Dictionary = "members", Sms = "Hi." }, 1);

        var draft = await store.ReadDraftAsync("welcome");
        (draft!.Revision, draft.Content.Email, draft.Content.Sms).Should().Be((2, (string?)null, "Hi."));
    }

    [Fact]
    public async Task Two_saves_from_the_same_revision_at_once_cannot_both_succeed()
    {
        using var store = Store();
        var content = new CorrespondenceContent { Name = "Welcome", Dictionary = "members", Sms = "Hi." };

        var saves = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.SaveDraftAsync("welcome", content, 0)));

        saves.Count(revision => revision is not null).Should().Be(1);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("Welcome")]
    [InlineData("a/b")]
    public async Task A_key_that_could_leave_the_folder_is_never_used_as_a_path(string key)
    {
        using var store = Store();

        (await store.SaveDraftAsync(key, new CorrespondenceContent { Name = "x", Dictionary = "members", Sms = "Hi." }, 0)).Should().BeNull();
        (await store.ReadDraftAsync(key)).Should().BeNull();
        Directory.GetFileSystemEntries(_folder.FullName, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Dictionaries_and_their_samples_are_read_from_their_own_files()
    {
        CopyBenchmark("decision-letter");
        var dictionaries = Dictionaries();

        (await dictionaries.ListAsync()).Should().Equal("decision-letter");
        (await dictionaries.ListSamplesAsync("decision-letter")).Should().Equal("approved", "approved-dangerous-props", "refused");
        (await dictionaries.ReadSampleAsync("decision-letter", "refused")).Should().Contain("Casey Catcher");
        (await dictionaries.ReadSampleAsync("decision-letter", "../decision-letter")).Should().BeNull();
        (await dictionaries.ReadAsync("../decision-letter")).Should().BeNull();
    }

    [Fact]
    public async Task The_library_hands_out_a_starter_document_and_the_draft_back_with_comments()
    {
        CopyBenchmark("acknowledgement");
        using var store = Store();
        var library = new CorrespondenceLibrary(store, Dictionaries(), TimeProvider.System);
        await library.SaveDraftAsync("acknowledgement", new CorrespondenceContent { Name = "We have your application", Dictionary = "acknowledgement", Sms = "Hi." }, 0);

        var starter = await library.StarterDocumentAsync("acknowledgement");
        (await library.AnnotatedDocumentAsync("acknowledgement")).Should().BeNull("the draft has no document yet");
        await library.SaveDraftAsync("acknowledgement", new CorrespondenceContent { Name = "We have your application", Dictionary = "acknowledgement", Document = starter }, 1);
        var annotated = await library.AnnotatedDocumentAsync("acknowledgement");

        TestDocx.Property(starter!, "title").Should().Be("We have your application");
        TestDocx.PartText(annotated!, "word/comments.xml").Should().Contain(DiagnosticCodes.TagSheetLeftIn);
    }

    private FolderCorrespondenceStore Store() => new(Path.Combine(_folder.FullName, "correspondence"));

    private FolderDictionarySource Dictionaries() => new(Path.Combine(_folder.FullName, "dictionaries"));

    private void CopyBenchmark(string letter)
    {
        var dictionaries = Directory.CreateDirectory(Path.Combine(_folder.FullName, "dictionaries"));
        File.Copy(Benchmark.PathTo(letter, "dictionary.json"), Path.Combine(dictionaries.FullName, letter + ".json"));
        var samples = Directory.CreateDirectory(Path.Combine(dictionaries.FullName, letter + ".samples"));
        foreach (var sample in Directory.GetFiles(Path.GetDirectoryName(Benchmark.PathTo(letter, Path.Combine("samples", "x")))!, "*.json"))
        {
            File.Copy(sample, Path.Combine(samples.FullName, Path.GetFileName(sample)));
        }
    }
}
