using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>The second benchmark letter: whole sections that appear for some recipients only. See benchmarks/decision-letter.</summary>
public class DecisionLetterTests
{
    private const string Letter = "decision-letter";

    [Fact]
    public void The_template_compiles_with_nothing_to_report()
    {
        Benchmark.Compile(Letter).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void An_approved_applicant_gets_the_approval_and_not_the_refusal()
    {
        var paragraphs = RenderParagraphs("approved");

        paragraphs.Should().Contain("Your juggling event licence has been approved");
        paragraphs.Should().Contain("Your licence number is JL-27-00381.");
        paragraphs.Should().NotContain("We have refused your application for a juggling event licence");
        paragraphs.Should().NotContain("Why");
    }

    [Fact]
    public void A_refused_applicant_gets_the_refusal_and_not_the_approval()
    {
        var paragraphs = RenderParagraphs("refused");

        paragraphs.Should().Contain("We have refused your application for a juggling event licence");
        paragraphs.Should().Contain("We cannot license Clubtown Winter Torch Night on 30 January 2027.");
        paragraphs.Should().NotContain("Your juggling event licence has been approved");
        paragraphs.Should().NotContain("What you need to do");
    }

    [Fact]
    public void The_section_inside_a_section_appears_only_when_both_conditions_hold()
    {
        const string heading = "Fire, knives and other dangerous props";

        RenderParagraphs("approved").Should().NotContain(heading);
        RenderParagraphs("approved-dangerous-props").Should().Contain(heading)
            .And.Contain("have a trained fire marshal present for the whole event");
        RenderParagraphs("refused").Should().NotContain(heading, "the applicant had dangerous props but the licence was refused");
    }

    [Fact]
    public void A_hidden_section_leaves_nothing_behind()
    {
        foreach (var sample in new[] { "approved", "approved-dangerous-props", "refused" })
        {
            var paragraphs = RenderParagraphs(sample);

            paragraphs.Should().NotContain("", $"a hidden section in the {sample} letter should leave no empty paragraph");
            paragraphs.Should().NotContain(paragraph => paragraph.Contains("if))", StringComparison.OrdinalIgnoreCase)
                                                        || paragraph.Contains("((if", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Data_that_belongs_to_the_other_outcome_can_be_left_out()
    {
        // The refused sample has no licence number, fee or fee due date. The approved one has no reason or deadline.
        Render("refused").Diagnostics.Should().BeEmpty();
        Render("approved").Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Money_and_numbers_are_printed_without_the_author_choosing_a_format()
    {
        RenderParagraphs("approved").Should().Contain(
            "Pay the licence fee of £45.00 by 9 January 2027. Your licence is not valid until we receive it.");
        RenderParagraphs("approved-dangerous-props").Should().Contain(
            "Pay the licence fee of £120.50 by 9 January 2027. Your licence is not valid until we receive it.");
        string.Concat(RenderParagraphs("approved")).Should().Contain("for up to 24 jugglers.");
    }

    [Fact]
    public void A_caseworkers_free_text_keeps_its_paragraphs_and_cannot_become_a_tag()
    {
        var reason = RenderParagraphs("refused").Single(paragraph => paragraph.StartsWith("The risk assessment", StringComparison.Ordinal));

        reason.Should().Contain("audience area.\n\nOur insurer, SafetyNet Underwriting,");
        reason.Should().Contain("\"cover excludes naked flame in venues under 200 m² & with fewer than 2 exits\"");
        reason.Should().Contain("Do not write ((licence number)) or {{fields}} here");
    }

    [EditorFact]
    public void The_letter_renders_the_same_after_LibreOffice_has_saved_it()
    {
        using var saved = new MemoryStream(LibreOffice.OpenAndSave(Benchmark.PathTo(Letter, "document.docx")));
        var compilation = DocumentTemplate.Compile(saved, Benchmark.Dictionary(Letter));

        compilation.Diagnostics.Should().BeEmpty();
        foreach (var sample in new[] { "approved-dangerous-props", "refused" })
        {
            var after = compilation.Document!.Render(Benchmark.Sample(Letter, sample), Benchmark.British).Docx!;
            TestDocx.Paragraphs(after).Should().Equal(RenderParagraphs(sample));
        }
    }

    private static DocumentRender Render(string sample) =>
        Benchmark.Compile(Letter).Document!.Render(Benchmark.Sample(Letter, sample), Benchmark.British);

    private static IReadOnlyList<string> RenderParagraphs(string sample)
    {
        var render = Render(sample);
        render.Diagnostics.Should().BeEmpty();
        return TestDocx.Paragraphs(render.Docx!);
    }
}
