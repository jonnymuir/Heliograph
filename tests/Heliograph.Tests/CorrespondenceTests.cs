using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>A correspondence is one thing with a part for each channel: one compile, one render, every part.</summary>
public class CorrespondenceTests
{
    private static readonly FieldDictionary Dictionary = new(
    [
        new Field("Member name", "memberName", FieldType.Text),
        new Field("Amount owed", "amountOwed", FieldType.Money),
    ]);

    [Theory]
    [InlineData("acknowledgement", "typical", "Dear Alex Applicant,", "We have received your application: NJF-2026-004217", "NJF: we have your licence application")]
    [InlineData("decision-letter", "refused", "Dear Casey Catcher,", "A decision on your juggling event licence application", "NJF: we have made a decision")]
    [InlineData("annual-statement", "refund-and-arrears", "You owe £112.50", "Your NJF contributions statement", "You owe £112.50, due by 4 January 2027.")]
    public void One_call_renders_the_letter_the_email_and_the_text_for_a_benchmark_recipient(
        string letter, string sample, string inLetter, string subject, string inText)
    {
        var compilation = Compile(letter);
        compilation.Diagnostics.Should().BeEmpty();

        var rendered = compilation.Correspondence!.Render(Benchmark.Sample(letter, sample), Benchmark.British);

        rendered.Diagnostics.Should().BeEmpty();
        TestDocx.Paragraphs(rendered.Docx!).Should().Contain(inLetter);
        rendered.Email!.Subject.Should().Be(subject);
        rendered.Email.Html.Should().StartWith("<!DOCTYPE html>");
        rendered.Sms!.Text.Should().Contain(inText);
        rendered.Sms.Segments.Should().BeGreaterThan(0);
    }

    [Fact]
    public void A_correspondence_may_have_only_some_parts()
    {
        var compilation = Correspondence.Compile(new CorrespondenceSource { Sms = "Hi ((Member name))." }, Dictionary);

        var rendered = compilation.Correspondence!.Render(Benchmark.Data("""{ "memberName": "Alex" }"""), Benchmark.British);

        rendered.Sms!.Text.Should().Be("Hi Alex.");
        rendered.Docx.Should().BeNull();
        rendered.Email.Should().BeNull();
        rendered.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_correspondence_with_no_parts_is_not_a_correspondence()
    {
        var compile = () => Correspondence.Compile(new CorrespondenceSource(), Dictionary);

        compile.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_mistake_in_any_part_stops_the_whole_correspondence_and_every_parts_problems_are_reported()
    {
        using var document = TestDocx.With("Dear ((Membr name)),").Build();
        var source = new CorrespondenceSource { Document = document, Email = "Subject: Hello\n\nDear ((Member name)),", Sms = "You owe £((Amount owd))." };

        var compilation = Correspondence.Compile(source, Dictionary);

        compilation.Succeeded.Should().BeFalse();
        compilation.Correspondence.Should().BeNull();
        compilation.Diagnostics.Select(d => d.Location!.Part).Should().Equal("document", "sms");
    }

    [Fact]
    public void A_warning_in_one_part_does_not_stop_the_correspondence()
    {
        var source = new CorrespondenceSource { Email = "Subject: Hello\n\nDear {{Member name}},", Sms = "Hi ((Member name))." };

        var compilation = Correspondence.Compile(source, Dictionary);

        compilation.Succeeded.Should().BeTrue();
        compilation.Diagnostics.Should().ContainSingle().Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Data_that_is_wrong_gives_no_part_at_all_and_each_problem_once()
    {
        using var document = TestDocx.With("Dear ((Member name)), you owe £((Amount owed)).").Build();
        var source = new CorrespondenceSource { Document = document, Email = "Subject: Hi ((Member name))\n\nDear ((Member name)),", Sms = "Hi ((Member name))." };
        var correspondence = Correspondence.Compile(source, Dictionary).Correspondence!;

        var rendered = correspondence.Render(Benchmark.Data("""{ "amountOwed": "lots" }"""), Benchmark.British);

        rendered.Docx.Should().BeNull();
        rendered.Email.Should().BeNull();
        rendered.Sms.Should().BeNull();
        rendered.Diagnostics.Select(d => d.Code).Should().BeEquivalentTo([DiagnosticCodes.ValueMissing, DiagnosticCodes.ValueWrongType]);
    }

    private static CorrespondenceCompilation Compile(string letter)
    {
        using var document = File.OpenRead(Benchmark.PathTo(letter, "document.docx"));
        var source = new CorrespondenceSource
        {
            Document = document,
            Email = File.ReadAllText(Benchmark.PathTo(letter, "email.md")),
            Sms = File.ReadAllText(Benchmark.PathTo(letter, "sms.txt")),
        };
        return Correspondence.Compile(source, Benchmark.Dictionary(letter));
    }
}
