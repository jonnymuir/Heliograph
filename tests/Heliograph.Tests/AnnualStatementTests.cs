using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>The third benchmark letter: a table with a row for each item in a list. See benchmarks/annual-statement.</summary>
public class AnnualStatementTests
{
    private const string Letter = "annual-statement";

    [Fact]
    public void The_template_compiles_with_nothing_to_report()
    {
        Benchmark.Compile(Letter).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void There_is_a_row_for_each_contribution_between_the_heading_row_and_the_totals()
    {
        var cells = TestDocx.TableRows(Render("typical"));

        cells.Should().HaveCount(1 + 12 + 2);
        cells[0].Should().Equal("Date", "Description", "Amount (£)", "Running total (£)");
        cells[1].Should().Equal("2 December 2025", "Monthly contribution", "12.50", "12.50");
        cells[12].Should().Equal("2 November 2026", "Monthly contribution", "12.50", "150.00");
        cells[13].Should().Equal("", "Total paid", "", "£150.00");
        cells[14].Should().Equal("", "Amount due for the year", "", "£150.00");
    }

    [Fact]
    public void A_refund_is_a_negative_amount_and_the_arrears_paragraph_appears()
    {
        var docx = Render("refund-and-arrears");

        TestDocx.TableLines(docx).Should().Contain("17 February 2026 | Refund: charged twice in February | -12.50 | 62.50");
        TestDocx.Paragraphs(docx).Should().Contain("You owe £112.50")
            .And.Contain("Pay £112.50 by 4 January 2027 to keep your membership. Quote your member reference, M-0042181.");
    }

    [Fact]
    public void A_member_with_no_contributions_gets_a_sentence_and_no_table()
    {
        var docx = Render("no-contributions");

        TestDocx.TableRows(docx).Should().BeEmpty();
        TestDocx.Paragraphs(docx).Should().Contain("We did not receive any contributions from you in this period.");
    }

    [Fact]
    public void Sixty_contributions_make_sixty_rows_in_order()
    {
        var rows = TestDocx.TableRows(Render("weekly-payer"));

        rows.Should().HaveCount(1 + 60 + 2);
        rows[1][0].Should().Be("5 December 2025");
        rows[60][0].Should().Be("28 November 2026");
        rows.Skip(1).Take(60).Select(row => row[3]).Last().Should().Be("184.00");
    }

    [Fact]
    public void The_table_settings_an_author_chose_survive_on_every_row()
    {
        var xml = TestDocx.PartText(Render("weekly-payer"), "word/document.xml");

        System.Text.RegularExpressions.Regex.Matches(xml, "<w:tblHeader").Should().HaveCount(1, "only the heading row repeats on each page");
        System.Text.RegularExpressions.Regex.Matches(xml, "<w:cantSplit").Should().HaveCount(63, "no row may split across pages");
    }

    [Theory]
    [InlineData("typical")]
    [InlineData("weekly-payer")]
    [InlineData("no-contributions")]
    public void The_rendered_statement_is_a_valid_document(string sample)
    {
        var docx = Render(sample);

        WordRules.Problems(docx).Should().BeEmpty();
        WordSchema.Problems(docx).Except(WordSchema.Problems(File.ReadAllBytes(Benchmark.PathTo(Letter, "document.docx")))).Should().BeEmpty();
    }

    [EditorFact]
    public void The_statement_renders_the_same_after_LibreOffice_has_saved_it()
    {
        using var saved = new MemoryStream(LibreOffice.OpenAndSave(Benchmark.PathTo(Letter, "document.docx")));
        var compilation = DocumentTemplate.Compile(saved, Benchmark.Dictionary(Letter));

        compilation.Diagnostics.Should().BeEmpty();
        var after = compilation.Document!.Render(Benchmark.Sample(Letter, "refund-and-arrears"), Benchmark.British).Docx!;
        TestDocx.TableLines(after).Should().Equal(TestDocx.TableLines(Render("refund-and-arrears")));
    }

    [EditorFact]
    public void A_sixty_row_statement_opens_in_LibreOffice()
    {
        var reopened = LibreOffice.OpenAndSave(Render("weekly-payer"));

        TestDocx.TableRows(reopened).Should().HaveCount(63);
    }

    private static byte[] Render(string sample)
    {
        var render = Benchmark.Compile(Letter).Document!.Render(Benchmark.Sample(Letter, sample), Benchmark.British);
        render.Diagnostics.Should().BeEmpty();
        return render.Docx!;
    }
}
